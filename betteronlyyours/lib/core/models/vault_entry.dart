import 'dart:convert';

/// A single user-defined field attached to an entry.
class VaultCustomField {
  const VaultCustomField({
    required this.label,
    required this.value,
    this.secret = false,
  });

  final String label;
  final String value;
  final bool secret;

  VaultCustomField copyWith({String? label, String? value, bool? secret}) {
    return VaultCustomField(
      label: label ?? this.label,
      value: value ?? this.value,
      secret: secret ?? this.secret,
    );
  }

  Map<String, dynamic> toJson() => <String, dynamic>{
    'label': label,
    'value': value,
    if (secret) 'secret': true,
  };

  static VaultCustomField? fromJson(Object? raw) {
    if (raw is! Map) return null;
    final label = raw['label'];
    final value = raw['value'];
    if (label is! String) return null;
    return VaultCustomField(
      label: label,
      value: value is String ? value : '${value ?? ''}',
      secret: raw['secret'] == true,
    );
  }

  @override
  bool operator ==(Object other) =>
      other is VaultCustomField &&
      other.label == label &&
      other.value == value &&
      other.secret == secret;

  @override
  int get hashCode => Object.hash(label, value, secret);
}

/// A password this entry used before the current one.
class VaultPasswordRecord {
  const VaultPasswordRecord({required this.password, required this.replacedAt});

  final String password;
  final DateTime replacedAt;

  Map<String, dynamic> toJson() => <String, dynamic>{
    'password': password,
    'replacedAt': replacedAt.millisecondsSinceEpoch,
  };

  static VaultPasswordRecord? fromJson(Object? raw) {
    if (raw is! Map) return null;
    final password = raw['password'];
    if (password is! String || password.isEmpty) return null;
    final replacedAt = raw['replacedAt'];
    return VaultPasswordRecord(
      password: password,
      replacedAt: replacedAt is int
          ? DateTime.fromMillisecondsSinceEpoch(replacedAt)
          : DateTime.fromMillisecondsSinceEpoch(0),
    );
  }

  @override
  bool operator ==(Object other) =>
      other is VaultPasswordRecord &&
      other.password == password &&
      other.replacedAt == replacedAt;

  @override
  int get hashCode => Object.hash(password, replacedAt);
}

/// A vault entry.
///
/// Persistence contract (see [toStorageValue] / [fromStorage]):
/// the vault file stores a `Map<String, String>` where the key is the entry
/// title. Historically the value was arbitrary free-form text. Structured
/// entries are stored as `BOY1:<json>` so that legacy note-only entries stay
/// byte-identical on disk and are never rewritten unless the user adds
/// structure to them.
class VaultEntry {
  const VaultEntry({
    required this.title,
    this.username = '',
    this.password = '',
    this.url = '',
    this.notes = '',
    this.tags = const <String>[],
    this.favorite = false,
    this.createdAt,
    this.updatedAt,
    this.customFields = const <VaultCustomField>[],
    this.passwordHistory = const <VaultPasswordRecord>[],
    this.totp = '',
    this.extra = const <String, dynamic>{},
  });

  /// Marker prefix identifying a structured (JSON) payload.
  static const String structuredPrefix = 'BOY1:';

  /// How many superseded passwords an entry keeps. Bounded so a vault cannot
  /// grow without limit, and so an old secret does not linger forever.
  static const int maxPasswordHistory = 20;

  final String title;
  final String username;
  final String password;
  final String url;
  final String notes;
  final List<String> tags;
  final bool favorite;
  final DateTime? createdAt;
  final DateTime? updatedAt;
  final List<VaultCustomField> customFields;

  /// Superseded passwords, newest first.
  final List<VaultPasswordRecord> passwordHistory;

  /// The entry's two-factor token, sealed by [TotpSecretBox].
  ///
  /// Opaque on purpose: this is ciphertext, never the shared secret. Nothing
  /// in the interface can turn it back into text — only the code generator
  /// opens it, and only for as long as it takes to produce six digits.
  final String totp;

  /// Fields written by a newer version of the app. Preserved verbatim so an
  /// older build never silently drops data it does not understand.
  final Map<String, dynamic> extra;

  factory VaultEntry.create(String title, {DateTime? now}) {
    final stamp = now ?? DateTime.now();
    return VaultEntry(title: title, createdAt: stamp, updatedAt: stamp);
  }

