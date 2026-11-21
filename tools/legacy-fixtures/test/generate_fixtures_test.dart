// Generates BetterOnlyYours vault fixtures with the legacy app's own code, so
// OnlyYours' migration tests run against byte-for-byte genuine files.
//
//   cd tools/legacy-fixtures
//   flutter pub get
//   flutter test test/generate_fixtures_test.dart
//
// Output: ../../tests/OnlyYours.Core.Tests/Fixtures/Legacy (override: FIXTURE_OUT).
// Every vault is re-opened with the legacy VaultRepository before the manifest is
// written, which proves the files are exactly what BetterOnlyYours reads.
import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:betteronlyyours/core/models/app_settings.dart';
import 'package:betteronlyyours/core/models/vault_entry.dart';
import 'package:betteronlyyours/core/models/vault_meta.dart';
import 'package:betteronlyyours/core/security/base32.dart';
import 'package:betteronlyyours/core/security/totp.dart';
import 'package:betteronlyyours/core/security/totp_secret_box.dart';
import 'package:betteronlyyours/core/security/vault_crypto.dart';
import 'package:betteronlyyours/core/security/vault_file.dart';
import 'package:betteronlyyours/core/security/vault_kdf.dart';
import 'package:betteronlyyours/core/security/vault_repository.dart';
import 'package:flutter_test/flutter_test.dart';

const String totpSecretBase32 = 'JBSWY3DPEHPK3PXP';
final DateTime codeTime = DateTime.fromMillisecondsSinceEpoch(1700000000000, isUtc: true);
final DateTime codeTime2 = DateTime.fromMillisecondsSinceEpoch(1234567890000, isUtc: true);

DateTime at(int y, int m, int d, [int h = 12]) => DateTime.utc(y, m, d, h);

String hex(List<int> bytes) => bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();

Uint8List utf8Bytes(String s) => Uint8List.fromList(utf8.encode(s));

/// One vault to generate plus everything the C# tests should find inside it.
class FixtureVault {
  FixtureVault(this.file, this.password, this.version, this.kdf, this.entries, {this.meta, this.extraRaw = const {}});

  final String file;
  final String password;
  final int version;
  final VaultKdfParams kdf;
  final List<Map<String, dynamic>> entries;
  final VaultMeta? meta;

  /// Raw map values written verbatim (non-structured payload shapes).
  final Map<String, String> extraRaw;
}

Map<String, dynamic> describeKdf(VaultKdfParams kdf) => switch (kdf) {
  Argon2idParams a => {'kdf': 'argon2id', 'memoryKiB': a.memoryKib, 'iterations': a.iterations, 'parallelism': a.parallelism},
  Pbkdf2Params p => {'kdf': 'pbkdf2', 'iterations': p.iterations},
};

Future<void> writeVault(Directory out, FixtureVault fixture, Map<String, String> payload) async {
  final salt = VaultCrypto.randomBytes(VaultCrypto.saltLength);
  final nonce = VaultCrypto.randomBytes(VaultCrypto.nonceLength);
  final key = fixture.kdf.deriveKey(utf8Bytes(fixture.password), salt);
  late Uint8List header;
  switch (fixture.version) {
    case 1:
      // magic | 1 | salt | nonce — PBKDF2-HMAC-SHA256, 3000 iterations.
      header = VaultCrypto.concat([VaultFileCodec.magic, Uint8List.fromList([1]), salt, nonce]);
    case 2:
      // magic | 2 | kdf=1 | iterations (u32 BE) | salt | nonce.
      final iterations = ByteData(4)..setUint32(0, (fixture.kdf as Pbkdf2Params).iterations);
      header = VaultCrypto.concat([
        VaultFileCodec.magic,
        Uint8List.fromList([2, VaultKdfAlgorithm.pbkdf2Sha256.id]),
        iterations.buffer.asUint8List(),
        salt,
        nonce,
      ]);
    default:
      header = VaultFileCodec.buildHeader(salt: salt, nonce: nonce, kdf: fixture.kdf);
  }

  final ciphertext = VaultCrypto.aesGcm(
    forEncryption: true,
    key: key,
    nonce: nonce,
    input: VaultFileCodec.encodePayload(payload),
    aad: header,
  );
  await File('${out.path}/${fixture.file}').writeAsBytes(VaultCrypto.concat([header, ciphertext]), flush: true);
}

/// Re-opens a copy with the legacy repository (no upgrade: same KDF policy).
Future<Map<String, String>> reopen(String path, String password, VaultKdfParams kdf) async {
  final temp = await Directory.systemTemp.createTemp('boy-fixture-');
  try {
    final copy = File('${temp.path}/credentials.plf');
    await File(path).copy(copy.path);
    final repository = VaultRepository(path: copy.path, deriveOnIsolate: false, kdfOverride: kdf);
    final result = await repository.open(password);
    expect(result.upgradedFormat, isFalse);
    result.session.dispose();
    return result.entries;
  } finally {
    await temp.delete(recursive: true);
  }
}

