import 'dart:convert';
import 'dart:typed_data';

import 'vault_crypto.dart';
import 'vault_exception.dart';
import 'vault_kdf.dart';

/// Parsed vault file header plus the exact header bytes, which are also the
/// AEAD associated data (so header tampering breaks decryption).
class VaultHeader {
  const VaultHeader({
    required this.version,
    required this.kdf,
    required this.salt,
    required this.nonce,
    required this.bytes,
  });

  final int version;
  final VaultKdfParams kdf;
  final Uint8List salt;
  final Uint8List nonce;
  final Uint8List bytes;

  bool get isCurrentVersion => version == VaultFileCodec.currentVersion;
}

/// Binary layout of `credentials.plf`.
///
/// v1 (written by the first builds, still readable):
///   magic[4] | version=1 | salt[16] | nonce[12] | ciphertext+tag
///   PBKDF2-HMAC-SHA256, fixed 3 000 iterations.
///
/// v2 (PBKDF2 with a stored iteration count, still readable):
///   magic[4] | version=2 | kdf[1] | iterations[4] | salt[16] | nonce[12] |
///   ciphertext+tag
///
/// v3 (written today):
///   magic[4] | version=3 | kdf[1] | params[9] | salt[16] | nonce[12] |
///   ciphertext+tag
///   `params` is memory (KiB, u32) | iterations (u32) | parallelism (u8),
///   which describes Argon2id and PBKDF2 alike.
///
/// In every version the full header is authenticated as GCM associated data.
class VaultFileCodec {
  const VaultFileCodec._();

  static final Uint8List magic = Uint8List.fromList(<int>[
    0xFF,
    0xFE,
    0x0D,
    0x0A,
  ]);

  static const int legacyVersion = 1;
  static const int pbkdf2Version = 2;
  static const int currentVersion = 3;

  static const int _magicLength = 4;
  static const int _v1HeaderLength =
      _magicLength + 1 + VaultCrypto.saltLength + VaultCrypto.nonceLength;
  static const int _v2HeaderLength =
      _magicLength +
      1 +
      1 +
      4 +
      VaultCrypto.saltLength +
      VaultCrypto.nonceLength;
  static const int _v3HeaderLength =
      _magicLength +
      1 +
      1 +
      VaultKdfParams.encodedLength +
      VaultCrypto.saltLength +
      VaultCrypto.nonceLength;

  /// Longest header this codec can produce or read; used when peeking at a
  /// file without decrypting it.
  static const int maxHeaderLength = _v3HeaderLength;

  static VaultHeader parseHeader(Uint8List raw) {
    if (raw.length < 5) {
      throw const VaultException(
        VaultErrorKind.truncated,
        'File is shorter than a vault header.',
      );
    }
    if (!VaultCrypto.constantTimeEquals(
      Uint8List.sublistView(raw, 0, _magicLength),
      magic,
    )) {
      throw const VaultException(
        VaultErrorKind.badSignature,
        'Magic bytes do not match the vault signature.',
      );
    }

    final version = raw[4];
    return switch (version) {
      legacyVersion => _parseV1(raw),
      pbkdf2Version => _parseV2(raw),
      currentVersion => _parseV3(raw),
      _ => throw VaultException(
        VaultErrorKind.unsupportedVersion,
        'Vault format version $version is not supported.',
      ),
    };
  }

  static VaultHeader _parseV1(Uint8List raw) {
    _requireLength(raw, _v1HeaderLength);
    const saltStart = 5;
    const nonceStart = saltStart + VaultCrypto.saltLength;
    return VaultHeader(
      version: legacyVersion,
      kdf: Pbkdf2Params.legacyV1,
      salt: Uint8List.sublistView(raw, saltStart, nonceStart),
      nonce: Uint8List.sublistView(raw, nonceStart, _v1HeaderLength),
      bytes: Uint8List.sublistView(raw, 0, _v1HeaderLength),
    );
  }

  static VaultHeader _parseV2(Uint8List raw) {
    _requireLength(raw, _v2HeaderLength);
    final kdfId = raw[5];
    if (kdfId != VaultKdfAlgorithm.pbkdf2Sha256.id) {
      throw VaultException(
        VaultErrorKind.unsupportedVersion,
        'Unknown key derivation function id $kdfId.',
      );
    }
    final iterations = ByteData.sublistView(raw, 6, 10).getUint32(0);
    const saltStart = 10;
    const nonceStart = saltStart + VaultCrypto.saltLength;
    return VaultHeader(
      version: pbkdf2Version,
      kdf: Pbkdf2Params(iterations: iterations),
      salt: Uint8List.sublistView(raw, saltStart, nonceStart),
      nonce: Uint8List.sublistView(raw, nonceStart, _v2HeaderLength),
      bytes: Uint8List.sublistView(raw, 0, _v2HeaderLength),
    );
  }

  static VaultHeader _parseV3(Uint8List raw) {
    _requireLength(raw, _v3HeaderLength);
    final kdfId = raw[5];
    const paramsStart = 6;
    const saltStart = paramsStart + VaultKdfParams.encodedLength;
    const nonceStart = saltStart + VaultCrypto.saltLength;
    final kdf = VaultKdfParams.decode(
      kdfId,
      Uint8List.sublistView(raw, paramsStart, saltStart),
    );
    return VaultHeader(
      version: currentVersion,
      kdf: kdf,
      salt: Uint8List.sublistView(raw, saltStart, nonceStart),
      nonce: Uint8List.sublistView(raw, nonceStart, _v3HeaderLength),
      bytes: Uint8List.sublistView(raw, 0, _v3HeaderLength),
    );
  }

  static void _requireLength(Uint8List raw, int headerLength) {
    if (raw.length <= headerLength) {
      throw const VaultException(
        VaultErrorKind.truncated,
        'Vault header is incomplete.',
      );
    }
  }

  static Uint8List buildHeader({
    required Uint8List salt,
    required Uint8List nonce,
    required VaultKdfParams kdf,
  }) {
    final header = Uint8List(_v3HeaderLength);
    header.setAll(0, magic);
    header[4] = currentVersion;
    header[5] = kdf.algorithm.id;
    header.setAll(6, kdf.encode());
    header.setAll(6 + VaultKdfParams.encodedLength, salt);
    header.setAll(
      6 + VaultKdfParams.encodedLength + VaultCrypto.saltLength,
      nonce,
    );
    return header;
  }

  static Uint8List ciphertextOf(Uint8List raw, VaultHeader header) {
    final ciphertext = Uint8List.sublistView(raw, header.bytes.length);
    if (ciphertext.isEmpty) {
      throw const VaultException(
        VaultErrorKind.truncated,
        'Vault payload is missing.',
      );
    }
    return ciphertext;
  }

  static Uint8List encodePayload(Map<String, String> entries) {
    return Uint8List.fromList(utf8.encode(jsonEncode(entries)));
  }

  static Map<String, String> decodePayload(Uint8List plaintext) {
    try {
      final decoded = jsonDecode(utf8.decode(plaintext));
      if (decoded is! Map) {
        throw const VaultException(
          VaultErrorKind.malformedPayload,
          'Vault payload is not a JSON object.',
        );
      }
      return <String, String>{
        for (final entry in decoded.entries)
          '${entry.key}': entry.value is String
              ? entry.value as String
              : jsonEncode(entry.value),
      };
    } on VaultException {
      rethrow;
    } catch (error) {
      throw VaultException(
        VaultErrorKind.malformedPayload,
        'Vault payload could not be decoded.',
        cause: error,
      );
    }
  }
}
