import 'dart:math';
import 'dart:typed_data';

import 'package:pointycastle/export.dart';

/// Low-level cryptographic primitives used by the vault.
///
/// Nothing here is home-grown: key derivation is PBKDF2-HMAC-SHA256 and
/// confidentiality/integrity come from AES-256-GCM, both from PointyCastle.
class VaultCrypto {
  const VaultCrypto._();

  static final Random _random = Random.secure();

  static const int keyLength = 32; // AES-256
  static const int saltLength = 16;
  static const int nonceLength = 12; // GCM standard
  static const int tagLengthBits = 128;

  static Uint8List randomBytes(int length) {
    final bytes = Uint8List(length);
    for (var i = 0; i < length; i++) {
      bytes[i] = _random.nextInt(256);
    }
    return bytes;
  }

  static Uint8List deriveKey({
    required Uint8List password,
    required Uint8List salt,
    required int iterations,
    int length = keyLength,
  }) {
    final derivator = PBKDF2KeyDerivator(HMac(SHA256Digest(), 64))
      ..init(Pbkdf2Parameters(salt, iterations, length));
    return derivator.process(password);
  }

  static Uint8List aesGcm({
    required bool forEncryption,
    required Uint8List key,
    required Uint8List nonce,
    required Uint8List input,
    required Uint8List aad,
  }) {
    final cipher = GCMBlockCipher(AESEngine())
      ..init(
        forEncryption,
        AEADParameters(KeyParameter(key), tagLengthBits, nonce, aad),
      );
    return cipher.process(input);
  }

  static Uint8List concat(List<Uint8List> parts) {
    final total = parts.fold<int>(0, (sum, part) => sum + part.length);
    final result = Uint8List(total);
    var offset = 0;
    for (final part in parts) {
      result.setAll(offset, part);
      offset += part.length;
    }
    return result;
  }

  static bool constantTimeEquals(Uint8List a, Uint8List b) {
    if (a.length != b.length) return false;
    var diff = 0;
    for (var i = 0; i < a.length; i++) {
      diff |= a[i] ^ b[i];
    }
    return diff == 0;
  }

  /// Best-effort zeroing of a byte buffer. Dart cannot guarantee the value was
  /// not copied by the GC, but this bounds how long key material stays
  /// readable in the original buffer.
  static void wipe(Uint8List? data) {
    if (data == null) return;
    data.fillRange(0, data.length, 0);
  }
}
