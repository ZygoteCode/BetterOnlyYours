import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:betteronlyyours/core/security/vault_crypto.dart';
import 'package:betteronlyyours/core/security/vault_exception.dart';
import 'package:betteronlyyours/core/security/vault_file.dart';
import 'package:betteronlyyours/core/security/vault_kdf.dart';
import 'package:betteronlyyours/core/security/vault_repository.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  late Directory tempDir;
  late String vaultPath;

  setUp(() async {
    tempDir = await Directory.systemTemp.createTemp('boy_vault_test');
    vaultPath = '${tempDir.path}${Platform.pathSeparator}credentials.plf';
  });

  tearDown(() async {
    if (tempDir.existsSync()) {
      await tempDir.delete(recursive: true);
    }
  });

  VaultRepository repository() => VaultRepository(
    path: vaultPath,
    deriveOnIsolate: false,
    kdfOverride: const Pbkdf2Params(iterations: 5000),
  );

  /// Builds a file in the original v1 layout (fixed 3 000 PBKDF2 iterations).
  void writeLegacyVault(Map<String, String> entries, String password) {
    final salt = VaultCrypto.randomBytes(VaultCrypto.saltLength);
    final nonce = VaultCrypto.randomBytes(VaultCrypto.nonceLength);
    final key = VaultCrypto.deriveKeyPbkdf2(
      password: Uint8List.fromList(utf8.encode(password)),
      salt: salt,
      iterations: 3000,
    );
    final header = VaultCrypto.concat(<Uint8List>[
      VaultFileCodec.magic,
      Uint8List.fromList(<int>[VaultFileCodec.legacyVersion]),
      salt,
      nonce,
    ]);
    final ciphertext = VaultCrypto.aesGcm(
      forEncryption: true,
      key: key,
      nonce: nonce,
      input: Uint8List.fromList(
        utf8.encode(const JsonEncoder.withIndent('  ').convert(entries)),
      ),
      aad: header,
    );
    File(vaultPath)
        .writeAsBytesSync(VaultCrypto.concat(<Uint8List>[header, ciphertext]));
  }

  test('creates, saves and re-opens a vault', () async {
    final repo = repository();
    final session = await repo.create(
      'correct horse battery staple',
      entries: <String, String>{'GitHub': 'token'},
    );

    await repo.save(<String, String>{
      'GitHub': 'token',
      'Bank': 'card',
    }, session);

    final opened = await repository().open('correct horse battery staple');
    expect(opened.entries, <String, String>{'GitHub': 'token', 'Bank': 'card'});
    expect(opened.session.fileVersion, VaultFileCodec.currentVersion);
    expect(opened.upgradedFormat, isFalse);
  });

  test('rejects a wrong password with a typed error', () async {
    final repo = repository();
    await repo.create('right password');

    await expectLater(
      repository().open('wrong password'),
      throwsA(
        isA<VaultException>().having(
          (e) => e.kind,
          'kind',
          VaultErrorKind.invalidPassword,
        ),
      ),
    );
  });

  test('reads a legacy v1 vault and upgrades it in place', () async {
    writeLegacyVault(<String, String>{'Old': 'plain text'}, 'legacy pass');

    final result = await repository().open('legacy pass');

    expect(result.entries, <String, String>{'Old': 'plain text'});
    expect(result.upgradedFormat, isTrue);

    final info = await repository().describe();
    expect(info.formatVersion, VaultFileCodec.currentVersion);
    expect(info.kdf, isA<Pbkdf2Params>());
    expect((info.kdf! as Pbkdf2Params).iterations, 5000);

    // The upgraded file still opens with the same password.
    final reopened = await repository().open('legacy pass');
    expect(reopened.entries['Old'], 'plain text');
    expect(reopened.upgradedFormat, isFalse);
  });

  test('a non-vault file is reported as a bad signature', () async {
    File(vaultPath).writeAsBytesSync(Uint8List.fromList(<int>[1, 2, 3, 4, 5]));

    await expectLater(
      repository().open('whatever'),
      throwsA(
        isA<VaultException>().having(
          (e) => e.kind,
          'kind',
          VaultErrorKind.badSignature,
        ),
      ),
    );
  });

  test('missing file reports notFound', () async {
    await expectLater(
      repository().open('whatever'),
      throwsA(
        isA<VaultException>().having(
          (e) => e.kind,
          'kind',
          VaultErrorKind.notFound,
        ),
      ),
    );
  });

  test('falls back to the .bak copy when the vault is corrupted', () async {
    final repo = repository();
    final session = await repo.create(
      'pass',
      entries: <String, String>{'A': '1'},
    );
    // Second save rotates the previous good file into .bak.
    await repo.save(<String, String>{'A': '1', 'B': '2'}, session);

    expect(File('$vaultPath.bak').existsSync(), isTrue);
    File(vaultPath).writeAsBytesSync(Uint8List.fromList(<int>[9, 9, 9, 9, 9]));

    final recovered = await repository().open('pass');
    expect(recovered.recoveredFromBackup, isTrue);
    expect(recovered.entries['A'], '1');
  });

  test('every save uses a new nonce but keeps the salt', () async {
    final repo = repository();
    final session = await repo.create('pass');

    await repo.save(<String, String>{'A': '1'}, session);
    final first = File(vaultPath).readAsBytesSync();
    await repo.save(<String, String>{'A': '2'}, session);
    final second = File(vaultPath).readAsBytesSync();

    final firstHeader = VaultFileCodec.parseHeader(first);
    final secondHeader = VaultFileCodec.parseHeader(second);

    expect(firstHeader.salt, secondHeader.salt);
    expect(firstHeader.nonce, isNot(secondHeader.nonce));
  });

  test('verifyPassword compares against the live session key', () async {
    final repo = repository();
    final session = await repo.create('the right one');

    expect(await repo.verifyPassword('the right one', session), isTrue);
    expect(await repo.verifyPassword('nope', session), isFalse);
  });

  test('re-keying re-encrypts the vault under a new password', () async {
    final repo = repository();
    final session = await repo.create(
      'old pass',
      entries: <String, String>{'A': '1'},
    );

    final next = await repo.reKey('new pass', <String, String>{'A': '1'});
    session.dispose();

    expect((await repository().open('new pass')).entries['A'], '1');
    await expectLater(
      repository().open('old pass'),
      throwsA(isA<VaultException>()),
    );
    next.dispose();
  });

  test('describe reports header facts without decrypting', () async {
    await repository().create('pass', entries: <String, String>{'A': '1'});

    final info = await repository().describe();
    expect(info.exists, isTrue);
    expect(info.formatVersion, VaultFileCodec.currentVersion);
    expect(info.kdf, isA<Pbkdf2Params>());
    expect((info.kdf! as Pbkdf2Params).iterations, 5000);
    expect(info.sizeBytes, greaterThan(0));
    expect(info.headerError, isNull);
  });
}
