import 'dart:convert';
import 'dart:io';
import 'dart:isolate';
import 'dart:typed_data';

import '../storage/app_paths.dart';
import 'vault_crypto.dart';
import 'vault_exception.dart';
import 'vault_file.dart';
import 'vault_kdf.dart';
import 'vault_session.dart';

class VaultOpenResult {
  const VaultOpenResult({
    required this.session,
    required this.entries,
    this.upgradedFormat = false,
    this.recoveredFromBackup = false,
  });

  final VaultSession session;
  final Map<String, String> entries;

  /// The file used an older format or weaker key-derivation settings and has
  /// been re-encrypted with the current parameters.
  final bool upgradedFormat;

  /// The primary file was unusable and the `.bak` copy was opened instead.
  final bool recoveredFromBackup;
}

class VaultFileInfo {
  const VaultFileInfo({
    required this.path,
    required this.exists,
    this.sizeBytes = 0,
    this.modifiedAt,
    this.formatVersion,
    this.kdf,
    this.backupExists = false,
    this.headerError,
  });

  final String path;
  final bool exists;
  final int sizeBytes;
  final DateTime? modifiedAt;
  final int? formatVersion;
  final VaultKdfParams? kdf;
  final bool backupExists;
  final String? headerError;

  String get directory {
    final separator = path.lastIndexOf(Platform.pathSeparator);
    return separator <= 0 ? path : path.substring(0, separator);
  }
}

/// Storage contract for the vault. [VaultRepository] is the real, file-backed
/// implementation; tests substitute an in-memory one.
abstract interface class VaultStore {
  Future<String> resolvePath();

  Future<bool> exists();

  Future<VaultFileInfo> describe();

  Future<VaultSession> create(String password, {Map<String, String> entries});

  Future<VaultOpenResult> open(String password);

  Future<void> save(Map<String, String> entries, VaultSession session);

  Future<VaultSession> reKey(String newPassword, Map<String, String> entries);

  Future<bool> verifyPassword(String password, VaultSession session);
}

/// Reads and writes the encrypted vault file.
///
/// All key derivation runs on a background isolate so the UI never stalls on
/// Argon2id. Writes are atomic (temp file + rename) and keep a `.bak` copy of
/// the previous good file.
class VaultRepository implements VaultStore {
  VaultRepository({
    String? path,
    bool deriveOnIsolate = true,
    VaultKdfParams? kdfOverride,
  }) : _explicitPath = path,
       _deriveOnIsolate = deriveOnIsolate,
       _kdf = kdfOverride ?? VaultKdfParams.current;

  final String? _explicitPath;
  final bool _deriveOnIsolate;

  /// Settings used for new vaults and for re-keying. Existing files keep the
  /// parameters recorded in their own header until they are upgraded.
  final VaultKdfParams _kdf;

  String? _resolvedPath;

  @override
  Future<String> resolvePath() async {
    final explicit = _explicitPath;
    if (explicit != null) return explicit;
    return _resolvedPath ??= AppPaths.vaultPath();
  }

  @override
  Future<bool> exists() async => File(await resolvePath()).exists();

  @override
  Future<VaultFileInfo> describe() async {
    final path = await resolvePath();
    final file = File(path);
    final backupExists = File('$path.bak').existsSync();
    if (!file.existsSync()) {
      return VaultFileInfo(
        path: path,
        exists: false,
        backupExists: backupExists,
      );
    }

    final stat = file.statSync();
    int? version;
    VaultKdfParams? kdf;
    String? headerError;
    try {
      final raf = file.openSync();
      try {
        // One byte more than the header, so the codec sees a payload behind it.
        final headerBytes = raf.readSync(VaultFileCodec.maxHeaderLength + 1);
        final header = VaultFileCodec.parseHeader(headerBytes);
        version = header.version;
        kdf = header.kdf;
      } finally {
        raf.closeSync();
      }
    } on VaultException catch (error) {
      headerError = error.title;
    } catch (error) {
      headerError = 'Vault header could not be read ($error)';
    }

    return VaultFileInfo(
      path: path,
      exists: true,
      sizeBytes: stat.size,
      modifiedAt: stat.modified,
      formatVersion: version,
      kdf: kdf,
      backupExists: backupExists,
      headerError: headerError,
    );
  }

