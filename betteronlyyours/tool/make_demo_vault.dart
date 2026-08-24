// ignore_for_file: avoid_print
// Dev utility: builds a throwaway vault for manual QA, so the real vault is
// never touched. Usage:
//   dart run tool/make_demo_vault.dart <path> <master password>
import 'dart:convert';
import 'dart:io';

import 'package:betteronlyyours/core/models/vault_entry.dart';
import 'package:betteronlyyours/core/models/vault_meta.dart';
import 'package:betteronlyyours/core/security/base32.dart';
import 'package:betteronlyyours/core/security/totp.dart';
import 'package:betteronlyyours/core/security/totp_secret_box.dart';
import 'package:betteronlyyours/core/security/vault_repository.dart';

Future<void> main(List<String> args) async {
  final path = args.isNotEmpty ? args[0] : 'demo_credentials.plf';
  final password = args.length > 1 ? args[1] : 'demo master password';
  const secretText = 'JBSWY3DPEHPK3PXP';

  final contentKey = TotpSecretBox.newContentKey();
  final sealed = TotpSecretBox.seal(
    secret: Base32.decode(secretText),
    config: const TotpConfig(issuer: 'GitHub', account: 'demo@example.com'),
    contentKey: contentKey,
  );

  final now = DateTime.now();
  final entries = <String, String>{
    'GitHub': VaultEntry.create('GitHub')
        .copyWith(
          username: 'demo@example.com',
          password: r'Kq7#vN2m!Ld8@Rp4',
          url: 'https://github.com',
          totp: sealed,
          favorite: true,
        )
        .toStorageValue(),
    'Bank': VaultEntry.create('Bank')
        .copyWith(username: 'demo', password: '123456')
        .toStorageValue(),
    VaultMeta.storageKey: VaultMeta(
      createdAt: now,
      updatedAt: now,
    ).withSecretKey(base64.encode(contentKey)).toStorageValue(),
  };

  final file = File(path);
  if (file.existsSync()) file.deleteSync();

  final repository = VaultRepository(path: path, deriveOnIsolate: false);
  final session = await repository.create(password, entries: entries);
  session.dispose();

  final code = Totp.generate(secret: Base32.decode(secretText));
  print('vault: $path');
  print('password: $password');
  print('totp secret: $secretText');
  print('code right now: ${code.value} (${code.secondsRemaining}s left)');
}
