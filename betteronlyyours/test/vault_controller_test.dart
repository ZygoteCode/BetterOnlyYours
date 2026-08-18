import 'dart:io';

import 'package:betteronlyyours/core/models/vault_entry.dart';
import 'package:betteronlyyours/core/models/vault_meta.dart';
import 'package:betteronlyyours/core/security/vault_repository.dart';
import 'package:betteronlyyours/core/services/settings_service.dart';
import 'package:betteronlyyours/state/settings_controller.dart';
import 'package:betteronlyyours/state/toast_controller.dart';
import 'package:betteronlyyours/state/vault_controller.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  late Directory tempDir;
  late String vaultPath;
  late VaultController vault;
  late ToastController toasts;
  late SettingsController settings;

  VaultRepository repository() => VaultRepository(
    path: vaultPath,
    deriveOnIsolate: false,
    iterationsOverride: 1000,
  );

  setUp(() async {
    tempDir = await Directory.systemTemp.createTemp('boy_controller_test');
    vaultPath = '${tempDir.path}${Platform.pathSeparator}credentials.plf';
    toasts = ToastController();
    settings = SettingsController(
      service: SettingsService(
        path: '${tempDir.path}${Platform.pathSeparator}settings.json',
      ),
    );
    settings.load();
    vault = VaultController(
      toasts: toasts,
      settings: settings,
      repository: repository(),
    );
  });

  tearDown(() async {
    vault.dispose();
    toasts.dispose();
    settings.dispose();
    if (tempDir.existsSync()) await tempDir.delete(recursive: true);
  });

  test('reports a missing vault, then unlocks after creation', () async {
    await vault.initialize();
    expect(vault.status, VaultStatus.missing);

    expect(await vault.createVault('master password'), isNull);
    expect(vault.status, VaultStatus.unlocked);
    expect(vault.isEmptyVault, isTrue);
  });

  test('entries survive a lock/unlock cycle', () async {
    await vault.createVault('master password');
    await vault.upsertEntry(
      VaultEntry.create('GitHub').copyWith(
        username: 'octocat',
        password: 'hunter2',
        tags: <String>['dev'],
      ),
    );

    vault.lock();
    expect(vault.status, VaultStatus.locked);
    expect(vault.entryCount, 0);

    expect(await vault.unlock('master password'), isNull);
    expect(vault.entryCount, 1);
    expect(vault.entry('GitHub')?.username, 'octocat');
    expect(vault.entry('GitHub')?.tags, <String>['dev']);
  });

  test('a wrong password leaves the vault locked', () async {
    await vault.createVault('master password');
    vault.lock();

    final failure = await vault.unlock('nope');
    expect(failure, isNotNull);
    expect(vault.status, VaultStatus.locked);
  });

  test('legacy entries are preserved verbatim on disk', () async {
    // Write a vault the way the original app did: name -> free text.
    final repo = repository();
    final session = await repo.create(
      'legacy',
      entries: <String, String>{'Server': 'root/toor'},
    );
    session.dispose();

    await vault.initialize();
    await vault.unlock('legacy');
    expect(vault.entry('Server')!.isLegacyFormat, isTrue);
    expect(vault.entry('Server')!.notes, 'root/toor');

    // Adding an unrelated entry must not rewrite the legacy one.
    await vault.upsertEntry(VaultEntry.create('New').copyWith(username: 'u'));

    final raw = await repository().open('legacy');
    expect(raw.entries['Server'], 'root/toor');
    expect(raw.entries['New']!.startsWith(VaultEntry.structuredPrefix), isTrue);
  });

  test('renaming keeps history and drops the old key', () async {
    await vault.createVault('pw');
    await vault.upsertEntry(VaultEntry.create('Old name'));
    vault.select('Old name');

    final entry = vault.entry('Old name')!;
    await vault.upsertEntry(
      entry.copyWith(title: 'New name'),
      previousTitle: 'Old name',
    );

    expect(vault.hasEntry('Old name'), isFalse);
    expect(vault.hasEntry('New name'), isTrue);
    expect(vault.selectedTitle, 'New name');
    expect(vault.lastUsedAt('New name'), isNotNull);
  });

  test('delete can be undone', () async {
    await vault.createVault('pw');
    await vault.upsertEntry(VaultEntry.create('Bank').copyWith(username: 'me'));

    await vault.deleteEntry('Bank');
    expect(vault.hasEntry('Bank'), isFalse);

    await vault.restoreLastDeleted();
    expect(vault.entry('Bank')?.username, 'me');
  });

  test('duplicate creates a distinct copy', () async {
    await vault.createVault('pw');
    await vault.upsertEntry(VaultEntry.create('Mail').copyWith(password: 'p'));

    await vault.duplicateEntry('Mail');
    expect(vault.hasEntry('Mail copy'), isTrue);
    expect(vault.entry('Mail copy')?.password, 'p');
  });

  test('favorites and filters drive the visible list', () async {
    await vault.createVault('pw');
    await vault.upsertEntry(
      VaultEntry.create('Alpha').copyWith(tags: <String>['work']),
    );
    await vault.upsertEntry(
      VaultEntry.create('Beta').copyWith(tags: <String>['home']),
    );
    await vault.toggleFavorite('Alpha');

    expect(vault.favorites.map((e) => e.title), <String>['Alpha']);

    vault.setTagFilter('home');
    expect(vault.visibleEntries.map((e) => e.title), <String>['Beta']);

    vault.setTagFilter(null);
    vault.setFavoritesOnly(true);
    expect(vault.visibleEntries.map((e) => e.title), <String>['Alpha']);

    vault.clearFilters();
    vault.setQuery('bet');
    expect(vault.visibleEntries.map((e) => e.title), <String>['Beta']);
  });

  test('recently opened is tracked and persisted inside the vault', () async {
    await vault.createVault('pw');
    await vault.upsertEntry(VaultEntry.create('First'));
    await vault.upsertEntry(VaultEntry.create('Second'));

    vault.select('First');
    vault.select('Second');
    expect(vault.recentEntries.first.title, 'Second');

    await vault.flushPendingWrites();
    final raw = await repository().open('pw');
    expect(raw.entries.containsKey(VaultMeta.storageKey), isTrue);

    vault.lock();
    await vault.unlock('pw');
    expect(vault.recentEntries.map((e) => e.title), contains('Second'));
  });

  test('the reserved metadata key is never exposed as an entry', () async {
    await vault.createVault('pw');
    await vault.upsertEntry(VaultEntry.create('Visible'));
    vault.select('Visible');
    await vault.flushPendingWrites();

    vault.lock();
    await vault.unlock('pw');

    expect(vault.entryCount, 1);
    expect(vault.hasEntry(VaultMeta.storageKey), isFalse);
    expect(
      await vault.upsertEntry(VaultEntry.create(VaultMeta.storageKey)),
      isFalse,
    );
  });

  test('changing the master password re-keys the vault', () async {
    await vault.createVault('old master');
    await vault.upsertEntry(VaultEntry.create('Item'));

    expect(await vault.changeMasterPassword('wrong', 'new master'), isNotNull);
    expect(
      await vault.changeMasterPassword('old master', 'new master'),
      isNull,
    );

    vault.lock();
    expect(await vault.unlock('old master'), isNotNull);
    expect(await vault.unlock('new master'), isNull);
    expect(vault.hasEntry('Item'), isTrue);
  });

  test('a failed write keeps the edit and can be retried', () async {
    await vault.createVault('pw');

    // A directory sitting on the temp-file path makes the atomic write fail.
    Directory('$vaultPath.tmp').createSync();

    final saved = await vault.upsertEntry(VaultEntry.create('Kept'));
    expect(saved, isFalse);
    expect(vault.saveState, SaveState.failed);
    expect(vault.lastSaveError, isNotNull);
    expect(vault.hasEntry('Kept'), isTrue, reason: 'the edit must survive');

    Directory('$vaultPath.tmp').deleteSync();

    expect(await vault.retrySave(), isTrue);
    expect(vault.saveState, SaveState.saved);

    vault.lock();
    await vault.unlock('pw');
    expect(vault.hasEntry('Kept'), isTrue);
  });
}
