import 'dart:convert';
import 'dart:typed_data';

import 'package:pointycastle/export.dart';

import '../models/vault_entry.dart';
import '../security/vault_crypto.dart';
import 'password_strength.dart';

/// Entries that share one password.
class ReusedPasswordGroup {
  const ReusedPasswordGroup({required this.entries, required this.strength});

  /// Always two or more, sorted by title.
  final List<VaultEntry> entries;

  /// Strength of the shared password: reuse of a weak password is worse than
  /// reuse of a strong one, and the UI says so.
  final PasswordStrength strength;

  int get count => entries.length;

  List<String> get titles => entries.map((e) => e.title).toList();
}

/// Password hygiene of a whole vault.
class VaultHealthReport {
  const VaultHealthReport({
    required this.entryCount,
    required this.withPassword,
    required this.weak,
    required this.reused,
    required this.withoutPassword,
    required this.reusedTitles,
  });

  static const VaultHealthReport empty = VaultHealthReport(
    entryCount: 0,
    withPassword: 0,
    weak: <VaultEntry>[],
    reused: <ReusedPasswordGroup>[],
    withoutPassword: <VaultEntry>[],
    reusedTitles: <String>{},
  );

  final int entryCount;
  final int withPassword;

  /// Entries whose password rates weak or very weak.
  final List<VaultEntry> weak;

  /// Groups of entries sharing the same password, worst first.
  final List<ReusedPasswordGroup> reused;

  /// Structured entries that have no password at all.
  final List<VaultEntry> withoutPassword;

  /// Fast lookup for "is this entry's password shared with another one?".
  final Set<String> reusedTitles;

  int get reusedEntryCount => reusedTitles.length;

  int get issueCount => weak.length + reusedEntryCount;

  bool get isClean => issueCount == 0;

  bool isReused(String title) => reusedTitles.contains(title);

  /// 0–100 hygiene score. A blunt instrument on purpose: it exists to rank
  /// "needs attention" against "fine", not to be a security metric.
  int get score {
    if (withPassword == 0) return 100;
    final weakShare = weak.length / withPassword;
    final reusedShare = reusedEntryCount / withPassword;
    final raw = 100 - (weakShare * 55) - (reusedShare * 45);
    return raw.clamp(0, 100).round();
  }
}

/// Analyses passwords across the vault.
///
/// Passwords are grouped by a keyed hash rather than by their plaintext, so
/// the report never holds a map keyed by a secret. The key is random per
/// analysis, which also makes the digests useless outside this one report.
class VaultHealth {
  const VaultHealth._();

  static VaultHealthReport analyze(Iterable<VaultEntry> entries) {
    final all = entries.toList();
    if (all.isEmpty) return VaultHealthReport.empty;

    final sessionKey = VaultCrypto.randomBytes(32);
    final byDigest = <String, List<VaultEntry>>{};
    final strengthByDigest = <String, PasswordStrength>{};
    final weak = <VaultEntry>[];
    final withoutPassword = <VaultEntry>[];
    var withPassword = 0;

    try {
      for (final entry in all) {
        if (entry.password.isEmpty) {
          // Legacy note-only entries are not "missing a password", they simply
          // are not credentials.
          if (!entry.isLegacyFormat) withoutPassword.add(entry);
          continue;
        }
        withPassword++;

        final digest = _digest(entry.password, sessionKey);
        final bucket = byDigest.putIfAbsent(digest, () => <VaultEntry>[]);
        bucket.add(entry);

        final strength = strengthByDigest.putIfAbsent(
          digest,
          () => PasswordStrength.evaluate(entry.password),
        );
        if (strength.level == StrengthLevel.veryWeak ||
            strength.level == StrengthLevel.weak) {
          weak.add(entry);
        }
      }
    } finally {
      VaultCrypto.wipe(sessionKey);
    }

    final reused = <ReusedPasswordGroup>[];
    final reusedTitles = <String>{};
    for (final bucket in byDigest.entries) {
      if (bucket.value.length < 2) continue;
      final group = List<VaultEntry>.from(
        bucket.value,
      )..sort((a, b) => a.title.toLowerCase().compareTo(b.title.toLowerCase()));
      reused.add(
        ReusedPasswordGroup(
          entries: group,
          strength: strengthByDigest[bucket.key]!,
        ),
      );
      reusedTitles.addAll(group.map((e) => e.title));
    }

    // Worst first: weaker shared passwords, then larger groups.
    reused.sort((a, b) {
      final byStrength = a.strength.entropyBits.compareTo(
        b.strength.entropyBits,
      );
      if (byStrength != 0) return byStrength;
      return b.count.compareTo(a.count);
    });

    weak.sort((a, b) => a.title.toLowerCase().compareTo(b.title.toLowerCase()));
    withoutPassword.sort(
      (a, b) => a.title.toLowerCase().compareTo(b.title.toLowerCase()),
    );

    return VaultHealthReport(
      entryCount: all.length,
      withPassword: withPassword,
      weak: weak,
      reused: reused,
      withoutPassword: withoutPassword,
      reusedTitles: reusedTitles,
    );
  }

  static String _digest(String password, Uint8List key) {
    final mac = HMac(SHA256Digest(), 64)..init(KeyParameter(key));
    final bytes = Uint8List.fromList(utf8.encode(password));
    try {
      final out = Uint8List(mac.macSize);
      mac.update(bytes, 0, bytes.length);
      mac.doFinal(out, 0);
      return base64Url.encode(out);
    } finally {
      VaultCrypto.wipe(bytes);
    }
  }
}
