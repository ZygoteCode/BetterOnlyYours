import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';

import '../core/models/vault_entry.dart';
import '../core/models/vault_meta.dart';
import '../core/security/totp.dart';
import '../core/security/totp_secret_box.dart';
import '../core/security/vault_crypto.dart';
import '../core/security/vault_exception.dart';
import '../core/security/vault_kdf.dart';
import '../core/security/vault_repository.dart';
import '../core/security/vault_session.dart';
import '../core/services/fuzzy_search.dart';
import '../core/services/vault_health.dart';
import '../l10n/l10n.dart';
import 'settings_controller.dart';
import 'toast_controller.dart';

enum VaultStatus { loading, missing, locked, unlocked }

enum SaveState { idle, saving, saved, failed }

enum EntrySort {
  titleAsc('Name (A–Z)'),
  titleDesc('Name (Z–A)'),
  recentlyUsed('Recently opened'),
  recentlyUpdated('Recently modified');

  const EntrySort(this.label);

  final String label;
}

class DeletedEntry {
  const DeletedEntry({required this.entry, required this.at});

  final VaultEntry entry;
  final DateTime at;
}

/// Owns the vault session and everything derived from it.
///
/// The master password is never retained: unlocking derives a key which is
/// kept in a [VaultSession] and wiped on lock.
class VaultController extends ChangeNotifier {
  VaultController({
    required this._toasts,
    required this._settings,
    VaultStore? repository,
  }) : _repository = repository ?? VaultRepository() {
    // Turning auto-lock on in Settings must arm the timer immediately, not
    // only after the next interaction.
    _settings.addListener(_ensureAutoLockTimer);
  }

  final ToastController _toasts;

  /// Attached by the widget tree so background notifications (unlock upgrade,
  /// save failures, auto-lock) speak the user's language. English is used
  /// until it is set.
  AppLocalizations? localizations;
  final SettingsController _settings;
  final VaultStore _repository;

  final Map<String, VaultEntry> _entries = <String, VaultEntry>{};

  VaultSession? _session;
  VaultMeta _meta = const VaultMeta();
  VaultStatus _status = VaultStatus.loading;
  SaveState _saveState = SaveState.idle;
  VaultException? _lastSaveError;
  DateTime? _lastSavedAt;
  DateTime _lastActivity = DateTime.now();

  String? _selectedTitle;
  String _query = '';
  String? _tagFilter;
  bool _favoritesOnly = false;
  EntrySort _sort = EntrySort.titleAsc;

  DeletedEntry? _lastDeleted;
  VaultFileInfo? _fileInfo;

  VaultHealthReport? _health;
  Uint8List? _totpKey;
  Future<void> _saveChain = Future<void>.value();
  Timer? _metaFlushTimer;
  Timer? _autoLockTimer;
  bool _metaDirty = false;

  VaultStatus get status => _status;
  bool get isUnlocked => _status == VaultStatus.unlocked;
  SaveState get saveState => _saveState;
  VaultException? get lastSaveError => _lastSaveError;
  DateTime? get lastSavedAt => _lastSavedAt;
  VaultFileInfo? get fileInfo => _fileInfo;

  String get query => _query;
  String? get tagFilter => _tagFilter;
  bool get favoritesOnly => _favoritesOnly;
  EntrySort get sort => _sort;
  String? get selectedTitle => _selectedTitle;
  VaultEntry? get selectedEntry =>
      _selectedTitle == null ? null : _entries[_selectedTitle];

  int get entryCount => _entries.length;
  bool get isEmptyVault => _entries.isEmpty;

  List<VaultEntry> get allEntries => _sorted(_entries.values.toList());

  List<VaultEntry> get favorites =>
      _sorted(_entries.values.where((e) => e.favorite).toList());

  List<VaultEntry> get legacyEntries =>
      _entries.values.where((e) => e.isLegacyFormat).toList();

