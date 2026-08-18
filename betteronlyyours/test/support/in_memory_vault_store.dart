import 'dart:typed_data';

import 'package:betteronlyyours/core/security/vault_crypto.dart';
import 'package:betteronlyyours/core/security/vault_exception.dart';
import 'package:betteronlyyours/core/security/vault_file.dart';
import 'package:betteronlyyours/core/security/vault_repository.dart';
import 'package:betteronlyyours/core/security/vault_session.dart';

/// Vault storage that lives in memory.
///
/// Widget tests run inside a fake-async zone where real file I/O can never
/// complete; this keeps the controller's behaviour intact while every
/// operation resolves in a microtask. Encryption itself is covered by the
/// file-backed repository tests.
class InMemoryVaultStore implements VaultStore {
  InMemoryVaultStore({this.path = 'memory://credentials.plf'});

  final String path;

  Map<String, String>? _entries;
  String? _password;
  int writeCount = 0;

  /// Set to a failure to make the next write fail, exercising error paths.
  VaultException? failNextWrite;

  bool get hasVault => _entries != null;

  Map<String, String> get contents =>
      Map<String, String>.unmodifiable(_entries ?? const <String, String>{});

  /// Seeds an existing vault without going through the UI.
  void seed(String password, Map<String, String> entries) {
    _password = password;
    _entries = Map<String, String>.from(entries);
  }

  @override
  Future<String> resolvePath() async => path;

  @override
  Future<bool> exists() async => hasVault;

  @override
  Future<VaultFileInfo> describe() async => VaultFileInfo(
    path: path,
    exists: hasVault,
    sizeBytes: hasVault ? 512 : 0,
    modifiedAt: hasVault ? DateTime.fromMillisecondsSinceEpoch(0) : null,
    formatVersion: hasVault ? VaultFileCodec.currentVersion : null,
    iterations: hasVault ? VaultFileCodec.currentIterations : null,
  );

  @override
  Future<VaultSession> create(
    String password, {
    Map<String, String> entries = const <String, String>{},
  }) async {
    _password = password;
    _entries = Map<String, String>.from(entries);
    writeCount++;
    return _session();
  }

  @override
  Future<VaultOpenResult> open(String password) async {
    final entries = _entries;
    if (entries == null) {
      throw const VaultException(
        VaultErrorKind.notFound,
        'No vault in this store.',
      );
    }
    if (password != _password) {
      throw const VaultException(
        VaultErrorKind.invalidPassword,
        'Authenticated decryption failed.',
      );
    }
    return VaultOpenResult(
      session: _session(),
      entries: Map<String, String>.from(entries),
    );
  }

  @override
  Future<void> save(Map<String, String> entries, VaultSession session) async {
    final failure = failNextWrite;
    if (failure != null) {
      failNextWrite = null;
      throw failure;
    }
    _entries = Map<String, String>.from(entries);
    writeCount++;
  }

  @override
  Future<VaultSession> reKey(
    String newPassword,
    Map<String, String> entries,
  ) async {
    _password = newPassword;
    _entries = Map<String, String>.from(entries);
    writeCount++;
    return _session();
  }

  @override
  Future<bool> verifyPassword(String password, VaultSession session) async =>
      password == _password;

  VaultSession _session() => VaultSession(
    key: Uint8List(VaultCrypto.keyLength),
    salt: Uint8List(VaultCrypto.saltLength),
    iterations: VaultFileCodec.currentIterations,
    fileVersion: VaultFileCodec.currentVersion,
  );
}
