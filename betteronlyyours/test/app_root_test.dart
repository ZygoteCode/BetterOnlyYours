import 'package:betteronlyyours/app/app.dart';
import 'package:betteronlyyours/app/window_controller.dart';
import 'package:betteronlyyours/core/models/vault_entry.dart';
import 'package:betteronlyyours/core/services/clipboard_service.dart';
import 'package:betteronlyyours/features/auth/create_vault_screen.dart';
import 'package:betteronlyyours/features/auth/lock_screen.dart';
import 'package:betteronlyyours/features/shell/app_shell.dart';
import 'package:betteronlyyours/features/shell/title_bar.dart';
import 'package:betteronlyyours/state/settings_controller.dart';
import 'package:betteronlyyours/state/shell_controller.dart';
import 'package:betteronlyyours/state/toast_controller.dart';
import 'package:betteronlyyours/state/vault_controller.dart';
import 'package:material_ui/material_ui.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'support/in_memory_vault_store.dart';
import 'support/memory_settings_service.dart';

/// Renders the real application root — the widget `main()` hands to runApp —
/// and asserts the interface actually occupies the window.
///
/// Regression guard: the root Stack once contained only positioned children,
/// so under the Scaffold's loose constraints it collapsed to 0x0 and clipped
/// the whole app. Every screen still "built", only nothing was painted.
void main() {
  late InMemoryVaultStore store;
  late VaultController vault;
  late SettingsController settings;
  late ShellController shell;
  late ToastController toasts;
  late ClipboardService clipboard;
  late WindowController window;

  setUp(() {
    store = InMemoryVaultStore();
    settings = SettingsController(service: MemorySettingsService())..load();
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
  });

  tearDown(() {
    vault.dispose();
    settings.dispose();
    shell.dispose();
    toasts.dispose();
    clipboard.dispose();
    window.dispose();
  });

  Future<void> pumpApp(
    WidgetTester tester, {
    Size size = const Size(1180, 780),
  }) async {
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
        child: const BetterOnlyYoursApp(),
      ),
    );
    for (var i = 0; i < 8; i++) {
      await tester.pump(const Duration(milliseconds: 16));
    }
  }

  void expectFillsWindow(WidgetTester tester, Finder finder, Size window) {
    expect(finder, findsOneWidget);
    final size = tester.getSize(finder);
    expect(
      size.width,
      greaterThan(window.width * 0.5),
      reason: 'content collapsed horizontally',
    );
    expect(
      size.height,
      greaterThan(window.height * 0.5),
      reason: 'content collapsed vertically',
    );
  }

  testWidgets('first run paints the vault-creation screen', (tester) async {
    await vault.initialize();
    await pumpApp(tester);

    expect(vault.status, VaultStatus.missing);
    expectFillsWindow(
      tester,
      find.byType(CreateVaultScreen),
      const Size(1180, 780),
    );
    expect(find.byType(AppTitleBar), findsOneWidget);
    expect(find.text('One password, one file, no cloud'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('an existing vault paints the lock screen', (tester) async {
    store.seed('master password', <String, String>{'Bank': 'notes'});
    await vault.initialize();
    await pumpApp(tester);

    expect(vault.status, VaultStatus.locked);
    expectFillsWindow(tester, find.byType(LockScreen), const Size(1180, 780));
    expect(find.text('Vault locked'), findsOneWidget);
    expect(find.text('Unlock vault'), findsOneWidget);
  });

  testWidgets('an unlocked vault paints the shell', (tester) async {
    store.seed('master password', <String, String>{
      'GitHub': VaultEntry.create('GitHub')
          .copyWith(username: 'octocat')
          .toStorageValue(),
    });
    await vault.initialize();
    await vault.unlock('master password');
    await pumpApp(tester);

    expect(vault.status, VaultStatus.unlocked);
    expectFillsWindow(tester, find.byType(AppShell), const Size(1180, 780));
    expect(find.text('GitHub'), findsWidgets);
    expect(tester.takeException(), isNull);
  });

  testWidgets('the root stack keeps its size at the minimum window size', (
    tester,
  ) async {
    await vault.initialize();
    await pumpApp(tester, size: const Size(720, 520));

    expectFillsWindow(
      tester,
      find.byType(CreateVaultScreen),
      const Size(720, 520),
    );
  });
}