  /// Password hygiene across the vault: weak passwords and reuse.
  ///
  /// Computed lazily and cached until the next change, so several widgets can
  /// read it in the same frame without re-analysing the vault.
  VaultHealthReport get health =>
      _health ??= VaultHealth.analyze(_entries.values);

  /// Entries ordered by the moment they were last opened.
  List<VaultEntry> get recentEntries {
    final withTimes = _entries.values
        .where((e) => _meta.lastUsed.containsKey(e.title))
        .toList();
    withTimes.sort(
      (a, b) => _meta.lastUsed[b.title]!.compareTo(_meta.lastUsed[a.title]!),
    );
    return withTimes;
  }

  DateTime? lastUsedAt(String title) => _meta.lastUsed[title];

  List<VaultEntry> get recentlyModified {
    final withTimes = _entries.values.where((e) => e.updatedAt != null).toList()
      ..sort((a, b) => b.updatedAt!.compareTo(a.updatedAt!));
    return withTimes;
  }

  Map<String, int> get tagCounts {
    final counts = <String, int>{};
    for (final entry in _entries.values) {
      for (final tag in entry.tags) {
        counts[tag] = (counts[tag] ?? 0) + 1;
      }
    }
    return counts;
  }

  List<String> get allTags {
    final tags = tagCounts.keys.toList()
      ..sort((a, b) => a.toLowerCase().compareTo(b.toLowerCase()));
    return tags;
  }

  /// Entries matching the current search text, tag filter and favourite
  /// filter, ranked by fuzzy score when a query is active.
  List<VaultEntry> get visibleEntries => searchEntries(_query);

  List<VaultEntry> searchEntries(
    String query, {
    bool applyFilters = true,
    int? limit,
  }) {
    var pool = _entries.values.toList();
    if (applyFilters) {
      if (_favoritesOnly) {
        pool = pool.where((e) => e.favorite).toList();
      }
      final tag = _tagFilter;
      if (tag != null) {
        pool = pool.where((e) => e.tags.contains(tag)).toList();
      }
    }

    final trimmed = query.trim();
    if (trimmed.isEmpty) {
      final sorted = _sorted(pool);
      return limit == null ? sorted : sorted.take(limit).toList();
    }

    final scored = <({VaultEntry entry, int score})>[];
    for (final entry in pool) {
      final match = FuzzySearch.match(trimmed, entry.searchText);
      if (match != null) {
        scored.add((entry: entry, score: match.score));
      }
    }
    scored.sort((a, b) {
      final byScore = b.score.compareTo(a.score);
      if (byScore != 0) return byScore;
      return a.entry.title.toLowerCase().compareTo(b.entry.title.toLowerCase());
    });
    final result = scored.map((s) => s.entry).toList();
    return limit == null ? result : result.take(limit).toList();
  }

  List<VaultEntry> _sorted(List<VaultEntry> entries) {
    final sorted = List<VaultEntry>.from(entries);
    switch (_sort) {
      case EntrySort.titleAsc:
        sorted.sort(
          (a, b) => a.title.toLowerCase().compareTo(b.title.toLowerCase()),
        );
      case EntrySort.titleDesc:
        sorted.sort(
          (a, b) => b.title.toLowerCase().compareTo(a.title.toLowerCase()),
        );
      case EntrySort.recentlyUsed:
        sorted.sort((a, b) {
          final aTime = _meta.lastUsed[a.title];
          final bTime = _meta.lastUsed[b.title];
          if (aTime == null && bTime == null) {
            return a.title.toLowerCase().compareTo(b.title.toLowerCase());
          }
          if (aTime == null) return 1;
          if (bTime == null) return -1;
          return bTime.compareTo(aTime);
        });
      case EntrySort.recentlyUpdated:
        sorted.sort((a, b) {
          final aTime = a.updatedAt;
          final bTime = b.updatedAt;
          if (aTime == null && bTime == null) {
            return a.title.toLowerCase().compareTo(b.title.toLowerCase());
          }
          if (aTime == null) return 1;
          if (bTime == null) return -1;
          return bTime.compareTo(aTime);
        });
    }
    return sorted;
  }

