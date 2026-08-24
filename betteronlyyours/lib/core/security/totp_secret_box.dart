import 'dart:convert';
import 'dart:typed_data';

import 'totp.dart';
import 'vault_crypto.dart';

/// A TOTP secret as it is stored inside an entry.
///
/// The vault file is already encrypted end to end; this is a second,
/// independent envelope around the one secret the app deliberately never shows
/// again. It exists so that:
///
///  * the secret is not sitting in the decrypted entry map while the vault is
///    unlocked — it is only unsealed for the milliseconds it takes to compute
///    a code, then wiped;
///  * every entry has its own key, derived with HKDF from a vault-wide random
///    content key, so one recovered sub-key reveals nothing about the others;
///  * the stored text is opaque: it says "there is a token here" and nothing
///    else — not the issuer, not the algorithm, not the digit count.
///
/// The content key lives in the vault metadata, which is inside the encrypted
/// payload. Someone holding the master password can still reach the secret —
/// no local app can prevent that and pretending otherwise would be theatre —
/// but nothing short of the master password does, and no code path in the app
/// hands the secret back to the interface.
class TotpSecretBox {
  const TotpSecretBox._();

  /// Marker and version of the envelope format.
  static const String prefix = 'BOYTOTP1:';

  /// Bound into the AEAD as additional data, so a box cannot be replayed into
  /// a different field or a different envelope version.
  static const String _aad = 'betteronlyyours/totp/v1';

  static const String _hkdfInfo = 'betteronlyyours/totp/v1/entry-key';

  static const int saltLength = VaultCrypto.saltLength;

  /// Length of the vault-wide content key that protects every box.
  static const int contentKeyLength = VaultCrypto.keyLength;

  static bool looksSealed(String? raw) =>
      raw != null && raw.startsWith(prefix) && raw.length > prefix.length;

  static Uint8List newContentKey() => VaultCrypto.randomBytes(contentKeyLength);

  /// Encrypts [secret] and [config] under a fresh per-box key.
  ///
  /// [secret] belongs to the caller and is not wiped here: the caller knows
  /// whether it still needs it (the setup dialog previews a code first).
  static String seal({
    required Uint8List secret,
    required TotpConfig config,
    required Uint8List contentKey,
  }) {
    _requireContentKey(contentKey);
    if (secret.isEmpty) {
      throw const TotpSealException('The secret is empty.');
    }

    final salt = VaultCrypto.randomBytes(saltLength);
    final nonce = VaultCrypto.randomBytes(VaultCrypto.nonceLength);
    final key = _entryKey(contentKey, salt);
    final payload = Uint8List.fromList(
      utf8.encode(
        jsonEncode(<String, dynamic>{
          's': base64.encode(secret),
          ...config.normalized().toJson(),
        }),
      ),
    );

    try {
      final sealed = VaultCrypto.aesGcm(
        forEncryption: true,
        key: key,
        nonce: nonce,
        input: payload,
        aad: _aadBytes,
      );
      return '$prefix${base64Url.encode(salt)}'
          '.${base64Url.encode(nonce)}'
          '.${base64Url.encode(sealed)}';
    } finally {
      VaultCrypto.wipe(key);
      VaultCrypto.wipe(payload);
    }
  }

  /// Opens a box. The returned material owns a live copy of the secret and
  /// must be disposed by the caller.
  static TotpMaterial open(String raw, Uint8List contentKey) {
    _requireContentKey(contentKey);
    if (!looksSealed(raw)) {
      throw const TotpSealException('Not a sealed token.');
    }

    final parts = raw.substring(prefix.length).split('.');
    if (parts.length != 3) {
      throw const TotpSealException('The stored token is malformed.');
    }

    late final Uint8List salt;
    late final Uint8List nonce;
    late final Uint8List sealed;
    try {
      salt = base64Url.decode(parts[0]);
      nonce = base64Url.decode(parts[1]);
      sealed = base64Url.decode(parts[2]);
    } on FormatException {
      throw const TotpSealException('The stored token is malformed.');
    }
    if (salt.length != saltLength || nonce.length != VaultCrypto.nonceLength) {
      throw const TotpSealException('The stored token is malformed.');
    }

    final key = _entryKey(contentKey, salt);
    Uint8List? plaintext;
    try {
      plaintext = VaultCrypto.aesGcm(
        forEncryption: false,
        key: key,
        nonce: nonce,
        input: sealed,
        aad: _aadBytes,
      );
      final decoded = jsonDecode(utf8.decode(plaintext));
      if (decoded is! Map<String, dynamic>) {
        throw const TotpSealException('The stored token is malformed.');
      }
      final secretText = decoded['s'];
      if (secretText is! String) {
        throw const TotpSealException('The stored token has no secret.');
      }
      return TotpMaterial(
        secret: base64.decode(secretText),
        config: TotpConfig.fromJson(decoded),
      );
    } on TotpSealException {
      rethrow;
    } catch (_) {
      // A failed tag check, a truncated payload or a wrong content key all
      // end up here and all mean the same thing to the user.
      throw const TotpSealException('This token could not be decrypted.');
    } finally {
      VaultCrypto.wipe(key);
      VaultCrypto.wipe(plaintext);
    }
  }

  /// Non-throwing [open], for widgets that just skip broken tokens.
  static TotpMaterial? tryOpen(String? raw, Uint8List? contentKey) {
    if (raw == null || contentKey == null || !looksSealed(raw)) return null;
    try {
      return open(raw, contentKey);
    } on TotpSealException {
      return null;
    }
  }

  static Uint8List _entryKey(Uint8List contentKey, Uint8List salt) =>
      VaultCrypto.hkdfSha256(key: contentKey, salt: salt, info: _hkdfInfo);

  static void _requireContentKey(Uint8List key) {
    if (key.length != contentKeyLength) {
      throw const TotpSealException('The vault has no usable token key.');
    }
  }

  static final Uint8List _aadBytes = Uint8List.fromList(utf8.encode(_aad));
}

/// A decrypted secret and its settings, alive only as long as the caller keeps
/// it. [dispose] zeroes the secret buffer.
class TotpMaterial {
  TotpMaterial({required this.secret, required this.config});

  final Uint8List secret;
  final TotpConfig config;

  bool _disposed = false;
  bool get isDisposed => _disposed;

  /// Generates the current code and immediately forgets the secret again.
  TotpCode codeThenDispose({DateTime? at}) {
    try {
      return Totp.generate(secret: secret, config: config, at: at);
    } finally {
      dispose();
    }
  }

  void dispose() {
    if (_disposed) return;
    _disposed = true;
    VaultCrypto.wipe(secret);
  }

  /// Never prints the secret, however it is logged.
  @override
  String toString() =>
      'TotpMaterial(${config.algorithm.label}/'
      '${config.effectiveDigits} digits/${config.period}s)';
}

class TotpSealException implements Exception {
  const TotpSealException(this.message);

  final String message;

  @override
  String toString() => 'TotpSealException: $message';
}
