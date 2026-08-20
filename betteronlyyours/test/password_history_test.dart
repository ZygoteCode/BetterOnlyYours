import 'package:betteronlyyours/core/models/vault_entry.dart';
import 'package:betteronlyyours/state/settings_controller.dart';
import 'package:betteronlyyours/state/toast_controller.dart';
import 'package:betteronlyyours/state/vault_controller.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/in_memory_vault_store.dart';
import 'support/memory_settings_service.dart';

void main() {
  group('VaultEntry password history', () {
    final t0 = DateTime.fromMillisecondsSinceEpoch(1700000000000);

    test('records the replaced password, newest first', () {
      const previous = VaultEntry(title: 'A', password: 'old');
      final next = previous
          .copyWith(password: 'new')
          .withHistoryFrom(previous, now: t0);

      expect(next.password, 'new');
      expect(next.passwordHistory.single.password, 'old');
      expect(next.passwordHistory.single.replacedAt, t0);
    });

    test('an unchanged password adds nothing', () {
      const previous = VaultEntry(title: 'A', password: 'same');
      final next = previous.withHistoryFrom(previous, now: t0);
      expect(next.passwordHistory, isEmpty);
    });

    test('an entry without a password adds nothing', () {
      const previous = VaultEntry(title: 'A');
      final next = previous
          .copyWith(password: 'first')
          .withHistoryFrom(previous, now: t0);
      expect(next.passwordHistory, isEmpty);
    });

    test('the password in use is never duplicated in the history', () {
      const previous = VaultEntry(
        title: 'A',
        password: 'b',
        passwordHistory: <VaultPasswordRecord>[],
      );
      final rotated = previous
          .copyWith(password: 'a')
          .withHistoryFrom(previous, now: t0);
      // Rotating back to "b" must drop "b" from the history.
      final rotatedBack = rotated
          .copyWith(password: 'b')
          .withHistoryFrom(rotated, now: t0);

      expect(rotatedBack.password, 'b');
      expect(rotatedBack.passwordHistory.map((r) => r.password), <String>['a']);
    });

    test('history is capped and keeps the newest entries', () {
      var entry = const VaultEntry(title: 'A', password: 'p0');
      for (var i = 1; i <= VaultEntry.maxPasswordHistory + 5; i++) {
        final next = entry.copyWith(password: 'p$i');
        entry = next.withHistoryFrom(entry, now: t0.add(Duration(minutes: i)));
      }

      expect(entry.passwordHistory.length, VaultEntry.maxPasswordHistory);
      expect(entry.passwordHistory.first.password, 'p24');
      expect(entry.password, 'p25');
    });

    test('survives serialisation', () {
      final entry = const VaultEntry(title: 'A', password: 'new').copyWith(
        passwordHistory: <VaultPasswordRecord>[
          VaultPasswordRecord(password: 'old', replacedAt: t0),
        ],
      );

      final parsed = VaultEntry.fromStorage('A', entry.toStorageValue());
      expect(parsed.passwordHistory.single.password, 'old');
      expect(parsed.passwordHistory.single.replacedAt, t0);
    });

    test('history alone makes an entry structured', () {
      final entry = const VaultEntry(title: 'A').copyWith(
        passwordHistory: <VaultPasswordRecord>[
          VaultPasswordRecord(password: 'old', replacedAt: t0),
        ],
      );
      expect(entry.isLegacyFormat, isFalse);
      expect(
        entry.toStorageValue().startsWith(VaultEntry.structuredPrefix),
        isTrue,
      );
    });

    test('malformed history records are dropped, not fatal', () {
      final parsed = VaultEntry.fromStorage(
        'A',
        '${VaultEntry.structuredPrefix}'
            '{"password":"p","history":[{"nope":1},{"password":"good"}]}',
      );
      expect(parsed.passwordHistory.length, 1);
      expect(parsed.passwordHistory.single.password, 'good');
    });

    test('withoutPasswordHistory clears it', () {
      final entry = const VaultEntry(title: 'A', password: 'x').copyWith(
        passwordHistory: <VaultPasswordRecord>[
          VaultPasswordRecord(password: 'old', replacedAt: t0),
        ],
      );
      expect(entry.withoutPasswordHistory().passwordHistory, isEmpty);
    });
  });

  group('VaultController password history', () {
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
      await vault.createVault('pw');
      await vault.upsertEntry(
        VaultEntry.create('GitHub').copyWith(password: 'first'),
      );
    });

    tearDown(() {
      vault.dispose();
      settings.dispose();
      toasts.dispose();
    });

    test('changing a password records the old one', () async {
      await vault.upsertEntry(
        vault.entry('GitHub')!.copyWith(password: 'second'),
      );

      final entry = vault.entry('GitHub')!;
      expect(entry.password, 'second');
      expect(entry.passwordHistory.single.password, 'first');
    });

    test('restoring swaps the current and the historical password', () async {
      await vault.upsertEntry(
        vault.entry('GitHub')!.copyWith(password: 'second'),
      );
      final record = vault.entry('GitHub')!.passwordHistory.single;

      await vault.restorePassword('GitHub', record);

      final entry = vault.entry('GitHub')!;
      expect(entry.password, 'first');
      expect(entry.passwordHistory.single.password, 'second');
    });

    test('a single record can be forgotten', () async {
      await vault.upsertEntry(
        vault.entry('GitHub')!.copyWith(password: 'second'),
      );
      final record = vault.entry('GitHub')!.passwordHistory.single;

      expect(await vault.forgetPassword('GitHub', record), isTrue);
      expect(vault.entry('GitHub')!.passwordHistory, isEmpty);
      expect(await vault.forgetPassword('GitHub', record), isFalse);
    });

    test('history can be cleared per entry and vault-wide', () async {
      await vault.upsertEntry(
        VaultEntry.create('Bank').copyWith(password: 'b1'),
      );
      await vault.upsertEntry(
        vault.entry('GitHub')!.copyWith(password: 'second'),
      );
      await vault.upsertEntry(vault.entry('Bank')!.copyWith(password: 'b2'));

      expect(vault.storedPasswordHistoryCount, 2);

      expect(await vault.clearPasswordHistory('GitHub'), isTrue);
      expect(vault.storedPasswordHistoryCount, 1);

      expect(await vault.clearAllPasswordHistory(), isTrue);
      expect(vault.storedPasswordHistoryCount, 0);
      expect(await vault.clearAllPasswordHistory(), isFalse);
    });

    test('the setting disables and purges recording', () async {
      settings.setKeepPasswordHistory(false);

      await vault.upsertEntry(
        vault.entry('GitHub')!.copyWith(password: 'second'),
      );

      expect(vault.entry('GitHub')!.passwordHistory, isEmpty);
      expect(vault.storedPasswordHistoryCount, 0);
    });

    test('duplicating an entry does not copy old secrets', () async {
      await vault.upsertEntry(
        vault.entry('GitHub')!.copyWith(password: 'second'),
      );
      await vault.duplicateEntry('GitHub');

      expect(vault.entry('GitHub copy')!.password, 'second');
      expect(vault.entry('GitHub copy')!.passwordHistory, isEmpty);
    });

    test('history reaches storage and survives a lock cycle', () async {
      await vault.upsertEntry(
        vault.entry('GitHub')!.copyWith(password: 'second'),
      );

      expect(store.contents['GitHub'], contains('history'));

      vault.lock();
      await vault.unlock('pw');
      expect(vault.entry('GitHub')!.passwordHistory.single.password, 'first');
    });
  });
}