  // ---------------------------------------------------------------- lifecycle

  Future<void> initialize() async {
    _status = VaultStatus.loading;
    notifyListeners();
    await refreshFileInfo();
    _status = (_fileInfo?.exists ?? false)
        ? VaultStatus.locked
        : VaultStatus.missing;
    notifyListeners();
  }

  Future<void> refreshFileInfo() async {
    try {
      _fileInfo = await _repository.describe();
    } catch (error) {
      debugPrint('Vault file could not be inspected: $error');
    }
    notifyListeners();
  }

  Future<VaultException?> createVault(String password) async {
    notifyListeners();
    try {
      final now = DateTime.now();
      _session = await _repository.create(password);
      _entries.clear();
      _meta = VaultMeta(createdAt: now, updatedAt: now);
      _status = VaultStatus.unlocked;
      _lastSavedAt = now;
      _saveState = SaveState.saved;
      _noteActivityInternal();
      await refreshFileInfo();
      return null;
    } on VaultException catch (error) {
      return error;
    } catch (error) {
      return VaultException(
        VaultErrorKind.ioFailure,
        'Vault could not be created.',
        cause: error,
      );
    } finally {
      notifyListeners();
    }
  }

  Future<VaultException?> unlock(String password) async {
    notifyListeners();
    try {
      final result = await _repository.open(password);
      _session?.dispose();
      _session = result.session;
      _loadEntries(result.entries);
      _status = VaultStatus.unlocked;
      _saveState = SaveState.idle;
      _lastSaveError = null;
      _noteActivityInternal();
      await refreshFileInfo();

      final l10n = localizations;
      if (result.recoveredFromBackup) {
        _toasts.warning(
          l10n?.vaultRestoredBackup ?? 'Vault restored from backup',
          detail:
              l10n?.vaultRestoredBackupDetail ??
              'The main file could not be read, so the .bak copy was opened.',
        );
      }
      if (result.upgradedFormat) {
        final describe = VaultKdfParams.current.describe();
        _toasts.success(
          l10n?.vaultUpgraded ?? 'Vault upgraded',
          detail:
              l10n?.vaultUpgradedDetail(describe) ??
              'Re-encrypted with stronger key derivation ($describe).',
        );
      }
      return null;
    } on VaultException catch (error) {
      return error;
    } catch (error) {
      return VaultException(
        VaultErrorKind.ioFailure,
        'Vault could not be opened.',
        cause: error,
      );
    } finally {
      notifyListeners();
    }
  }

  void lock({String? reason}) {
    if (_status != VaultStatus.unlocked) return;

    if (_metaDirty) {
      // Flush usage metadata before the key is discarded.
      unawaited(_persist(silent: true));
    }

    _metaFlushTimer?.cancel();
    _metaFlushTimer = null;
    _autoLockTimer?.cancel();
    _autoLockTimer = null;

    _entries.clear();
    _meta = const VaultMeta();
    VaultCrypto.wipe(_totpKey);
    _totpKey = null;
    _selectedTitle = null;
    _query = '';
    _tagFilter = null;
    _favoritesOnly = false;
    _lastDeleted = null;
    _status = VaultStatus.locked;
    _saveState = SaveState.idle;

    // The session is disposed after any in-flight save completes so a pending
    // write can never lose data.
    final session = _session;
    _session = null;
    _saveChain = _saveChain.whenComplete(() => session?.dispose());

    if (reason != null) {
      _toasts.show(
        localizations?.vaultLocked ?? 'Vault locked',
        detail: reason,
      );
    }
    notifyListeners();
  }

