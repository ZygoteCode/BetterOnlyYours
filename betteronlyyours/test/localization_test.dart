import 'dart:convert';
import 'dart:io';

import 'package:betteronlyyours/app/theme/app_theme.dart';
import 'package:betteronlyyours/app/window_controller.dart';
import 'package:betteronlyyours/core/models/app_settings.dart';
import 'package:betteronlyyours/core/security/vault_exception.dart';
import 'package:betteronlyyours/core/services/clipboard_service.dart';
import 'package:betteronlyyours/core/services/password_strength.dart';
import 'package:betteronlyyours/features/auth/lock_screen.dart';
import 'package:betteronlyyours/l10n/l10n.dart';
import 'package:betteronlyyours/state/settings_controller.dart';
import 'package:betteronlyyours/state/shell_controller.dart';
import 'package:betteronlyyours/state/toast_controller.dart';
import 'package:betteronlyyours/state/vault_controller.dart';
import 'package:material_ui/material_ui.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'support/in_memory_vault_store.dart';
import 'support/memory_settings_service.dart';

Future<void> pumpFrames(WidgetTester tester, [int frames = 8]) async {
  for (var i = 0; i < frames; i++) {
    await tester.pump(const Duration(milliseconds: 16));
  }
}

void main() {
  group('ARB catalogues', () {
    Map<String, dynamic> read(String locale) {
      final file = File('lib/l10n/app_$locale.arb');
      expect(file.existsSync(), isTrue, reason: '${file.path} must exist');
      return jsonDecode(file.readAsStringSync()) as Map<String, dynamic>;
    }

    test('English and Italian define exactly the same messages', () {
      final en = read('en');
      final it = read('it');

      final enKeys = en.keys.where((k) => !k.startsWith('@')).toSet();
      final itKeys = it.keys.where((k) => !k.startsWith('@')).toSet();

      expect(
        enKeys.difference(itKeys),
        isEmpty,
        reason: 'missing Italian translations',
      );
      expect(
        itKeys.difference(enKeys),
        isEmpty,
        reason: 'Italian has keys English does not',
      );
      expect(enKeys.length, greaterThan(300));
    });

    test('no Italian message was left identical to a long English one', () {
      final en = read('en');
      final it = read('it');

      final untranslated = <String>[];
      for (final key in en.keys.where((k) => !k.startsWith('@'))) {
        final english = en[key];
        final italian = it[key];
        if (english is! String || italian is! String) continue;
        // Short labels legitimately match (Password, Nonce, Tag names...).
        if (english.length < 25) continue;
        if (english == italian) untranslated.add(key);
      }

      expect(untranslated, isEmpty);
    });

    test('placeholders are declared for every parameterised message', () {
      final en = read('en');
      final pattern = RegExp(r'\{(\w+)[,}]');

      for (final key in en.keys.where((k) => !k.startsWith('@'))) {
        final value = en[key];
        if (value is! String) continue;
        final names = pattern.allMatches(value).map((m) => m.group(1)!).toSet();
        if (names.isEmpty) continue;

        final meta = en['@$key'];
        expect(meta, isA<Map<String, dynamic>>(), reason: 'metadata for $key');
        final declared =
            ((meta as Map<String, dynamic>)['placeholders']
                    as Map<String, dynamic>?)
                ?.keys
                .toSet() ??
            <String>{};
        expect(names.difference(declared), isEmpty, reason: 'in $key');
      }
    });
  });

  group('generated localisations', () {
    testWidgets('English and Italian resolve different strings', (
      tester,
    ) async {
      late AppLocalizations english;
      late AppLocalizations italian;

      Future<void> pump(Locale locale, void Function(AppLocalizations) sink) {
        return tester.pumpWidget(
          MaterialApp(
            locale: locale,
            supportedLocales: appSupportedLocales,
            localizationsDelegates: appLocalizationsDelegates,
            home: Builder(
              builder: (context) {
                sink(context.l10n);
                return const SizedBox.shrink();
              },
            ),
          ),
        );
      }

      await pump(const Locale('en'), (l) => english = l);
      await pumpFrames(tester);
      await pump(const Locale('it'), (l) => italian = l);
      await pumpFrames(tester);

      expect(english.authVaultLocked, 'Vault locked');
      expect(italian.authVaultLocked, 'Cassaforte bloccata');
      expect(italian.navSettings, 'Impostazioni');
      expect(italian.entriesCount(1), '1 voce');
      expect(italian.entriesCount(4), '4 voci');
      expect(english.entriesCount(4), '4 entries');
    });

    testWidgets('relative time and enum labels follow the locale', (
      tester,
    ) async {
      late AppLocalizations italian;
      await tester.pumpWidget(
        MaterialApp(
          locale: const Locale('it'),
          supportedLocales: appSupportedLocales,
          localizationsDelegates: appLocalizationsDelegates,
          home: Builder(
            builder: (context) {
              italian = context.l10n;
              return const SizedBox.shrink();
            },
          ),
        ),
      );
      await pumpFrames(tester);

      final now = DateTime(2026, 8, 20, 12);
      expect(
        formatRelativeTime(
          italian,
          now.subtract(const Duration(minutes: 5)),
          now: now,
        ),
        '5 minuti fa',
      );
      expect(formatRelativeTime(italian, null), 'mai');
      expect(
        formatRelativeTime(
          italian,
          now.subtract(const Duration(hours: 1)),
          now: now,
        ),
        '1 ora fa',
      );

      expect(ShellDestination.settings.localizedLabel(italian), 'Impostazioni');
      expect(StrengthLevel.strong.localizedLabel(italian), 'Robusta');
      expect(AppLanguage.italian.localizedLabel(italian), 'Italiano');
      expect(
        const VaultException(
          VaultErrorKind.invalidPassword,
          'x',
        ).localizedTitle(italian),
        'Password principale non accettata',
      );
    });

    /// Regression guard: the generated `AppLocalizations.localizationsDelegates`
    /// carries `flutter_localizations`' material delegate, which localises the
    /// SDK's legacy material library. Under `material_ui` that delegate is
    /// invisible, so every widget requiring `MaterialLocalizations` threw
    /// outside English.
    testWidgets('material_ui widgets find localisations in every locale', (
      tester,
    ) async {
      for (final locale in appSupportedLocales) {
        late MaterialLocalizations material;
        await tester.pumpWidget(
          MaterialApp(
            locale: locale,
            supportedLocales: appSupportedLocales,
            localizationsDelegates: appLocalizationsDelegates,
            home: Builder(
              builder: (context) {
                material = MaterialLocalizations.of(context);
                return const Scaffold(body: TextField());
              },
            ),
          ),
        );
        await pumpFrames(tester);

        expect(tester.takeException(), isNull, reason: '$locale');
        expect(material.cancelButtonLabel, isNotEmpty);
      }
    });

    testWidgets('material strings follow the app locale', (tester) async {
      late MaterialLocalizations material;
      await tester.pumpWidget(
        MaterialApp(
          locale: const Locale('it'),
          supportedLocales: appSupportedLocales,
          localizationsDelegates: appLocalizationsDelegates,
          home: Builder(
            builder: (context) {
              material = MaterialLocalizations.of(context);
              return const SizedBox.shrink();
            },
          ),
        ),
      );
      await pumpFrames(tester);

      expect(material.cancelButtonLabel, 'Annulla');
    });
  });

  group('language preference', () {
    test('defaults to the system locale and round-trips', () {
      const settings = AppSettings();
      expect(settings.language, AppLanguage.system);
      expect(settings.language.locale, isNull);

      final parsed = AppSettings.fromJson(
        const AppSettings(language: AppLanguage.italian).toJson(),
      );
      expect(parsed.language, AppLanguage.italian);
      expect(parsed.language.locale, const Locale('it'));
    });

    test('an unknown language falls back to system', () {
      final parsed = AppSettings.fromJson(<String, dynamic>{
        'language': 'klingon',
      });
      expect(parsed.language, AppLanguage.system);
    });
  });

  group('localised screens', () {
    testWidgets('the lock screen renders in Italian', (tester) async {
      final store = InMemoryVaultStore()
        ..seed('master password', <String, String>{'Bank': 'notes'});
      final settings = SettingsController(service: MemorySettingsService())
        ..load();
      settings.setReduceMotion(true);
      settings.setLanguage(AppLanguage.italian);
      final toasts = ToastController();
      final shell = ShellController();
      final clipboard = ClipboardService();
      final window = WindowController(settings: settings);
      final vault = VaultController(
        toasts: toasts,
        settings: settings,
        repository: store,
      );
      await vault.initialize();

      addTearDown(() {
        vault.dispose();
        settings.dispose();
        toasts.dispose();
        shell.dispose();
        clipboard.dispose();
        window.dispose();
      });

      tester.view.physicalSize = const Size(1100, 800);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.reset);

      await tester.pumpWidget(
        MultiProvider(
          providers: [
            ChangeNotifierProvider<SettingsController>.value(value: settings),
            ChangeNotifierProvider<ToastController>.value(value: toasts),
            ChangeNotifierProvider<ShellController>.value(value: shell),
            ChangeNotifierProvider<ClipboardService>.value(value: clipboard),
            ChangeNotifierProvider<VaultController>.value(value: vault),
            ChangeNotifierProvider<WindowController>.value(value: window),
          ],
          child: MaterialApp(
            locale: settings.settings.language.locale,
            supportedLocales: appSupportedLocales,
            localizationsDelegates: appLocalizationsDelegates,
            theme: AppTheme.build(settings.settings),
            home: const Scaffold(body: LockScreen()),
          ),
        ),
      );
      await pumpFrames(tester);

      expect(find.text('Cassaforte bloccata'), findsOneWidget);
      expect(find.text('Sblocca cassaforte'), findsOneWidget);
      expect(find.text('Vault locked'), findsNothing);
    });
  });
}