  /// Parses a raw stored value. Never throws: anything unparsable is kept as
  /// opaque note text so that user data survives.
  factory VaultEntry.fromStorage(String title, String raw) {
    if (raw.startsWith(structuredPrefix)) {
      try {
        final decoded = jsonDecode(raw.substring(structuredPrefix.length));
        if (decoded is Map<String, dynamic>) {
          return VaultEntry._fromJson(title, decoded);
        }
      } catch (_) {
        // Fall through: keep the payload as plain notes rather than losing it.
      }
    }
    return VaultEntry(title: title, notes: raw);
  }

  factory VaultEntry._fromJson(String title, Map<String, dynamic> json) {
    const known = <String>{
      'title',
      'username',
      'password',
      'url',
      'notes',
      'tags',
      'favorite',
      'createdAt',
      'updatedAt',
      'fields',
      'history',
      'totp',
    };
    final extra = <String, dynamic>{
      for (final entry in json.entries)
        if (!known.contains(entry.key)) entry.key: entry.value,
    };

    return VaultEntry(
      title: title,
      username: _string(json['username']),
      password: _string(json['password']),
      url: _string(json['url']),
      notes: _string(json['notes']),
      tags: _stringList(json['tags']),
      favorite: json['favorite'] == true,
      createdAt: _time(json['createdAt']),
      updatedAt: _time(json['updatedAt']),
      customFields: _fields(json['fields']),
      passwordHistory: _history(json['history']),
      totp: _string(json['totp']),
      extra: extra,
    );
  }

  /// True when the entry carries information that plain text cannot express.
  /// Note-only entries stay in the legacy plain-text representation.
  bool get isStructured =>
      username.isNotEmpty ||
      password.isNotEmpty ||
      url.isNotEmpty ||
      tags.isNotEmpty ||
      favorite ||
      customFields.isNotEmpty ||
      passwordHistory.isNotEmpty ||
      totp.isNotEmpty ||
      createdAt != null ||
      updatedAt != null ||
      extra.isNotEmpty;

  /// True for entries that came from a pre-structured vault and still are
  /// stored as free text.
  bool get isLegacyFormat => !isStructured;

  String toStorageValue() {
    if (!isStructured) return notes;

    final json = <String, dynamic>{...extra};
    json['title'] = title;
    if (username.isNotEmpty) json['username'] = username;
    if (password.isNotEmpty) json['password'] = password;
    if (url.isNotEmpty) json['url'] = url;
    if (notes.isNotEmpty) json['notes'] = notes;
    if (tags.isNotEmpty) json['tags'] = tags;
    if (favorite) json['favorite'] = true;
    final created = createdAt;
    if (created != null) {
      json['createdAt'] = created.millisecondsSinceEpoch;
    }
    final updated = updatedAt;
    if (updated != null) {
      json['updatedAt'] = updated.millisecondsSinceEpoch;
    }
    if (customFields.isNotEmpty) {
      json['fields'] = customFields.map((f) => f.toJson()).toList();
    }
    if (passwordHistory.isNotEmpty) {
      json['history'] = passwordHistory.map((r) => r.toJson()).toList();
    }
    if (totp.isNotEmpty) json['totp'] = totp;
    return structuredPrefix + jsonEncode(json);
  }

  VaultEntry copyWith({
    String? title,
    String? username,
    String? password,
    String? url,
    String? notes,
    List<String>? tags,
    bool? favorite,
    DateTime? createdAt,
    DateTime? updatedAt,
    List<VaultCustomField>? customFields,
    List<VaultPasswordRecord>? passwordHistory,
    String? totp,
    Map<String, dynamic>? extra,
  }) {
    return VaultEntry(
      title: title ?? this.title,
      username: username ?? this.username,
      password: password ?? this.password,
      url: url ?? this.url,
      notes: notes ?? this.notes,
      tags: tags ?? this.tags,
      favorite: favorite ?? this.favorite,
      createdAt: createdAt ?? this.createdAt,
      updatedAt: updatedAt ?? this.updatedAt,
      customFields: customFields ?? this.customFields,
      passwordHistory: passwordHistory ?? this.passwordHistory,
      totp: totp ?? this.totp,
      extra: extra ?? this.extra,
    );
  }