  Future<VaultException?> changeMasterPassword(
    String currentPassword,
    String newPassword,
  ) async {
    final session = _session;
    if (session == null) {
      return const VaultException(
        VaultErrorKind.ioFailure,
        'The vault must be unlocked first.',
      );
    }

    notifyListeners();
    try {
      final valid = await _repository.verifyPassword(currentPassword, session);
      if (!valid) {
        return const VaultException(
          VaultErrorKind.invalidPassword,
          'The current master password does not match.',
        );
      }
      final next = await _repository.reKey(newPassword, _serialize());
      _session = next;
      session.dispose();
      _lastSavedAt = DateTime.now();
      _saveState = SaveState.saved;
      await refreshFileInfo();
      return null;
    } on VaultException catch (error) {
      return error;
    } catch (error) {
      return VaultException(
        VaultErrorKind.ioFailure,
        'The master password could not be changed.',
        cause: error,
      );
    } finally {
      notifyListeners();
    }
  }

  // -------------------------------------------------------------------- CRUD

  bool hasEntry(String title) => _entries.containsKey(title);

  VaultEntry? entry(String title) => _entries[title];

  /// Creates or updates an entry. When [previousTitle] differs from
  /// `entry.title` the entry is renamed, preserving its position and history.
  Future<bool> upsertEntry(VaultEntry entry, {String? previousTitle}) async {
    final title = entry.title.trim();
    if (title.isEmpty) return false;
    if (title == VaultMeta.storageKey) return false;

    final normalized = entry.copyWith(
      title: title,
      tags: _normalizeTags(entry.tags),
    );

    final previous = _entries[previousTitle ?? title];

    if (previousTitle != null && previousTitle != title) {
      _entries.remove(previousTitle);
      _meta = _meta.renamed(previousTitle, title);
      if (_selectedTitle == previousTitle) _selectedTitle = title;
    }

    // Password rotation is recorded centrally, so every path that changes a
    // password (editor, generator, restore) keeps the same history.
    var stored = normalized;
    if (!_settings.settings.keepPasswordHistory) {
      stored = stored.withoutPasswordHistory();
    } else if (previous != null) {
      stored = stored.withHistoryFrom(previous);
    }

    _entries[title] = stored.touched();
    _noteActivityInternal();
    notifyListeners();
    return _persist();
  }

  Future<bool> deleteEntry(String title) async {
    final removed = _entries.remove(title);
    if (removed == null) return false;

    _lastDeleted = DeletedEntry(entry: removed, at: DateTime.now());
    _meta = _meta.withoutEntry(title);
    if (_selectedTitle == title) _selectedTitle = null;
    _noteActivityInternal();
    notifyListeners();
    return _persist();
  }

  Future<bool> restoreLastDeleted() async {
    final deleted = _lastDeleted;
    if (deleted == null) return false;

    var title = deleted.entry.title;
    if (_entries.containsKey(title)) {
      title = '$title (restored)';
    }
    _entries[title] = deleted.entry.copyWith(title: title);
    _lastDeleted = null;
    _selectedTitle = title;
    notifyListeners();
    return _persist();
  }

  Future<bool> duplicateEntry(String title) async {
    final source = _entries[title];
    if (source == null) return false;

    var copyTitle = '$title copy';
    var index = 2;
    while (_entries.containsKey(copyTitle)) {
      copyTitle = '$title copy $index';
      index++;
    }
    final now = DateTime.now();
    // A duplicate starts clean: old secrets are not copied around, and a
    // second entry silently generating the same 2FA codes would be impossible
    // to tell apart from the original.
    _entries[copyTitle] = source
        .withoutPasswordHistory()
        .withoutTotp()
        .copyWith(title: copyTitle, createdAt: now, updatedAt: now);
    _selectedTitle = copyTitle;
    notifyListeners();
    return _persist();
  }

  Future<bool> toggleFavorite(String title) async {
    final entry = _entries[title];
    if (entry == null) return false;
    _entries[title] = entry.copyWith(favorite: !entry.favorite).touched();
    notifyListeners();
    return _persist();
  }

