import 'dart:convert';
import 'dart:math';
import 'dart:typed_data';

import 'package:pointycastle/export.dart';

/// Low-level cryptographic primitives used by the vault.
///
/// Nothing here is home-grown: key derivation is Argon2id (PBKDF2-HMAC-SHA256
/// for vaults written by older builds) and confidentiality/integrity come from
/// AES-256-GCM, all from PointyCastle.
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

  static Uint8List deriveKeyPbkdf2({
    required Uint8List password,
    required Uint8List salt,
    required int iterations,
    int length = keyLength,
  }) {
    final derivator = PBKDF2KeyDerivator(HMac(SHA256Digest(), 64))
      ..init(Pbkdf2Parameters(salt, iterations, length));
    return derivator.process(password);
  }

  /// Argon2id: memory-hard, so an attacker cannot trade memory for
  /// parallelism the way they can against PBKDF2.
  static Uint8List deriveKeyArgon2id({
    required Uint8List password,
    required Uint8List salt,
    required int memoryKib,
    required int iterations,
    required int parallelism,
    int length = keyLength,
  }) {
    final generator = Argon2BytesGenerator()
      ..init(
        Argon2Parameters(
          Argon2Parameters.ARGON2_id,
          salt,
          desiredKeyLength: length,
          memory: memoryKib,
          iterations: iterations,
          lanes: parallelism,
          version: Argon2Parameters.ARGON2_VERSION_13,
        ),
      );
    final out = Uint8List(length);
    generator.deriveKey(password, 0, out, 0);
    return out;
  }

  /// HKDF-SHA256 (RFC 5869): turns one high-entropy key into as many
  /// independent sub-keys as needed.
  ///
  /// Used to give every sealed secret its own key, so that two secrets
  /// protected by the same vault key never share key material and a leaked
  /// sub-key says nothing about the others.
  static Uint8List hkdfSha256({
    required Uint8List key,
    required Uint8List salt,
    required String info,
    int length = keyLength,
  }) {
    final derivator = HKDFKeyDerivator(SHA256Digest())
      ..init(
        HkdfParameters(
          key,
          length,
          salt,
          Uint8List.fromList(utf8.encode(info)),
        ),
      );
    return derivator.process(Uint8List(0));
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
