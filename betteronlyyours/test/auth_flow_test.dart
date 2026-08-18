import 'package:betteronlyyours/app/theme/app_theme.dart';
import 'package:betteronlyyours/app/window_controller.dart';
import 'package:betteronlyyours/features/auth/create_vault_screen.dart';
import 'package:betteronlyyours/features/auth/lock_screen.dart';
import 'package:betteronlyyours/shared/widgets/app_button.dart';
import 'package:betteronlyyours/state/settings_controller.dart';
import 'package:betteronlyyours/state/shell_controller.dart';
import 'package:betteronlyyours/state/toast_controller.dart';
import 'package:betteronlyyours/state/vault_controller.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'support/in_memory_vault_store.dart';
import 'support/memory_settings_service.dart';

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
  late WindowController window;

  setUp(() async {
    store = InMemoryVaultStore();
    settings = SettingsController(service: MemorySettingsService());
    settings.load();
    settings.setReduceMotion(true);
    toasts = ToastController();
    shell = ShellController();
    window = WindowController(settings: settings);
    vault = VaultController(
      toasts: toasts,
      settings: settings,
      repository: store,
    );
  });

  tearDown(() {
    vault.dispose();
    settings.dispose();
    shell.dispose();
    toasts.dispose();
    window.dispose();
  });

  Future<void> pump(WidgetTester tester, Widget child, Size size) async {
    tester.view.physicalSize = size;
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      MultiProvider(
        providers: [
          ChangeNotifierProvider<SettingsController>.value(value: settings),
          ChangeNotifierProvider<ToastController>.value(value: toasts),
          ChangeNotifierProvider<ShellController>.value(value: shell),
          ChangeNotifierProvider<VaultController>.value(value: vault),
          ChangeNotifierProvider<WindowController>.value(value: window),
        ],
        child: MaterialApp(
          theme: AppTheme.build(settings.settings),
          home: Scaffold(body: child),
        ),
      ),
    );
    await pumpFrames(tester);
  }

  /// Drains the work a button press kicked off. Plain pumps rather than
  /// pumpAndSettle, because a busy button shows an endless progress spinner.
  Future<void> settle(WidgetTester tester) async {
    await pumpFrames(tester);
    toasts.clear();
    await tester.pump();
  }

  testWidgets('vault creation screen fits small and large windows', (
    tester,
  ) async {
    for (final size in const <Size>[Size(900, 600), Size(1600, 1000)]) {
      await pump(tester, const CreateVaultScreen(), size);
      expect(tester.takeException(), isNull);
      expect(find.text('Create vault'), findsOneWidget);
    }
  });

  testWidgets('vault creation validates length, match and acknowledgement', (
    tester,
  ) async {
    await pump(tester, const CreateVaultScreen(), const Size(1000, 900));

    final fields = find.byType(TextField);
    await tester.enterText(fields.at(0), 'short');
    await tester.enterText(fields.at(1), 'short');
    await tester.tap(find.text('Create vault'));
    await pumpFrames(tester);
    expect(find.textContaining('at least 10 characters'), findsWidgets);
    expect(store.hasVault, isFalse);

    await tester.enterText(fields.at(0), 'a long enough password');
    await tester.enterText(fields.at(1), 'a different password');
    await tester.tap(find.text('Create vault'));
    await pumpFrames(tester);
    expect(find.textContaining('do not match'), findsOneWidget);

    await tester.enterText(fields.at(1), 'a long enough password');
    await tester.tap(find.text('Create vault'));
    await pumpFrames(tester);
    expect(find.textContaining('no password recovery'), findsOneWidget);
    expect(store.hasVault, isFalse);

    await tester.tap(find.byType(Checkbox));
    await pumpFrames(tester);
    await tester.tap(find.text('Create vault'));
    await settle(tester);

    expect(vault.status, VaultStatus.unlocked);
    expect(store.hasVault, isTrue);
  });

  testWidgets('lock screen rejects a wrong password and then unlocks', (
    tester,
  ) async {
    store.seed('the real password', <String, String>{'Bank': 'notes'});
    await vault.initialize();
    expect(vault.status, VaultStatus.locked);

    await pump(tester, const LockScreen(), const Size(1000, 800));

    await tester.enterText(find.byType(TextField).first, 'wrong password');
    await tester.tap(find.text('Unlock vault'));
    await settle(tester);

    expect(vault.status, VaultStatus.locked);
    expect(find.text('Master password not accepted'), findsOneWidget);

    await tester.enterText(find.byType(TextField).first, 'the real password');
    await tester.tap(find.text('Unlock vault'));
    await settle(tester);

    expect(vault.status, VaultStatus.unlocked);
    expect(vault.entryCount, 1);
  });

  testWidgets('unlocking honours a palette request made while locked', (
    tester,
  ) async {
    store.seed('open sesame please', <String, String>{});
    await vault.initialize();
    shell.requestPaletteAfterUnlock();

    await pump(tester, const LockScreen(), const Size(1000, 800));
    await tester.enterText(find.byType(TextField).first, 'open sesame please');
    await tester.tap(find.byType(AppButton).first);
    await settle(tester);

    expect(vault.status, VaultStatus.unlocked);
    expect(shell.commandPaletteOpen, isTrue);
  });

  testWidgets('a vault that does not exist yet leads to creation', (
    tester,
  ) async {
    await vault.initialize();
    expect(vault.status, VaultStatus.missing);
  });
}