  Future<bool> setTags(String title, List<String> tags) async {
    final entry = _entries[title];
    if (entry == null) return false;
    _entries[title] = entry.copyWith(tags: _normalizeTags(tags)).touched();
    notifyListeners();
    return _persist();
  }

  // ------------------------------------------------------ password history

  /// Puts a superseded password back in use. The password being replaced is
  /// pushed onto the history by [upsertEntry], so nothing is lost.
  Future<bool> restorePassword(String title, VaultPasswordRecord record) {
    final entry = _entries[title];
    if (entry == null) return Future<bool>.value(false);
    return upsertEntry(entry.copyWith(password: record.password));
  }

  Future<bool> forgetPassword(String title, VaultPasswordRecord record) async {
    final entry = _entries[title];
    if (entry == null) return false;
    final history = entry.passwordHistory
        .where((item) => item != record)
        .toList();
    if (history.length == entry.passwordHistory.length) return false;
    _entries[title] = entry.copyWith(passwordHistory: history);
    notifyListeners();
    return _persist();
  }

  Future<bool> clearPasswordHistory(String title) async {
    final entry = _entries[title];
    if (entry == null || entry.passwordHistory.isEmpty) return false;
    _entries[title] = entry.withoutPasswordHistory();
    notifyListeners();
    return _persist();
  }

  /// Drops every superseded password in the vault in a single write.
  Future<bool> clearAllPasswordHistory() async {
    var changed = false;
    for (final entry in _entries.values.toList()) {
      if (entry.passwordHistory.isEmpty) continue;
      _entries[entry.title] = entry.withoutPasswordHistory();
      changed = true;
    }
    if (!changed) return false;
    notifyListeners();
    return _persist();
  }

  int get storedPasswordHistoryCount => _entries.values.fold<int>(
    0,
    (total, entry) => total + entry.passwordHistory.length,
  );

  // ------------------------------------------------------------ two-factor

  /// How many entries carry a two-factor token.
  int get totpCount => _entries.values.where((entry) => entry.hasTotp).length;

  /// The code valid at [at] (now by default), or null when the entry has no
  /// token or the token cannot be opened.
  ///
  /// The secret is unsealed, used and wiped inside this call: it never becomes
  /// a field, a return value or anything the interface could render.
  TotpCode? totpCode(VaultEntry entry, {DateTime? at}) {
    final material = TotpSecretBox.tryOpen(entry.totp, _contentKey());
    if (material == null) return null;
    return material.codeThenDispose(at: at);
  }

  /// Algorithm, digits, period and labels of an entry's token — never the
  /// secret. Null when there is no readable token.
  TotpConfig? totpConfig(VaultEntry entry) {
    final material = TotpSecretBox.tryOpen(entry.totp, _contentKey());
    if (material == null) return null;
    final config = material.config;
    material.dispose();
    return config;
  }

  /// True when the entry has a token that this vault cannot open — a sign the
  /// content key was lost, not that the token is wrong.
  bool totpIsUnreadable(VaultEntry entry) =>
      entry.hasTotp && totpConfig(entry) == null;

  /// Seals [secret] into the entry. The caller keeps ownership of the buffer
  /// and is expected to wipe it afterwards.
  Future<bool> setTotp(
    String title, {
    required Uint8List secret,
    required TotpConfig config,
  }) async {
    final entry = _entries[title];
    if (entry == null || _status != VaultStatus.unlocked) return false;

    final String sealed;
    try {
      sealed = TotpSecretBox.seal(
        secret: secret,
        config: config,
        contentKey: _ensureContentKey(),
      );
    } on TotpSealException {
      return false;
    }

    _entries[title] = entry.copyWith(totp: sealed).touched();
    _noteActivityInternal();
    notifyListeners();
    return _persist();
  }

  Future<bool> removeTotp(String title) async {
    final entry = _entries[title];
    if (entry == null || !entry.hasTotp) return false;
    _entries[title] = entry.withoutTotp().touched();
    notifyListeners();
    return _persist();
  }

