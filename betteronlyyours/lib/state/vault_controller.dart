import 'dart:async';

import 'package:flutter/foundation.dart';

import '../core/models/vault_entry.dart';
import '../core/models/vault_meta.dart';
import '../core/security/vault_exception.dart';
import '../core/security/vault_repository.dart';
import '../core/security/vault_session.dart';
import '../core/services/fuzzy_search.dart';
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
    required ToastController toasts,
    required SettingsController settings,
    VaultStore? repository,
  }) : _toasts = toasts,
       _settings = settings,
       _repository = repository ?? VaultRepository() {
    // Turning auto-lock on in Settings must arm the timer immediately, not
    // only after the next interaction.
    _settings.addListener(_ensureAutoLockTimer);
  }

  final ToastController _toasts;
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

      if (result.recoveredFromBackup) {
        _toasts.warning(
          'Vault restored from backup',
          detail:
              'The main file could not be read, so the .bak copy was opened.',
        );
      }
      if (result.upgradedFormat) {
        _toasts.success(
          'Vault upgraded',
          detail:
              'Re-encrypted with stronger key derivation (format v2, '
              '200,000 PBKDF2 iterations).',
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
      _toasts.show('Vault locked', detail: reason);
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

    if (previousTitle != null && previousTitle != title) {
      _entries.remove(previousTitle);
      _meta = _meta.renamed(previousTitle, title);
      if (_selectedTitle == previousTitle) _selectedTitle = title;
    }

    _entries[title] = normalized.touched();
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
    _entries[copyTitle] = source.copyWith(
      title: copyTitle,
      createdAt: now,
      updatedAt: now,
    );
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
        _toasts.error(
          error.title,
          detail: '${error.hint} Your changes are still here — retry the save.',
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
          'Vault could not be saved',
          detail: 'Your changes are still here — retry the save.',
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
      lock(reason: 'Locked after $minutes minutes of inactivity.');
    }
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
