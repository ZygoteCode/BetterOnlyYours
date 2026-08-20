import 'dart:typed_data';

import 'vault_crypto.dart';
import 'vault_exception.dart';

enum VaultKdfAlgorithm {
  pbkdf2Sha256(1, 'PBKDF2-HMAC-SHA256'),
  argon2id(2, 'Argon2id');

  const VaultKdfAlgorithm(this.id, this.label);

  final int id;
  final String label;

  static VaultKdfAlgorithm? fromId(int id) {
    for (final algorithm in values) {
      if (algorithm.id == id) return algorithm;
    }
    return null;
  }
}

/// Key-derivation settings of a vault file.
///
/// The parameters live in the (authenticated) file header, so a vault can be
/// re-keyed to stronger settings later without breaking older files: every
/// file carries the recipe needed to open it.
sealed class VaultKdfParams {
  const VaultKdfParams();

  /// Size of the encoded parameter block inside the header.
  static const int encodedLength = 9;

  /// What new and re-keyed vaults use.
  ///
  /// Argon2id is memory-hard, so a GPU or ASIC attacker cannot trade memory
  /// for parallelism the way it can with PBKDF2. 64 MiB / t=2 / p=1 costs
  /// roughly one second in the pure-Dart implementation this app ships, which
  /// is the practical ceiling for an interactive unlock.
  static const VaultKdfParams current = Argon2idParams(
    memoryKib: 65536,
    iterations: 2,
    parallelism: 1,
  );

  /// Minimum settings considered acceptable; anything weaker is transparently
  /// upgraded on the next successful unlock. Mirrors the OWASP baseline for
  /// Argon2id (19 MiB, t=2, p=1).
  static const int minimumMemoryKib = 19456;
  static const int minimumIterations = 2;

  VaultKdfAlgorithm get algorithm;

  /// True when this vault already uses at least the current policy.
  bool get meetsCurrentPolicy;

  /// Human-readable description for the security screen.
  String describe();

  /// True when [target] is a strictly stronger recipe than this one, which is
  /// what drives the transparent re-key on unlock. Never reports a downgrade
  /// as an upgrade.
  bool isWeakerThan(VaultKdfParams target) {
    if (algorithm == target.algorithm) {
      return switch ((this, target)) {
        (Pbkdf2Params a, Pbkdf2Params b) => a.iterations < b.iterations,
        (Argon2idParams a, Argon2idParams b) =>
          a.memoryKib < b.memoryKib || a.iterations < b.iterations,
        _ => false,
      };
    }
    // Argon2id supersedes PBKDF2; the reverse is not an upgrade.
    return target.algorithm == VaultKdfAlgorithm.argon2id;
  }

  Uint8List encode();

  Uint8List deriveKey(
    Uint8List password,
    Uint8List salt, {
    int length = VaultCrypto.keyLength,
  });

  static VaultKdfParams decode(int algorithmId, Uint8List block) {
    if (block.length < encodedLength) {
      throw const VaultException(
        VaultErrorKind.truncated,
        'Key-derivation parameters are incomplete.',
      );
    }
    final data = ByteData.sublistView(block, 0, encodedLength);
    final memoryKib = data.getUint32(0);
    final iterations = data.getUint32(4);
    final parallelism = data.getUint8(8);

    final algorithm = VaultKdfAlgorithm.fromId(algorithmId);
    switch (algorithm) {
      case VaultKdfAlgorithm.pbkdf2Sha256:
        return Pbkdf2Params(iterations: iterations);
      case VaultKdfAlgorithm.argon2id:
        return Argon2idParams(
          memoryKib: memoryKib,
          iterations: iterations,
          parallelism: parallelism,
        );
      case null:
        throw VaultException(
          VaultErrorKind.unsupportedVersion,
          'Unknown key derivation function id $algorithmId.',
        );
    }
  }

  static Uint8List _encodeBlock({
    required int memoryKib,
    required int iterations,
    required int parallelism,
  }) {
    final block = Uint8List(encodedLength);
    final data = ByteData.sublistView(block);
    data.setUint32(0, memoryKib);
    data.setUint32(4, iterations);
    data.setUint8(8, parallelism);
    return block;
  }
}

final class Pbkdf2Params extends VaultKdfParams {
  const Pbkdf2Params({required this.iterations});

  /// Parameters of the original v1 vault layout.
  static const Pbkdf2Params legacyV1 = Pbkdf2Params(iterations: 3000);

  static const int minIterations = 1000;
  static const int maxIterations = 20000000;

  final int iterations;

  @override
  VaultKdfAlgorithm get algorithm => VaultKdfAlgorithm.pbkdf2Sha256;

  /// PBKDF2 is never at policy any more: it is not memory-hard.
  @override
  bool get meetsCurrentPolicy => false;

  @override
  String describe() =>
      '${algorithm.label} · $iterations iterations · 16-byte salt';

  @override
  Uint8List encode() => VaultKdfParams._encodeBlock(
    memoryKib: 0,
    iterations: iterations,
    parallelism: 0,
  );

  @override
  Uint8List deriveKey(
    Uint8List password,
    Uint8List salt, {
    int length = VaultCrypto.keyLength,
  }) {
    _validate();
    return VaultCrypto.deriveKeyPbkdf2(
      password: password,
      salt: salt,
      iterations: iterations,
      length: length,
    );
  }

  void _validate() {
    if (iterations < minIterations || iterations > maxIterations) {
      throw VaultException(
        VaultErrorKind.malformedPayload,
        'Implausible PBKDF2 iteration count $iterations in header.',
      );
    }
  }
}

final class Argon2idParams extends VaultKdfParams {
  const Argon2idParams({
    required this.memoryKib,
    required this.iterations,
    required this.parallelism,
  });

  /// Guards against a tampered header asking for an absurd allocation.
  static const int minMemoryKib = 8;
  static const int maxMemoryKib = 1048576; // 1 GiB
  static const int maxIterations = 64;
  static const int maxParallelism = 16;

  final int memoryKib;
  final int iterations;
  final int parallelism;

  @override
  VaultKdfAlgorithm get algorithm => VaultKdfAlgorithm.argon2id;

  @override
  bool get meetsCurrentPolicy =>
      memoryKib >= VaultKdfParams.minimumMemoryKib &&
      iterations >= VaultKdfParams.minimumIterations &&
      parallelism >= 1;

  @override
  String describe() =>
      '${algorithm.label} · ${(memoryKib / 1024).round()} MiB memory · '
      't=$iterations · p=$parallelism · 16-byte salt';

  @override
  Uint8List encode() => VaultKdfParams._encodeBlock(
    memoryKib: memoryKib,
    iterations: iterations,
    parallelism: parallelism,
  );

  @override
  Uint8List deriveKey(
    Uint8List password,
    Uint8List salt, {
    int length = VaultCrypto.keyLength,
  }) {
    _validate();
    return VaultCrypto.deriveKeyArgon2id(
      password: password,
      salt: salt,
      memoryKib: memoryKib,
      iterations: iterations,
      parallelism: parallelism,
      length: length,
    );
  }

  void _validate() {
    final valid =
        memoryKib >= minMemoryKib &&
        memoryKib <= maxMemoryKib &&
        iterations >= 1 &&
        iterations <= maxIterations &&
        parallelism >= 1 &&
        parallelism <= maxParallelism;
    if (!valid) {
      throw VaultException(
        VaultErrorKind.malformedPayload,
        'Implausible Argon2id parameters in header '
        '(m=$memoryKib, t=$iterations, p=$parallelism).',
      );
    }
  }
}
