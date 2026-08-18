import 'dart:convert';

/// Vault-level metadata stored *inside* the encrypted payload under a reserved
/// key, so that usage information never leaks to plaintext files on disk.
class VaultMeta {
  const VaultMeta({
    this.schemaVersion = currentSchemaVersion,
    this.createdAt,
    this.updatedAt,
    this.lastUsed = const <String, DateTime>{},
    this.extra = const <String, dynamic>{},
  });

  /// Reserved map key. Hidden from the entry list.
  static const String storageKey = '__betteronlyyours_meta__';
  static const String structuredPrefix = 'BOYMETA1:';
  static const int currentSchemaVersion = 1;

  final int schemaVersion;
  final DateTime? createdAt;
  final DateTime? updatedAt;

  /// Entry title -> last time the entry was opened.
  final Map<String, DateTime> lastUsed;

  /// Unknown keys written by newer versions, preserved on round-trip.
  final Map<String, dynamic> extra;

  factory VaultMeta.fromStorage(String raw) {
    var payload = raw;
    if (payload.startsWith(structuredPrefix)) {
      payload = payload.substring(structuredPrefix.length);
    }
    try {
      final decoded = jsonDecode(payload);
      if (decoded is! Map<String, dynamic>) return const VaultMeta();
      const known = <String>{
        'schemaVersion',
        'createdAt',
        'updatedAt',
        'lastUsed',
      };
      final lastUsed = <String, DateTime>{};
      final rawLastUsed = decoded['lastUsed'];
      if (rawLastUsed is Map) {
        for (final entry in rawLastUsed.entries) {
          final value = entry.value;
          if (value is int) {
            lastUsed['${entry.key}'] = DateTime.fromMillisecondsSinceEpoch(
              value,
            );
          }
        }
      }
      return VaultMeta(
        schemaVersion: decoded['schemaVersion'] is int
            ? decoded['schemaVersion'] as int
            : currentSchemaVersion,
        createdAt: _time(decoded['createdAt']),
        updatedAt: _time(decoded['updatedAt']),
        lastUsed: lastUsed,
        extra: <String, dynamic>{
          for (final entry in decoded.entries)
            if (!known.contains(entry.key)) entry.key: entry.value,
        },
      );
    } catch (_) {
      return const VaultMeta();
    }
  }

  String toStorageValue() {
    final json = <String, dynamic>{...extra};
    json['schemaVersion'] = schemaVersion;
    final created = createdAt;
    if (created != null) {
      json['createdAt'] = created.millisecondsSinceEpoch;
    }
    final updated = updatedAt;
    if (updated != null) {
      json['updatedAt'] = updated.millisecondsSinceEpoch;
    }
    if (lastUsed.isNotEmpty) {
      json['lastUsed'] = <String, dynamic>{
        for (final entry in lastUsed.entries)
          entry.key: entry.value.millisecondsSinceEpoch,
      };
    }
    return structuredPrefix + jsonEncode(json);
  }

  VaultMeta copyWith({
    DateTime? createdAt,
    DateTime? updatedAt,
    Map<String, DateTime>? lastUsed,
  }) {
    return VaultMeta(
      schemaVersion: schemaVersion,
      createdAt: createdAt ?? this.createdAt,
      updatedAt: updatedAt ?? this.updatedAt,
      lastUsed: lastUsed ?? this.lastUsed,
      extra: extra,
    );
  }

  /// Records an access, trimming the history to [keep] entries.
  ///
  /// The stamp is forced to be newer than every other one: the Windows clock
  /// has a coarse resolution, and two entries opened in the same millisecond
  /// would otherwise have an undefined "most recent" order.
  VaultMeta withAccess(String title, {DateTime? at, int keep = 40}) {
    var stamp = at ?? DateTime.now();
    for (final existing in lastUsed.entries) {
      if (existing.key == title) continue;
      if (!stamp.isAfter(existing.value)) {
        stamp = existing.value.add(const Duration(milliseconds: 1));
      }
    }
    final updated = Map<String, DateTime>.from(lastUsed)..[title] = stamp;
    if (updated.length > keep) {
      final sorted = updated.entries.toList()
        ..sort((a, b) => b.value.compareTo(a.value));
      updated
        ..clear()
        ..addEntries(sorted.take(keep));
    }
    return copyWith(lastUsed: updated);
  }

  VaultMeta withoutEntry(String title) {
    if (!lastUsed.containsKey(title)) return this;
    final updated = Map<String, DateTime>.from(lastUsed)..remove(title);
    return copyWith(lastUsed: updated);
  }

  VaultMeta renamed(String from, String to) {
    final stamp = lastUsed[from];
    if (stamp == null) return this;
    final updated = Map<String, DateTime>.from(lastUsed)
      ..remove(from)
      ..[to] = stamp;
    return copyWith(lastUsed: updated);
  }

  static DateTime? _time(Object? value) {
    if (value is int) return DateTime.fromMillisecondsSinceEpoch(value);
    if (value is String) return DateTime.tryParse(value);
    return null;
  }
}
