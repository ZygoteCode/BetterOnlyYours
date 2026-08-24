import 'dart:typed_data';

import 'package:betteronlyyours/core/models/vault_entry.dart';
import 'package:betteronlyyours/core/models/vault_meta.dart';
import 'package:betteronlyyours/core/security/base32.dart';
import 'package:betteronlyyours/core/security/totp.dart';
import 'package:betteronlyyours/core/security/totp_secret_box.dart';
import 'package:betteronlyyours/state/settings_controller.dart';
import 'package:betteronlyyours/state/toast_controller.dart';
import 'package:betteronlyyours/state/vault_controller.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/in_memory_vault_store.dart';
import 'support/memory_settings_service.dart';

/// How a two-factor token behaves once it is inside a vault.
void main() {
  const secretText = 'JBSWY3DPEHPK3PXP';
  const otherSecretText = 'KRSXG5CTMVRXEZLU';
  final at = DateTime.fromMillisecondsSinceEpoch(1111111109000, isUtc: true);

  Uint8List secret([String text = secretText]) => Base32.decode(text);

  late InMemoryVaultStore store;
  late VaultController vault;
  late SettingsController settings;
  late ToastController toasts;

  setUp(() async {
    store = InMemoryVaultStore();
    settings = SettingsController(service: MemorySettingsService())..load();
    toasts = ToastController();
    vault = VaultController(
      toasts: toasts,
      settings: settings,
      repository: store,
    );
    await vault.createVault('master');
    await vault.upsertEntry(
      VaultEntry.create('GitHub').copyWith(password: 'pw'),
    );
  });

  tearDown(() {
    vault.dispose();
    settings.dispose();
    toasts.dispose();
  });

  test('storing a secret makes the entry generate codes', () async {
    expect(vault.entry('GitHub')!.hasTotp, isFalse);
    expect(vault.totpCode(vault.entry('GitHub')!), isNull);

    final stored = await vault.setTotp(
      'GitHub',
      secret: secret(),
      config: const TotpConfig(),
    );

    expect(stored, isTrue);
    final entry = vault.entry('GitHub')!;
    expect(entry.hasTotp, isTrue);
    expect(vault.totpCount, 1);
    expect(
      vault.totpCode(entry, at: at)!.value,
      Totp.generate(secret: secret(), at: at).value,
    );
  });

  test('the raw secret never reaches the vault file', () async {
    await vault.setTotp(
      'GitHub',
      secret: secret(),
      config: const TotpConfig(issuer: 'GitHub', account: 'alice'),
    );

    final raw = store.contents['GitHub']!;
    expect(raw.contains(secretText), isFalse);
    expect(raw.contains(TotpSecretBox.prefix), isTrue);
    expect(vault.entry('GitHub')!.totp.contains(secretText), isFalse);
    // The search index must not carry the token either.
    expect(vault.entry('GitHub')!.searchText.contains('BOYTOTP'), isFalse);
  });

  test('replacing the secret changes the code immediately', () async {
    await vault.setTotp('GitHub', secret: secret(), config: const TotpConfig());
    final first = vault.totpCode(vault.entry('GitHub')!, at: at)!.value;

    await vault.setTotp(
      'GitHub',
      secret: secret(otherSecretText),
      config: const TotpConfig(),
    );
    final second = vault.totpCode(vault.entry('GitHub')!, at: at)!.value;

    expect(second, isNot(first));
    expect(
      second,
      Totp.generate(secret: secret(otherSecretText), at: at).value,
    );
  });

  test('the settings of a token are readable, the secret is not', () async {
    const config = TotpConfig(
      algorithm: TotpAlgorithm.sha512,
      digits: 8,
      period: 60,
      issuer: 'ACME',
      account: 'alice',
    );
    await vault.setTotp('GitHub', secret: secret(), config: config);

    expect(vault.totpConfig(vault.entry('GitHub')!), config);
    expect(vault.totpCode(vault.entry('GitHub')!, at: at)!.value.length, 8);
  });

  test('removing a token clears it', () async {
    await vault.setTotp('GitHub', secret: secret(), config: const TotpConfig());

    expect(await vault.removeTotp('GitHub'), isTrue);
    expect(vault.entry('GitHub')!.hasTotp, isFalse);
    expect(vault.totpCount, 0);
    expect(await vault.removeTotp('GitHub'), isFalse);
    expect(store.contents['GitHub']!.contains(TotpSecretBox.prefix), isFalse);
  });

  test('tokens survive a lock and unlock cycle', () async {
    await vault.setTotp(
      'GitHub',
      secret: secret(),
      config: const TotpConfig(digits: 8),
    );
    final before = vault.totpCode(vault.entry('GitHub')!, at: at)!.value;

    vault.lock();
    expect(vault.totpCode(const VaultEntry(title: 'GitHub')), isNull);

    await vault.unlock('master');
    expect(vault.totpCode(vault.entry('GitHub')!, at: at)!.value, before);
  });

  test('the token key is written to the vault metadata once', () async {
    await vault.setTotp('GitHub', secret: secret(), config: const TotpConfig());

    final metaRaw = store.contents[VaultMeta.storageKey];
    expect(metaRaw, isNotNull);
    final key = VaultMeta.fromStorage(metaRaw!).secretKey;
    expect(key, isNotEmpty);

    await vault.upsertEntry(VaultEntry.create('Bank'));
    await vault.setTotp(
      'Bank',
      secret: secret(otherSecretText),
      config: const TotpConfig(),
    );

    final updated = VaultMeta.fromStorage(
      store.contents[VaultMeta.storageKey]!,
    );
    expect(updated.secretKey, key, reason: 'the key must never be rotated');
    expect(vault.totpCount, 2);
  });

  test('changing the master password keeps every token working', () async {
    await vault.setTotp('GitHub', secret: secret(), config: const TotpConfig());
    final before = vault.totpCode(vault.entry('GitHub')!, at: at)!.value;

    final error = await vault.changeMasterPassword('master', 'stronger one');
    expect(error, isNull);

    expect(vault.totpCode(vault.entry('GitHub')!, at: at)!.value, before);

    vault.lock();
    await vault.unlock('stronger one');
    expect(vault.totpCode(vault.entry('GitHub')!, at: at)!.value, before);
  });

  test('a duplicate does not inherit the token', () async {
    await vault.setTotp('GitHub', secret: secret(), config: const TotpConfig());

    await vault.duplicateEntry('GitHub');

    expect(vault.entry('GitHub copy')!.hasTotp, isFalse);
    expect(vault.entry('GitHub')!.hasTotp, isTrue);
    expect(vault.totpCount, 1);
  });

  test('renaming an entry carries its token along', () async {
    await vault.setTotp('GitHub', secret: secret(), config: const TotpConfig());
    final before = vault.totpCode(vault.entry('GitHub')!, at: at)!.value;

    await vault.upsertEntry(
      vault.entry('GitHub')!.copyWith(title: 'GitHub Inc'),
      previousTitle: 'GitHub',
    );

    expect(vault.entry('GitHub'), isNull);
    expect(vault.totpCode(vault.entry('GitHub Inc')!, at: at)!.value, before);
  });

  test('a token sealed by another vault is reported, not crashed on', () async {
    final foreign = TotpSecretBox.seal(
      secret: secret(),
      config: const TotpConfig(),
      contentKey: TotpSecretBox.newContentKey(),
    );

    await vault.upsertEntry(
      VaultEntry.create('Imported').copyWith(totp: foreign),
    );

    final entry = vault.entry('Imported')!;
    expect(entry.hasTotp, isTrue);
    expect(vault.totpCode(entry), isNull);
    expect(vault.totpConfig(entry), isNull);
    expect(vault.totpIsUnreadable(entry), isTrue);
  });

  test('a deleted entry can be restored with its token', () async {
    await vault.setTotp('GitHub', secret: secret(), config: const TotpConfig());
    final before = vault.totpCode(vault.entry('GitHub')!, at: at)!.value;

    await vault.deleteEntry('GitHub');
    expect(vault.totpCount, 0);

    await vault.restoreLastDeleted();
    expect(vault.totpCode(vault.entry('GitHub')!, at: at)!.value, before);
  });

  test('the entry model keeps the token through serialisation', () {
    final sealedLike = '${TotpSecretBox.prefix}aaa.bbb.ccc';
    final entry = const VaultEntry(title: 'X').copyWith(totp: sealedLike);

    expect(entry.isStructured, isTrue);
    final parsed = VaultEntry.fromStorage('X', entry.toStorageValue());
    expect(parsed.totp, sealedLike);
    expect(parsed.hasTotp, isTrue);
    expect(parsed.withoutTotp().hasTotp, isFalse);
  });
}
