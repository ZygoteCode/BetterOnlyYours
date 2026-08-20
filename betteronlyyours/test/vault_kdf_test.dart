import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:betteronlyyours/core/security/vault_crypto.dart';
import 'package:betteronlyyours/core/security/vault_exception.dart';
import 'package:betteronlyyours/core/security/vault_file.dart';
import 'package:betteronlyyours/core/security/vault_kdf.dart';
import 'package:betteronlyyours/core/security/vault_repository.dart';
import 'package:flutter_test/flutter_test.dart';

/// Argon2id key derivation, the v3 file layout and the migration path from the
/// PBKDF2 formats.
void main() {
  // Deliberately tiny so the suite stays fast; production uses 64 MiB / t=2.
  const cheapArgon = Argon2idParams(
    memoryKib: 64,
    iterations: 1,
    parallelism: 1,
  );

  group('parameters', () {
    test('current policy is Argon2id and meets its own minimum', () {
      final current = VaultKdfParams.current;
      expect(current, isA<Argon2idParams>());
      expect(current.algorithm, VaultKdfAlgorithm.argon2id);
      expect(current.meetsCurrentPolicy, isTrue);
      expect(
        (current as Argon2idParams).memoryKib,
        greaterThanOrEqualTo(VaultKdfParams.minimumMemoryKib),
      );
    });

    test('PBKDF2 never satisfies the policy', () {
      expect(
        const Pbkdf2Params(iterations: 600000).meetsCurrentPolicy,
        isFalse,
      );
      expect(Pbkdf2Params.legacyV1.meetsCurrentPolicy, isFalse);
    });

    test('upgrade comparison never downgrades', () {
      const weakArgon = Argon2idParams(
        memoryKib: 1024,
        iterations: 1,
        parallelism: 1,
      );
      const strongArgon = Argon2idParams(
        memoryKib: 65536,
        iterations: 3,
        parallelism: 1,
      );
      const pbkdf2 = Pbkdf2Params(iterations: 200000);

      expect(pbkdf2.isWeakerThan(strongArgon), isTrue);
      expect(weakArgon.isWeakerThan(strongArgon), isTrue);
      expect(strongArgon.isWeakerThan(weakArgon), isFalse);
      expect(strongArgon.isWeakerThan(pbkdf2), isFalse);
      expect(const Pbkdf2Params(iterations: 1000).isWeakerThan(pbkdf2), isTrue);
    });

    test('encode/decode round trip', () {
      for (final params in <VaultKdfParams>[
        const Pbkdf2Params(iterations: 200000),
        const Argon2idParams(memoryKib: 65536, iterations: 2, parallelism: 1),
      ]) {
        final decoded = VaultKdfParams.decode(
          params.algorithm.id,
          params.encode(),
        );
        expect(decoded.runtimeType, params.runtimeType);
        expect(decoded.describe(), params.describe());
      }
    });

    test('absurd parameters from a tampered header are refused', () {
      final block = Uint8List(VaultKdfParams.encodedLength);
      final data = ByteData.sublistView(block);
      data.setUint32(0, 4000000); // ~4 GiB of memory
      data.setUint32(4, 3);
      data.setUint8(8, 1);
      final params = VaultKdfParams.decode(
        VaultKdfAlgorithm.argon2id.id,
        block,
      );

      expect(
        () => params.deriveKey(
          Uint8List.fromList(utf8.encode('x')),
          Uint8List(16),
        ),
        throwsA(
          isA<VaultException>().having(
            (e) => e.kind,
            'kind',
            VaultErrorKind.malformedPayload,
          ),
        ),
      );
    });

    test('unknown algorithm ids are rejected', () {
      expect(
        () =>
            VaultKdfParams.decode(99, Uint8List(VaultKdfParams.encodedLength)),
        throwsA(
          isA<VaultException>().having(
            (e) => e.kind,
            'kind',
            VaultErrorKind.unsupportedVersion,
          ),
        ),
      );
    });
  });

  group('Argon2id derivation', () {
    test('is deterministic for the same inputs', () {
      final salt = Uint8List.fromList(List<int>.generate(16, (i) => i));
      final password = Uint8List.fromList(utf8.encode('correct horse'));

      final first = cheapArgon.deriveKey(password, salt);
      final second = cheapArgon.deriveKey(password, salt);

      expect(first, equals(second));
      expect(first.length, VaultCrypto.keyLength);
    });

    test('changes with the salt, the password and the parameters', () {
      final password = Uint8List.fromList(utf8.encode('correct horse'));
      final saltA = Uint8List.fromList(List<int>.generate(16, (i) => i));
      final saltB = Uint8List.fromList(List<int>.generate(16, (i) => i + 1));

      final base = cheapArgon.deriveKey(password, saltA);
      expect(base, isNot(cheapArgon.deriveKey(password, saltB)));
      expect(
        base,
        isNot(
          cheapArgon.deriveKey(
            Uint8List.fromList(utf8.encode('wrong horse')),
            saltA,
          ),
        ),
      );
      expect(
        base,
        isNot(
          const Argon2idParams(
            memoryKib: 64,
            iterations: 2,
            parallelism: 1,
          ).deriveKey(password, saltA),
        ),
      );
    });
  });

  group('vault files', () {
    late Directory tempDir;
    late String vaultPath;

    setUp(() async {
      tempDir = await Directory.systemTemp.createTemp('boy_kdf_test');
      vaultPath = '${tempDir.path}${Platform.pathSeparator}credentials.plf';
    });

    tearDown(() async {
      if (tempDir.existsSync()) await tempDir.delete(recursive: true);
    });

    VaultRepository repository([VaultKdfParams? kdf]) => VaultRepository(
      path: vaultPath,
      deriveOnIsolate: false,
      kdfOverride: kdf ?? cheapArgon,
    );

    void writeLegacyV1(Map<String, String> entries, String password) {
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
        input: VaultFileCodec.encodePayload(entries),
        aad: header,
      );
      File(
        vaultPath,
      ).writeAsBytesSync(VaultCrypto.concat(<Uint8List>[header, ciphertext]));
    }

    void writeV2(Map<String, String> entries, String password, int iterations) {
      final salt = VaultCrypto.randomBytes(VaultCrypto.saltLength);
      final nonce = VaultCrypto.randomBytes(VaultCrypto.nonceLength);
      final key = VaultCrypto.deriveKeyPbkdf2(
        password: Uint8List.fromList(utf8.encode(password)),
        salt: salt,
        iterations: iterations,
      );
      final header = Uint8List(4 + 1 + 1 + 4 + 16 + 12);
      header.setAll(0, VaultFileCodec.magic);
      header[4] = VaultFileCodec.pbkdf2Version;
      header[5] = VaultKdfAlgorithm.pbkdf2Sha256.id;
      ByteData.sublistView(header, 6, 10).setUint32(0, iterations);
      header.setAll(10, salt);
      header.setAll(26, nonce);
      final ciphertext = VaultCrypto.aesGcm(
        forEncryption: true,
        key: key,
        nonce: nonce,
        input: VaultFileCodec.encodePayload(entries),
        aad: header,
      );
      File(
        vaultPath,
      ).writeAsBytesSync(VaultCrypto.concat(<Uint8List>[header, ciphertext]));
    }

    test('new vaults are written as v3 with Argon2id', () async {
      final session = await repository().create(
        'master password',
        entries: <String, String>{'A': '1'},
      );
      session.dispose();

      final info = await repository().describe();
      expect(info.formatVersion, VaultFileCodec.currentVersion);
      expect(info.kdf, isA<Argon2idParams>());
      expect((info.kdf! as Argon2idParams).memoryKib, 64);

      final opened = await repository().open('master password');
      expect(opened.entries['A'], '1');
      expect(opened.upgradedFormat, isFalse);
      opened.session.dispose();
    });

    test('a v1 vault opens and is re-keyed to Argon2id', () async {
      writeLegacyV1(<String, String>{'Old': 'plain text'}, 'legacy pass');

      final opened = await repository().open('legacy pass');
      expect(opened.entries['Old'], 'plain text');
      expect(opened.upgradedFormat, isTrue);
      opened.session.dispose();

      final info = await repository().describe();
      expect(info.formatVersion, VaultFileCodec.currentVersion);
      expect(info.kdf, isA<Argon2idParams>());

      final reopened = await repository().open('legacy pass');
      expect(reopened.upgradedFormat, isFalse);
      expect(reopened.entries['Old'], 'plain text');
      reopened.session.dispose();
    });

    test('a v2 PBKDF2 vault opens and is re-keyed to Argon2id', () async {
      writeV2(<String, String>{'B': '2'}, 'v2 pass', 4096);

      final info = await repository().describe();
      expect(info.formatVersion, VaultFileCodec.pbkdf2Version);
      expect(info.kdf, isA<Pbkdf2Params>());

      final opened = await repository().open('v2 pass');
      expect(opened.entries['B'], '2');
      expect(opened.upgradedFormat, isTrue);
      opened.session.dispose();

      final upgraded = await repository().describe();
      expect(upgraded.formatVersion, VaultFileCodec.currentVersion);
      expect(upgraded.kdf, isA<Argon2idParams>());
    });

    test('a wrong password is rejected on every format', () async {
      writeLegacyV1(<String, String>{'Old': 'x'}, 'right');
      await expectLater(
        repository().open('wrong'),
        throwsA(
          isA<VaultException>().having(
            (e) => e.kind,
            'kind',
            VaultErrorKind.invalidPassword,
          ),
        ),
      );

      writeV2(<String, String>{'Old': 'x'}, 'right', 2048);
      await expectLater(
        repository().open('wrong'),
        throwsA(isA<VaultException>()),
      );

      final session = await repository().create('right');
      session.dispose();
      await expectLater(
        repository().open('wrong'),
        throwsA(isA<VaultException>()),
      );
    });

    test(
      'the v3 header is authenticated: tampering breaks decryption',
      () async {
        final session = await repository().create(
          'pass',
          entries: <String, String>{'A': '1'},
        );
        session.dispose();

        final bytes = File(vaultPath).readAsBytesSync();
        // Flip a bit inside the KDF parameter block.
        bytes[7] ^= 0x01;
        File(vaultPath).writeAsBytesSync(bytes);

        await expectLater(
          repository().open('pass'),
          throwsA(isA<VaultException>()),
        );
      },
    );

    test('stronger parameters trigger a re-key, weaker ones do not', () async {
      const stronger = Argon2idParams(
        memoryKib: 128,
        iterations: 1,
        parallelism: 1,
      );

      final session = await repository().create(
        'pass',
        entries: <String, String>{'A': '1'},
      );
      session.dispose();

      final upgraded = await repository(stronger).open('pass');
      expect(upgraded.upgradedFormat, isTrue);
      expect((upgraded.session.kdf as Argon2idParams).memoryKib, 128);
      upgraded.session.dispose();

      // Opening with a weaker target must not rewrite the file.
      final unchanged = await repository(cheapArgon).open('pass');
      expect(unchanged.upgradedFormat, isFalse);
      expect((unchanged.session.kdf as Argon2idParams).memoryKib, 128);
      unchanged.session.dispose();
    });
  });
}