  /// Creates a brand-new vault containing [entries] (usually empty).
  @override
  Future<VaultSession> create(
    String password, {
    Map<String, String> entries = const <String, String>{},
  }) async {
    final salt = VaultCrypto.randomBytes(VaultCrypto.saltLength);
    final key = await _deriveKey(password, salt, _kdf);
    final session = VaultSession(
      key: key,
      salt: salt,
      kdf: _kdf,
      fileVersion: VaultFileCodec.currentVersion,
    );
    await save(entries, session);
    return session;
  }

  @override
  Future<VaultOpenResult> open(String password) async {
    final path = await resolvePath();
    final file = File(path);

    if (!file.existsSync()) {
      final backup = File('$path.bak');
      if (!backup.existsSync()) {
        throw VaultException(
          VaultErrorKind.notFound,
          'No vault file at $path.',
        );
      }
      final result = await _openBytes(await _read(backup), password);
      return VaultOpenResult(
        session: result.session,
        entries: result.entries,
        upgradedFormat: result.upgradedFormat,
        recoveredFromBackup: true,
      );
    }

    VaultOpenResult result;
    try {
      result = await _openBytes(await _read(file), password);
    } on VaultException catch (error) {
      final recoverable =
          error.kind == VaultErrorKind.badSignature ||
          error.kind == VaultErrorKind.truncated ||
          error.kind == VaultErrorKind.malformedPayload;
      final backup = File('$path.bak');
      if (!recoverable || !backup.existsSync()) rethrow;

      final recovered = await _openBytes(await _read(backup), password);
      result = VaultOpenResult(
        session: recovered.session,
        entries: recovered.entries,
        upgradedFormat: recovered.upgradedFormat,
        recoveredFromBackup: true,
      );
    }

    if (result.session.kdf.isWeakerThan(_kdf)) {
      // Vaults written before Argon2id (or with weaker settings than today's
      // policy) are re-keyed transparently now that the password is at hand.
      try {
        final upgraded = await reKey(password, result.entries);
        result.session.dispose();
        return VaultOpenResult(
          session: upgraded,
          entries: result.entries,
          upgradedFormat: true,
          recoveredFromBackup: result.recoveredFromBackup,
        );
      } catch (_) {
        // Upgrade is best effort: an unwritable folder must not block unlock.
      }
    } else if (result.recoveredFromBackup) {
      // Restore the primary file from the backup we just used.
      try {
        await save(result.entries, result.session);
      } catch (_) {
        // Non-fatal: the user can still work from the recovered contents.
      }
    }

    return result;
  }

  Future<VaultOpenResult> _openBytes(Uint8List raw, String password) async {
    final header = VaultFileCodec.parseHeader(raw);
    final ciphertext = VaultFileCodec.ciphertextOf(raw, header);
    final key = await _deriveKey(
      password,
      Uint8List.fromList(header.salt),
      header.kdf,
    );

    Uint8List plaintext;
    try {
      plaintext = VaultCrypto.aesGcm(
        forEncryption: false,
        key: key,
        nonce: Uint8List.fromList(header.nonce),
        input: Uint8List.fromList(ciphertext),
        aad: Uint8List.fromList(header.bytes),
      );
    } catch (error) {
      VaultCrypto.wipe(key);
      throw VaultException(
        VaultErrorKind.invalidPassword,
        'Authenticated decryption failed.',
        cause: error,
      );
    }

    try {
      final entries = VaultFileCodec.decodePayload(plaintext);
      return VaultOpenResult(
        session: VaultSession(
          key: key,
          salt: Uint8List.fromList(header.salt),
          kdf: header.kdf,
          fileVersion: header.version,
        ),
        entries: entries,
      );
    } catch (_) {
      VaultCrypto.wipe(key);
      rethrow;
    } finally {
      VaultCrypto.wipe(plaintext);
    }
  }