  /// Carries [previous]'s history over to this (edited) entry, recording the
  /// old password when it actually changed.
  ///
  /// Rotation lives here rather than in the editor so every path that changes
  /// a password — editor, generator, restore, import — behaves identically.
  VaultEntry withHistoryFrom(
    VaultEntry previous, {
    DateTime? now,
    int keep = maxPasswordHistory,
  }) {
    final history = <VaultPasswordRecord>[...previous.passwordHistory];
    final replaced = previous.password;
    final changed = replaced.isNotEmpty && replaced != password;

    if (changed) {
      history.removeWhere((record) => record.password == replaced);
      history.insert(
        0,
        VaultPasswordRecord(
          password: replaced,
          replacedAt: now ?? DateTime.now(),
        ),
      );
    }

    // The password in use is never also kept as history.
    history.removeWhere((record) => record.password == password);

    return copyWith(
      passwordHistory: history.length > keep
          ? history.sublist(0, keep)
          : history,
    );
  }

  VaultEntry withoutPasswordHistory() =>
      copyWith(passwordHistory: const <VaultPasswordRecord>[]);

  /// Marks the entry as touched, promoting legacy entries to the structured
  /// format only when they actually gained structure.
  VaultEntry touched({DateTime? now}) {
    final stamp = now ?? DateTime.now();
    if (isLegacyFormat) return this;
    return copyWith(createdAt: createdAt ?? stamp, updatedAt: stamp);
  }

  /// True when the entry carries a two-factor token.
  bool get hasTotp => totp.isNotEmpty;

  /// The entry without its token, used when the user removes 2FA and when an
  /// entry is copied out of the vault it was sealed for.
  VaultEntry withoutTotp() => copyWith(totp: '');

  bool get hasPassword => password.isNotEmpty;
  bool get hasUsername => username.isNotEmpty;
  bool get hasUrl => url.isNotEmpty;

  /// Host part of [url], if it looks like a web address.
  String? get host {
    if (url.trim().isEmpty) return null;
    var raw = url.trim();
    if (!raw.contains('://')) raw = 'https://$raw';
    final uri = Uri.tryParse(raw);
    final host = uri?.host;
    if (host == null || host.isEmpty) return null;
    return host.startsWith('www.') ? host.substring(4) : host;
  }

  /// Text used by search. Secrets are deliberately excluded.
  String get searchText => <String>[
    title,
    username,
    url,
    ...tags,
    ...customFields.where((f) => !f.secret).map((f) => f.label),
  ].join(' ');

  String get subtitle {
    if (username.isNotEmpty) return username;
    final h = host;
    if (h != null) return h;
    if (tags.isNotEmpty) return tags.join(' · ');
    if (notes.trim().isNotEmpty) {
      final firstLine = notes.trim().split('\n').first.trim();
      return firstLine.length > 64
          ? '${firstLine.substring(0, 64)}…'
          : firstLine;
    }
    return 'No details yet';
  }

  static String _string(Object? value) {
    if (value == null) return '';
    return value is String ? value : '$value';
  }

  static List<String> _stringList(Object? value) {
    if (value is! List) return const <String>[];
    return value
        .map((e) => e is String ? e : '$e')
        .where((e) => e.trim().isNotEmpty)
        .toList(growable: false);
  }

  static DateTime? _time(Object? value) {
    if (value is int) {
      return DateTime.fromMillisecondsSinceEpoch(value);
    }
    if (value is String) {
      return DateTime.tryParse(value);
    }
    return null;
  }

  static List<VaultPasswordRecord> _history(Object? value) {
    if (value is! List) return const <VaultPasswordRecord>[];
    final result = <VaultPasswordRecord>[];
    for (final raw in value) {
      final record = VaultPasswordRecord.fromJson(raw);
      if (record != null) result.add(record);
    }
    if (result.length > maxPasswordHistory) {
      return result.sublist(0, maxPasswordHistory);
    }
    return result;
  }

  static List<VaultCustomField> _fields(Object? value) {
    if (value is! List) return const <VaultCustomField>[];
    final result = <VaultCustomField>[];
    for (final raw in value) {
      final field = VaultCustomField.fromJson(raw);
      if (field != null) result.add(field);
    }
    return result;
  }
}
