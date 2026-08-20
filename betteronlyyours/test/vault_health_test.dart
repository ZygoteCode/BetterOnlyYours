import 'package:betteronlyyours/core/models/vault_entry.dart';
import 'package:betteronlyyours/core/services/password_strength.dart';
import 'package:betteronlyyours/core/services/vault_health.dart';
import 'package:betteronlyyours/state/settings_controller.dart';
import 'package:betteronlyyours/state/toast_controller.dart';
import 'package:betteronlyyours/state/vault_controller.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/in_memory_vault_store.dart';
import 'support/memory_settings_service.dart';

VaultEntry entry(String title, {String password = '', String notes = ''}) =>
    VaultEntry.create(title).copyWith(password: password, notes: notes);

void main() {
  group('VaultHealth', () {
    test('an empty vault is clean', () {
      final report = VaultHealth.analyze(const <VaultEntry>[]);
      expect(report.isClean, isTrue);
      expect(report.score, 100);
      expect(report.withPassword, 0);
    });

    test('groups entries that share a password', () {
      final report = VaultHealth.analyze(<VaultEntry>[
        entry('GitHub', password: r'Sh4red#Secret!42'),
        entry('GitLab', password: r'Sh4red#Secret!42'),
        entry('Bank', password: r'Unique#Value!77x'),
      ]);

      expect(report.reused.length, 1);
      expect(report.reused.single.count, 2);
      expect(report.reused.single.titles, <String>['GitHub', 'GitLab']);
      expect(report.reusedEntryCount, 2);
      expect(report.isReused('GitHub'), isTrue);
      expect(report.isReused('Bank'), isFalse);
    });

    test('reused groups are ordered worst first', () {
      final report = VaultHealth.analyze(<VaultEntry>[
        entry('A', password: r'K9#mQ2vR!7wZx4Lp'),
        entry('B', password: r'K9#mQ2vR!7wZx4Lp'),
        entry('C', password: '123456'),
        entry('D', password: '123456'),
      ]);

      expect(report.reused.length, 2);
      expect(report.reused.first.titles, <String>['C', 'D']);
      expect(
        report.reused.first.strength.level,
        anyOf(StrengthLevel.veryWeak, StrengthLevel.weak),
      );
    });

    test('weak passwords are listed and sorted by title', () {
      final report = VaultHealth.analyze(<VaultEntry>[
        entry('Zeta', password: 'abc'),
        entry('Alpha', password: 'password'),
        entry('Strong', password: r'9Tz#vQ2m!Lk8@Rd4^Ws1&Xp7'),
      ]);

      expect(report.weak.map((e) => e.title), <String>['Alpha', 'Zeta']);
      expect(report.withPassword, 3);
    });

    test('structured entries without a password are reported separately', () {
      final report = VaultHealth.analyze(<VaultEntry>[
        entry('No password', notes: 'just notes'),
        VaultEntry.fromStorage('Legacy', 'free text from an old vault'),
      ]);

      // The structured one counts, the legacy note-only entry does not.
      expect(report.withoutPassword.map((e) => e.title), <String>[
        'No password',
      ]);
      expect(report.withPassword, 0);
    });

    test('score drops with problems and stays inside 0..100', () {
      final clean = VaultHealth.analyze(<VaultEntry>[
        entry('A', password: r'9Tz#vQ2m!Lk8@Rd4^Ws1&Xp7'),
      ]);
      final messy = VaultHealth.analyze(<VaultEntry>[
        entry('A', password: '123456'),
        entry('B', password: '123456'),
        entry('C', password: 'password'),
      ]);

      expect(clean.score, 100);
      expect(clean.isClean, isTrue);
      expect(messy.score, lessThan(50));
      expect(messy.score, inInclusiveRange(0, 100));
      expect(messy.issueCount, greaterThan(0));
    });

    test('identical passwords are grouped without exposing them', () {
      final report = VaultHealth.analyze(<VaultEntry>[
        entry('A', password: 'shared-secret'),
        entry('B', password: 'shared-secret'),
      ]);

      // The report holds entries, never a map keyed by the secret itself.
      expect(report.reused.single.entries.length, 2);
      expect(report.reusedTitles, containsAll(<String>['A', 'B']));
    });

    test('analysis is stable across runs despite the random digest key', () {
      final entries = <VaultEntry>[
        entry('A', password: 'same'),
        entry('B', password: 'same'),
        entry('C', password: 'other'),
      ];
      final first = VaultHealth.analyze(entries);
      final second = VaultHealth.analyze(entries);

      expect(first.reused.single.titles, second.reused.single.titles);
      expect(first.score, second.score);
    });
  });

  group('VaultController health', () {
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
    });

    tearDown(() {
      vault.dispose();
      settings.dispose();
      toasts.dispose();
    });

    test('the report is cached until something changes', () async {
      await vault.upsertEntry(entry('A', password: 'shared'));
      await vault.upsertEntry(entry('B', password: 'shared'));

      final first = vault.health;
      expect(identical(vault.health, first), isTrue, reason: 'cached');
      expect(first.reusedEntryCount, 2);

      await vault.upsertEntry(entry('B', password: 'now unique'));
      expect(identical(vault.health, first), isFalse, reason: 'invalidated');
      expect(vault.health.reusedEntryCount, 0);
    });

    test('locking clears the report', () async {
      await vault.upsertEntry(entry('A', password: 'shared'));
      await vault.upsertEntry(entry('B', password: 'shared'));
      expect(vault.health.reusedEntryCount, 2);

      vault.lock();
      expect(vault.health.isClean, isTrue);
      expect(vault.health.entryCount, 0);
    });
  });
}
