import 'package:betteronlyyours/app/theme/app_theme.dart';
import 'package:betteronlyyours/app/window_controller.dart';
import 'package:betteronlyyours/core/models/vault_entry.dart';
import 'package:betteronlyyours/core/services/clipboard_service.dart';
import 'package:betteronlyyours/features/search/command_palette.dart';
import 'package:betteronlyyours/features/shell/app_shell.dart';
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

/// Every navigation destination must render at the smallest supported
/// window without overflowing.
/// Bounded frame pumping. `pumpAndSettle` is avoided on purpose: entrance
/// animations schedule their own delayed callbacks, which makes "settled"
/// timing-dependent and flaky under parallel test runs.
Future<void> pumpFrames(WidgetTester tester, [int frames = 8]) async {
  for (var i = 0; i < frames; i++) {
    await tester.pump(const Duration(milliseconds: 16));
  }
}

void main() {
  late InMemoryVaultStore store;
  late VaultController vault;
  late SettingsController settings;
  late ShellController shell;
  late ToastController toasts;
  late ClipboardService clipboard;
  late WindowController window;

  Future<void> buildControllers() async {
    store = InMemoryVaultStore();
    settings = SettingsController(service: MemorySettingsService());
    settings.load();
    // Layout, not motion, is what these tests assert; disabling animations
    // keeps every pump deterministic.
    settings.setReduceMotion(true);
    toasts = ToastController();
    shell = ShellController();
    clipboard = ClipboardService();
    window = WindowController(settings: settings);
    vault = VaultController(
      toasts: toasts,
      settings: settings,
      repository: store,
    );
  }

  /// Seeds storage directly and unlocks from inside the test body, so no
  /// write chain is started under fake async.
  Future<void> seedEntries({int entryCount = 6}) async {
    store.seed('layout test password', <String, String>{
      for (var i = 0; i < entryCount; i++)
        'Entry number $i': VaultEntry.create('Entry number $i')
            .copyWith(
              username: 'user$i@example.com',
              password: 'p4ssword-$i-with-length',
              url: 'https://service-$i.example.com',
              notes: 'Notes for entry $i',
              tags: <String>[i.isEven ? 'work' : 'personal'],
              favorite: i == 0,
            )
            .toStorageValue(),
    });
    await vault.unlock('layout test password');
  }

  Future<void> tearDownVault() async {
    // No flush: a write started inside the fake-async zone cannot resume once
    // the test body is over.
    vault.dispose();
    settings.dispose();
    shell.dispose();
    toasts.dispose();
    clipboard.dispose();
    window.dispose();
  }

  Future<void> pumpShell(WidgetTester tester, Size size) async {
    if (vault.status != VaultStatus.unlocked) await seedEntries();
    tester.view.physicalSize = size;
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
          theme: AppTheme.build(settings.settings),
          locale: const Locale('en'),
          supportedLocales: appSupportedLocales,
          localizationsDelegates: appLocalizationsDelegates,
          home: Scaffold(
            body: Stack(
              children: <Widget>[
                AppShell(searchFocusNode: FocusNode()),
                if (shell.commandPaletteOpen) const CommandPalette(),
              ],
            ),
          ),
        ),
      ),
    );
    await pumpFrames(tester);
  }

  setUp(() async => buildControllers());
  tearDown(() async => tearDownVault());

  for (final destination in ShellDestination.values) {
    testWidgets('${destination.label} renders at a small window', (
      tester,
    ) async {
      shell.goTo(destination);
      await pumpShell(tester, const Size(900, 600));
      expect(
        tester.takeException(),
        isNull,
        reason: '${destination.label} overflowed',
      );
    });
  }
}