  /// The vault-wide key that seals tokens, or null while locked / before the
  /// first token was ever stored.
  Uint8List? _contentKey() {
    if (_status != VaultStatus.unlocked) return null;
    final cached = _totpKey;
    if (cached != null) return cached;

    final stored = _meta.secretKey;
    if (stored.isEmpty) return null;
    try {
      final bytes = base64.decode(stored);
      if (bytes.length != TotpSecretBox.contentKeyLength) return null;
      return _totpKey = bytes;
    } on FormatException {
      return null;
    }
  }

  Uint8List _ensureContentKey() {
    final existing = _contentKey();
    if (existing != null) return existing;

    final key = TotpSecretBox.newContentKey();
    final encoded = base64.encode(key);
    // A key already recorded but unusable (wrong length, not base64) cannot
    // open anything, so replacing it loses nothing that still worked.
    _meta = _meta.secretKey.isEmpty
        ? _meta.withSecretKey(encoded)
        : _meta.copyWith(secretKey: encoded);
    return _totpKey = key;
  }

  /// Retries the last failed write.
  Future<bool> retrySave() => _persist();

  /// Waits for in-flight writes and flushes pending usage metadata. Called
  /// before the window closes so nothing is lost on exit.
  Future<void> flushPendingWrites() async {
    if (_metaDirty && _status == VaultStatus.unlocked) {
      await _persist(silent: true);
    }
    await _saveChain;
  }

  // ------------------------------------------------------------ selection/UI

  void select(String? title, {bool markAsUsed = true}) {
    if (title != null && !_entries.containsKey(title)) return;

    final changed = _selectedTitle != title;
    _selectedTitle = title;
    // Opening an entry counts as usage even when it is already selected —
    // that is what "recently opened" means to the user.
    if (title != null && markAsUsed) {
      _recordUsage(title);
    }
    _noteActivityInternal();
    if (changed || (title != null && markAsUsed)) {
      notifyListeners();
    }
  }

  void setQuery(String value) {
    if (_query == value) return;
    _query = value;
    notifyListeners();
  }

  void setTagFilter(String? tag) {
    if (_tagFilter == tag) return;
    _tagFilter = tag;
    notifyListeners();
  }

  void setFavoritesOnly(bool value) {
    if (_favoritesOnly == value) return;
    _favoritesOnly = value;
    notifyListeners();
  }

  void setSort(EntrySort sort) {
    if (_sort == sort) return;
    _sort = sort;
    notifyListeners();
  }

  void clearFilters() {
    if (_query.isEmpty && _tagFilter == null && !_favoritesOnly) return;
    _query = '';
    _tagFilter = null;
    _favoritesOnly = false;
    notifyListeners();
  }

  /// Called by the shell whenever the user interacts, to drive the optional
  /// inactivity auto-lock.
  void noteActivity() => _noteActivityInternal();

  // -------------------------------------------------------------- internals

  void _loadEntries(Map<String, String> raw) {
    _entries.clear();
    final metaRaw = raw[VaultMeta.storageKey];
    _meta = metaRaw == null
        ? const VaultMeta()
        : VaultMeta.fromStorage(metaRaw);

    for (final item in raw.entries) {
      if (item.key == VaultMeta.storageKey) continue;
      _entries[item.key] = VaultEntry.fromStorage(item.key, item.value);
    }
    _selectedTitle = null;
  }

  Map<String, String> _serialize() {
    final raw = <String, String>{};
    for (final entry in _entries.values) {
      raw[entry.title] = entry.toStorageValue();
    }
    final meta = _meta.copyWith(updatedAt: DateTime.now());
    _meta = meta;
    if (meta.lastUsed.isNotEmpty ||
        meta.createdAt != null ||
        meta.secretKey.isNotEmpty ||
        meta.extra.isNotEmpty) {
      raw[VaultMeta.storageKey] = meta.toStorageValue();
    }
    return raw;
  }