void main() {
  test('generate BetterOnlyYours fixtures', () async {
    final out = Directory(Platform.environment['FIXTURE_OUT'] ?? '../../tests/OnlyYours.Core.Tests/Fixtures/Legacy');
    if (out.existsSync()) await out.delete(recursive: true);
    await out.create(recursive: true);

    final contentKey = TotpSecretBox.newContentKey();
    final secret = Base32.decode(totpSecretBase32);
    final sha1Config = const TotpConfig(issuer: 'GitHub', account: 'ada@example.com');
    final sha256Config = const TotpConfig(algorithm: TotpAlgorithm.sha256, digits: 8, period: 60);

    Map<String, dynamic> totpFacts(TotpConfig config) => {
      'secretBase32': totpSecretBase32,
      'algorithm': config.algorithm.label,
      'digits': config.digits,
      'period': config.period,
      'codes': [
        {'unixMs': codeTime.millisecondsSinceEpoch, 'code': Totp.generate(secret: secret, config: config, at: codeTime).value},
        {'unixMs': codeTime2.millisecondsSinceEpoch, 'code': Totp.generate(secret: secret, config: config, at: codeTime2).value},
      ],
    };

    // ---------------------------------------------------------------- rich v3
    final github = VaultEntry(
      title: 'GitHub',
      username: 'ada@example.com',
      password: 'Tr0ub4dor&3-horse',
      url: 'https://github.com/login',
      notes: 'Account principale.\nChiavi SSH sul portatile.',
      tags: const ['dev', 'lavoro'],
      favorite: true,
      createdAt: at(2023, 1, 10),
      updatedAt: at(2024, 6, 1),
      customFields: const [
        VaultCustomField(label: 'Codici di recupero', value: 'a1b2-c3d4-e5f6', secret: true),
        VaultCustomField(label: 'Team', value: 'Platform'),
      ],
      passwordHistory: [
        VaultPasswordRecord(password: 'oldPassword#2', replacedAt: at(2024, 6, 1)),
        VaultPasswordRecord(password: 'oldPassword#1', replacedAt: at(2023, 9, 15)),
      ],
      totp: TotpSecretBox.seal(secret: secret, config: sha1Config, contentKey: contentKey),
    );
    final bank = VaultEntry(
      title: 'Banca Intesa',
      username: 'ada.lovelace',
      password: 'Pàss€wörd!🔐 κλειδί',
      url: 'intesasanpaolo.com',
      tags: const ['banca'],
      createdAt: at(2022, 3, 2),
      updatedAt: at(2022, 3, 2),
      totp: TotpSecretBox.seal(secret: secret, config: sha256Config, contentKey: contentKey),
    );
    final server = VaultEntry(
      title: 'Server SSH',
      username: 'root',
      password: r'p@$$w0rd with spaces & "quotes"',
      url: 'ssh://10.0.0.5:2222',
      notes: 'Riga 1\r\nRiga 2\tcon tab\n— fine —',
      createdAt: at(2021, 11, 20),
      updatedAt: at(2021, 11, 21),
      extra: const {'color': 'blue', 'pinnedAt': 1650000000000},
    );
    final oldAccount = VaultEntry(
      title: 'Vecchio account',
      username: 'ada1815',
      createdAt: at(2019, 5, 5),
      updatedAt: at(2019, 5, 5),
    );
    final special = VaultEntry(
      title: 'Account "speciale" 🚀 <tag> & co',
      password: 'emoji🚀🚀and\u0000null?',
      url: 'https://example.org/path?q=1&x=ä',
      createdAt: at(2024, 1, 1),
      updatedAt: at(2024, 2, 2),
    );
    const plainNotes = 'SSID: Casa\nPassword Wi-Fi: correct-horse-battery\nRouter: 192.168.1.1';

    final meta = VaultMeta(
      createdAt: at(2019, 5, 5),
      updatedAt: at(2024, 6, 1),
      lastUsed: {'GitHub': at(2024, 6, 2, 9), 'Banca Intesa': at(2024, 5, 30, 18)},
    ).withSecretKey(base64.encode(contentKey));

    List<Map<String, dynamic>> describe(List<VaultEntry> entries) => [
      for (final e in entries)
        {
          'title': e.title,
          'structured': e.isStructured,
          'username': e.username,
          'password': e.password,
          'url': e.url,
          'notes': e.notes,
          'tags': e.tags,
          'favorite': e.favorite,
          if (e.createdAt != null) 'createdAt': e.createdAt!.millisecondsSinceEpoch,
          if (e.updatedAt != null) 'updatedAt': e.updatedAt!.millisecondsSinceEpoch,
          'fields': [for (final f in e.customFields) f.toJson()],
          'history': [for (final h in e.passwordHistory) h.toJson()],
          if (identical(e, github)) 'totp': totpFacts(sha1Config),
          if (identical(e, bank)) 'totp': totpFacts(sha256Config),
          'extra': e.extra,
        },
    ];

    final richEntries = [github, bank, server, oldAccount, special];
    final richPayload = <String, String>{
      for (final e in richEntries) e.title: e.toStorageValue(),
      'Wi-Fi casa': plainNotes,
      VaultMeta.storageKey: meta.toStorageValue(),
    };

    final argonKdf = const Argon2idParams(memoryKib: 8192, iterations: 2, parallelism: 2);
    final fixtures = <Map<String, dynamic>>[];

    Future<void> emit(String file, String password, int version, VaultKdfParams kdf, Map<String, String> payload, List<Map<String, dynamic>> entries, {Map<String, dynamic>? extra}) async {
      await writeVault(out, FixtureVault(file, password, version, kdf, entries), payload);
      final reopened = await reopen('${out.path}/$file', password, kdf);
      expect(reopened, equals(payload));
      fixtures.add({'file': file, 'password': password, 'version': version, ...describeKdf(kdf), 'entries': entries, ...?extra});
    }

    await emit(
      'v3-argon2id.plf',
      'correct horse battery staple ✓',
      3,
      argonKdf,
      richPayload,
      [
        ...describe(richEntries),
        {'title': 'Wi-Fi casa', 'structured': false, 'notes': plainNotes, 'username': '', 'password': '', 'url': '', 'tags': [], 'favorite': false, 'fields': [], 'history': [], 'extra': {}},
      ],
      extra: {
        'lastUsed': {for (final e in meta.lastUsed.entries) e.key: e.value.millisecondsSinceEpoch},
        'metaCreatedAt': meta.createdAt!.millisecondsSinceEpoch,
      },
    );

    // ------------------------------------------------------------ v3 PBKDF2
    final simple = VaultEntry(
      title: 'Posta',
      username: 'ada@posta.it',
      password: 'Sp3cial-Chars_!?',
      url: 'https://mail.example.it',
      createdAt: at(2020, 4, 4),
      updatedAt: at(2020, 4, 5),
    );
    await emit(
      'v3-pbkdf2.plf',
      'pbkdf2-password',
      3,
      const Pbkdf2Params(iterations: 2000),
      {simple.title: simple.toStorageValue(), 'Nota libera': 'Solo testo'},
      [
        ...describe([simple]),
        {'title': 'Nota libera', 'structured': false, 'notes': 'Solo testo', 'username': '', 'password': '', 'url': '', 'tags': [], 'favorite': false, 'fields': [], 'history': [], 'extra': {}},
      ],
    );

    // ----------------------------------------------------------------- v2
    await emit(
      'v2-pbkdf2.plf',
      'v2 password',
      2,
      const Pbkdf2Params(iterations: 5000),
      {'Forum': 'utente: ada / pw: hunter2', 'Codice allarme': '4711'},
      [
        {'title': 'Forum', 'structured': false, 'notes': 'utente: ada / pw: hunter2', 'username': '', 'password': '', 'url': '', 'tags': [], 'favorite': false, 'fields': [], 'history': [], 'extra': {}},
        {'title': 'Codice allarme', 'structured': false, 'notes': '4711', 'username': '', 'password': '', 'url': '', 'tags': [], 'favorite': false, 'fields': [], 'history': [], 'extra': {}},
      ],
    );

    // ----------------------------------------------------------------- v1
    await emit(
      'v1.plf',
      'first-build',
      1,
      Pbkdf2Params.legacyV1,
      {'Primo segreto': 'scritto dalla prima build'},
      [
        {'title': 'Primo segreto', 'structured': false, 'notes': 'scritto dalla prima build', 'username': '', 'password': '', 'url': '', 'tags': [], 'favorite': false, 'fields': [], 'history': [], 'extra': {}},
      ],
    );

    // -------------------------------------- damaged main file, good .bak copy
    final backupDir = Directory('${out.path}/backup-only')..createSync();
    await writeVault(backupDir, FixtureVault('credentials.plf.bak', 'backup password', 3, argonKdf, const []), {simple.title: simple.toStorageValue()});
    final bakBytes = await File('${backupDir.path}/credentials.plf.bak').readAsBytes();
    await File('${backupDir.path}/credentials.plf').writeAsBytes(bakBytes.sublist(0, 20), flush: true);
    {
      final repository = VaultRepository(path: '${backupDir.path}/credentials.plf', deriveOnIsolate: false, kdfOverride: argonKdf);
      // Verify on a copy so the damaged file stays damaged.
      final temp = await Directory.systemTemp.createTemp('boy-bak-');
      await File('${backupDir.path}/credentials.plf').copy('${temp.path}/credentials.plf');
      await File('${backupDir.path}/credentials.plf.bak').copy('${temp.path}/credentials.plf.bak');
      final result = await VaultRepository(path: '${temp.path}/credentials.plf', deriveOnIsolate: false, kdfOverride: argonKdf).open('backup password');
      expect(result.recoveredFromBackup, isTrue);
      expect(result.entries.keys, contains('Posta'));
      result.session.dispose();
      await temp.delete(recursive: true);
      expect(repository, isNotNull);
    }
    fixtures.add({
      'file': 'backup-only/credentials.plf',
      'password': 'backup password',
      'version': 3,
      ...describeKdf(argonKdf),
      'recoveredFromBackup': true,
      'entries': describe([simple]),
    });

    // ------------------------------------------------------- legacy settings
    final settings = AppSettings.fromJson(<String, dynamic>{
      'language': 'italian',
      'theme': 'obsidian',
      'density': 'compact',
      'reduceMotion': true,
      'autoLockMinutes': 15,
      'clipboardClearSeconds': 15,
      'confirmDelete': false,
      'revealSecretsByDefault': false,
      'keepPasswordHistory': true,
      'sidebarWidth': 280.0,
      'globalHotkeyEnabled': false,
      'rememberWindowBounds': true,
      'generator': {
        'mode': 'passphrase',
        'length': 24,
        'lowercase': true,
        'uppercase': true,
        'digits': true,
        'symbols': false,
        'avoidAmbiguous': true,
        'words': 6,
        'separator': '.',
        'capitalizeWords': true,
        'appendNumber': false,
      },
    });
    await File('${out.path}/settings.json').writeAsString(const JsonEncoder.withIndent('  ').convert(settings.toJson()));

    // ----------------------------------- Argon2id cross-vectors (PointyCastle)
    final argonVectors = <Map<String, dynamic>>[];
    final cases = [
      ('password', 'somesaltsomesalt', 64, 1, 1),
      ('password', 'somesaltsomesalt', 256, 2, 2),
      ('pässwörd ✓', '0123456789abcdef', 1024, 3, 4),
      ('', 'emptypasswordsal', 512, 1, 3),
      ('a much longer passphrase with many words', 'longersaltvalue!', 2048, 2, 1),
    ];
    for (final (pw, salt, m, t, p) in cases) {
      final key = VaultCrypto.deriveKeyArgon2id(password: utf8Bytes(pw), salt: utf8Bytes(salt), memoryKib: m, iterations: t, parallelism: p);
      argonVectors.add({'password': hex(utf8.encode(pw)), 'salt': hex(utf8.encode(salt)), 'memoryKiB': m, 'iterations': t, 'parallelism': p, 'key': hex(key)});
    }

    // ------------------------------------------- PBKDF2 / HKDF cross-vectors
    final pbkdf2 = VaultCrypto.deriveKeyPbkdf2(password: utf8Bytes('password'), salt: utf8Bytes('saltsaltsaltsalt'), iterations: 3000);
    final hkdf = VaultCrypto.hkdfSha256(key: Uint8List.fromList(List.filled(32, 7)), salt: Uint8List.fromList(List.filled(16, 9)), info: 'betteronlyyours/totp/v1/entry-key');

    final manifest = {
      'generator': 'BetterOnlyYours legacy code (path dependency), tools/legacy-fixtures',
      'generatedAt': DateTime.now().toUtc().toIso8601String(),
      'vaults': fixtures,
      'settings': {'file': 'settings.json', 'language': 'it', 'autoLockMinutes': settings.autoLockMinutes, 'clipboardClearSeconds': settings.clipboardClearSeconds},
      'argon2id': argonVectors,
      'pbkdf2Sha256': {'password': hex(utf8.encode('password')), 'salt': hex(utf8.encode('saltsaltsaltsalt')), 'iterations': 3000, 'key': hex(pbkdf2)},
      'hkdfSha256': {'ikm': hex(List.filled(32, 7)), 'salt': hex(List.filled(16, 9)), 'info': 'betteronlyyours/totp/v1/entry-key', 'key': hex(hkdf)},
    };
    await File('${out.path}/manifest.json').writeAsString(const JsonEncoder.withIndent('  ').convert(manifest));
  }, timeout: const Timeout(Duration(minutes: 5)));
}