  /// Encrypts and writes [entries] using an already unlocked [session].
  @override
  Future<void> save(Map<String, String> entries, VaultSession session) async {
    if (session.isDisposed) {
      throw const VaultException(
        VaultErrorKind.ioFailure,
        'Vault session is closed.',
      );
    }

    final nonce = VaultCrypto.randomBytes(VaultCrypto.nonceLength);
    final header = VaultFileCodec.buildHeader(
      salt: session.salt,
      nonce: nonce,
      kdf: session.kdf,
    );
    final plaintext = VaultFileCodec.encodePayload(entries);

    try {
      final ciphertext = VaultCrypto.aesGcm(
        forEncryption: true,
        key: session.key,
        nonce: nonce,
        input: plaintext,
        aad: header,
      );
      await _writeAtomically(
        await resolvePath(),
        VaultCrypto.concat(<Uint8List>[header, ciphertext]),
      );
    } finally {
      VaultCrypto.wipe(plaintext);
    }
  }

  /// Re-encrypts the vault under a new master password and returns the new
  /// session. The previous session stays valid until the caller disposes it.
  @override
  Future<VaultSession> reKey(
    String newPassword,
    Map<String, String> entries,
  ) async {
    final salt = VaultCrypto.randomBytes(VaultCrypto.saltLength);
    final key = await _deriveKey(newPassword, salt, _kdf);
    final session = VaultSession(
      key: key,
      salt: salt,
      kdf: _kdf,
      fileVersion: VaultFileCodec.currentVersion,
    );
    await save(entries, session);
    return session;
  }

  /// Verifies [password] against an unlocked [session] without touching disk.
  @override
  Future<bool> verifyPassword(String password, VaultSession session) async {
    final candidate = await _deriveKey(
      password,
      Uint8List.fromList(session.salt),
      session.kdf,
    );
    try {
      return session.matchesKey(candidate);
    } finally {
      VaultCrypto.wipe(candidate);
    }
  }

  Future<Uint8List> _read(File file) async {
    try {
      return await file.readAsBytes();
    } on FileSystemException catch (error) {
      throw _mapFileSystemException(error, 'read');
    }
  }

  /// Key derivation is deliberately expensive, so it runs off the UI isolate.
  Future<Uint8List> _deriveKey(
    String password,
    Uint8List salt,
    VaultKdfParams kdf,
  ) async {
    final passwordBytes = Uint8List.fromList(utf8.encode(password));
    try {
      if (!_deriveOnIsolate) {
        return kdf.deriveKey(passwordBytes, salt);
      }
      return await Isolate.run(() => kdf.deriveKey(passwordBytes, salt));
    } finally {
      VaultCrypto.wipe(passwordBytes);
    }
  }

  Future<void> _writeAtomically(String path, Uint8List data) async {
    final target = File(path);
    final temp = File('$path.tmp');
    final backup = File('$path.bak');

    try {
      final parent = target.parent;
      if (!parent.existsSync()) {
        await parent.create(recursive: true);
      }

      final handle = await temp.open(mode: FileMode.writeOnly);
      try {
        await handle.writeFrom(data);
        await handle.flush();
      } finally {
        await handle.close();
      }

      if (target.existsSync()) {
        if (backup.existsSync()) {
          await backup.delete();
        }
        await target.copy(backup.path);
      }

      // rename() replaces the destination atomically on Windows and POSIX,
      // so a crash mid-save can never leave a half-written vault.
      await temp.rename(path);
    } on FileSystemException catch (error) {
      if (temp.existsSync()) {
        try {
          await temp.delete();
        } catch (_) {
          // Ignore: the failure to report is the original one.
        }
      }
      throw _mapFileSystemException(error, 'write');
    }
  }

  VaultException _mapFileSystemException(
    FileSystemException error,
    String action,
  ) {
    const accessDeniedWindows = 5;
    const accessDeniedPosix = 13;
    final code = error.osError?.errorCode;
    if (code == accessDeniedWindows || code == accessDeniedPosix) {
      return VaultException(
        VaultErrorKind.permissionDenied,
        'Permission denied while trying to $action the vault file.',
        cause: error,
      );
    }
    return VaultException(
      VaultErrorKind.ioFailure,
      'Could not $action the vault file: ${error.osError?.message ?? error.message}',
      cause: error,
    );
  }
}