  void _recordUsage(String title) {
    _meta = _meta.withAccess(title);
    _metaDirty = true;
    _metaFlushTimer?.cancel();
    _metaFlushTimer = Timer(const Duration(seconds: 5), () {
      if (_metaDirty && _status == VaultStatus.unlocked) {
        unawaited(_persist(silent: true));
      }
    });
  }

  List<String> _normalizeTags(List<String> tags) {
    final seen = <String>{};
    final result = <String>[];
    for (final raw in tags) {
      final tag = raw.trim();
      if (tag.isEmpty) continue;
      final key = tag.toLowerCase();
      if (seen.add(key)) result.add(tag);
    }
    result.sort((a, b) => a.toLowerCase().compareTo(b.toLowerCase()));
    return result;
  }

  Future<bool> _persist({bool silent = false}) {
    final completer = Completer<bool>();
    final previous = _saveChain;
    _saveChain = completer.future;

    unawaited(
      previous.then((_) async {
        final ok = await _write(silent: silent);
        completer.complete(ok);
      }),
    );

    return completer.future;
  }

  Future<bool> _write({required bool silent}) async {
    final session = _session;
    if (session == null || session.isDisposed) return false;

    _saveState = SaveState.saving;
    notifyListeners();

    try {
      await _repository.save(_serialize(), session);
      _metaDirty = false;
      _lastSavedAt = DateTime.now();
      _lastSaveError = null;
      _saveState = SaveState.saved;
      notifyListeners();
      return true;
    } on VaultException catch (error) {
      _lastSaveError = error;
      _saveState = SaveState.failed;
      notifyListeners();
      if (!silent) {
        final l10n = localizations;
        _toasts.error(
          l10n == null ? error.title : error.localizedTitle(l10n),
          detail: l10n == null
              ? '${error.hint} Your changes are still here — retry the save.'
              : l10n.vaultSaveRetryHint(error.localizedHint(l10n)),
        );
      }
      return false;
    } catch (error) {
      _lastSaveError = VaultException(
        VaultErrorKind.ioFailure,
        'Unexpected failure while writing the vault.',
        cause: error,
      );
      _saveState = SaveState.failed;
      notifyListeners();
      if (!silent) {
        _toasts.error(
          localizations?.vaultSaveFailed ?? 'Vault could not be saved',
          detail:
              localizations?.vaultSaveFailedDetail ??
              'Your changes are still here — retry the save.',
        );
      }
      return false;
    }
  }

  void _noteActivityInternal() {
    _lastActivity = DateTime.now();
    _ensureAutoLockTimer();
  }

  void _ensureAutoLockTimer() {
    if (_status != VaultStatus.unlocked) return;
    if (!_settings.settings.autoLockEnabled) {
      _autoLockTimer?.cancel();
      _autoLockTimer = null;
      return;
    }
    _autoLockTimer ??= Timer.periodic(
      const Duration(seconds: 20),
      (_) => _checkAutoLock(),
    );
  }

  void _checkAutoLock() {
    final minutes = _settings.settings.autoLockMinutes;
    if (minutes <= 0 || _status != VaultStatus.unlocked) {
      _autoLockTimer?.cancel();
      _autoLockTimer = null;
      return;
    }
    final idleFor = DateTime.now().difference(_lastActivity);
    if (idleFor.inMinutes >= minutes) {
      lock(
        reason:
            localizations?.vaultLockedInactivity(minutes) ??
            'Locked after $minutes minutes of inactivity.',
      );
    }
  }

  /// Any change invalidates the cached health report.
  @override
  void notifyListeners() {
    _health = null;
    super.notifyListeners();
  }

  @override
  void dispose() {
    _settings.removeListener(_ensureAutoLockTimer);
    _metaFlushTimer?.cancel();
    _autoLockTimer?.cancel();
    final session = _session;
    _session = null;
    session?.dispose();
    super.dispose();
  }
}
