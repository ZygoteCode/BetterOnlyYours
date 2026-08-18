import 'dart:typed_data';

import 'vault_crypto.dart';

/// Everything needed to re-encrypt the vault while it is unlocked.
///
/// Deliberately holds the derived key rather than the master password: the
/// salt is stable for the lifetime of a vault file, so saving only needs a
/// fresh nonce. The plaintext password is discarded as soon as unlocking or
/// re-keying completes.
class VaultSession {
  VaultSession({
    required Uint8List key,
    required Uint8List salt,
    required this.iterations,
    required this.fileVersion,
  }) : _key = key,
       _salt = salt;

  final Uint8List _key;
  final Uint8List _salt;
  final int iterations;

  /// Format version of the file this session was opened from.
  final int fileVersion;

  bool _disposed = false;

  bool get isDisposed => _disposed;
  Uint8List get key => _key;
  Uint8List get salt => _salt;

  /// Constant-time check that [candidateKey] was derived from the same
  /// password/salt pair. Used to verify the current master password without
  /// keeping it in memory.
  bool matchesKey(Uint8List candidateKey) =>
      VaultCrypto.constantTimeEquals(_key, candidateKey);

  void dispose() {
    if (_disposed) return;
    _disposed = true;
    VaultCrypto.wipe(_key);
    VaultCrypto.wipe(_salt);
  }
}
