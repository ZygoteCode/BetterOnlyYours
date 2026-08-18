import 'dart:convert';
import 'dart:typed_data';

import 'vault_crypto.dart';
import 'vault_exception.dart';

/// Key derivation function identifiers written into the v2 header.
class VaultKdf {
  const VaultKdf._();

  static const int pbkdf2Sha256 = 1;
}

/// Parsed vault file header plus the exact header bytes, which are also the
/// AEAD associated data (so header tampering breaks decryption).
class VaultHeader {
  const VaultHeader({
    required this.version,
    required this.kdfId,
    required this.iterations,
    required this.salt,
    required this.nonce,
    required this.bytes,
  });

  final int version;
  final int kdfId;
  final int iterations;
  final Uint8List salt;
  final Uint8List nonce;
  final Uint8List bytes;

  bool get isLegacy => version == VaultFileCodec.legacyVersion;
}

/// Binary layout of `credentials.plf`.
///
/// v1 (written by early builds, still readable):
///   magic[4] | version=1 | salt[16] | nonce[12] | ciphertext+tag
///   PBKDF2-HMAC-SHA256, fixed 3 000 iterations.
///
/// v2 (written today):
///   magic[4] | version=2 | kdf[1] | iterations[4, big endian] |
///   salt[16] | nonce[12] | ciphertext+tag
///
/// In both versions the full header is authenticated as GCM associated data.
class VaultFileCodec {
  const VaultFileCodec._();

  static final Uint8List magic = Uint8List.fromList(<int>[
    0xFF,
    0xFE,
    0x0D,
    0x0A,
  ]);

  static const int legacyVersion = 1;
  static const int currentVersion = 2;

  /// Iteration count baked into v1 files. Kept only to read old vaults.
  static const int legacyIterations = 3000;

  /// Iteration count used for every new or re-saved vault.
  static const int currentIterations = 200000;

  static const int _legacyHeaderLength =
      4 + 1 + VaultCrypto.saltLength + VaultCrypto.nonceLength;
  static const int _v2HeaderLength =
      4 + 1 + 1 + 4 + VaultCrypto.saltLength + VaultCrypto.nonceLength;

  static VaultHeader parseHeader(Uint8List raw) {
    if (raw.length < 5) {
      throw const VaultException(
        VaultErrorKind.truncated,
        'File is shorter than a vault header.',
      );
    }
    if (!VaultCrypto.constantTimeEquals(
      Uint8List.sublistView(raw, 0, 4),
      magic,
    )) {
      throw const VaultException(
        VaultErrorKind.badSignature,
        'Magic bytes do not match the vault signature.',
      );
    }

    final version = raw[4];
    switch (version) {
      case legacyVersion:
        if (raw.length <= _legacyHeaderLength) {
          throw const VaultException(
            VaultErrorKind.truncated,
            'Legacy vault header is incomplete.',
          );
        }
        return VaultHeader(
          version: version,
          kdfId: VaultKdf.pbkdf2Sha256,
          iterations: legacyIterations,
          salt: Uint8List.sublistView(raw, 5, 5 + VaultCrypto.saltLength),
          nonce: Uint8List.sublistView(
            raw,
            5 + VaultCrypto.saltLength,
            _legacyHeaderLength,
          ),
          bytes: Uint8List.sublistView(raw, 0, _legacyHeaderLength),
        );
      case currentVersion:
        if (raw.length <= _v2HeaderLength) {
          throw const VaultException(
            VaultErrorKind.truncated,
            'Vault header is incomplete.',
          );
        }
        final kdfId = raw[5];
        if (kdfId != VaultKdf.pbkdf2Sha256) {
          throw VaultException(
            VaultErrorKind.unsupportedVersion,
            'Unknown key derivation function id $kdfId.',
          );
        }
        final iterations = ByteData.sublistView(raw, 6, 10).getUint32(0);
        if (iterations < 1000 || iterations > 20000000) {
          throw VaultException(
            VaultErrorKind.malformedPayload,
            'Implausible iteration count $iterations in header.',
          );
        }
        const saltStart = 10;
        const nonceStart = saltStart + VaultCrypto.saltLength;
        return VaultHeader(
          version: version,
          kdfId: kdfId,
          iterations: iterations,
          salt: Uint8List.sublistView(raw, saltStart, nonceStart),
          nonce: Uint8List.sublistView(raw, nonceStart, _v2HeaderLength),
          bytes: Uint8List.sublistView(raw, 0, _v2HeaderLength),
        );
      default:
        throw VaultException(
          VaultErrorKind.unsupportedVersion,
          'Vault format version $version is not supported.',
        );
    }
  }

  static Uint8List buildHeader({
    required Uint8List salt,
    required Uint8List nonce,
    required int iterations,
  }) {
    final header = Uint8List(_v2HeaderLength);
    header.setAll(0, magic);
    header[4] = currentVersion;
    header[5] = VaultKdf.pbkdf2Sha256;
    ByteData.sublistView(header, 6, 10).setUint32(0, iterations);
    header.setAll(10, salt);
    header.setAll(10 + VaultCrypto.saltLength, nonce);
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
