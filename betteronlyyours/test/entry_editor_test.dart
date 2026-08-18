import 'package:betteronlyyours/app/theme/app_theme.dart';
import 'package:betteronlyyours/app/window_controller.dart';
import 'package:betteronlyyours/core/models/vault_entry.dart';
import 'package:betteronlyyours/core/services/clipboard_service.dart';
import 'package:betteronlyyours/features/vault/entry_detail_panel.dart';
import 'package:betteronlyyours/state/settings_controller.dart';
import 'package:betteronlyyours/state/shell_controller.dart';
import 'package:betteronlyyours/state/toast_controller.dart';
import 'package:betteronlyyours/state/vault_controller.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'support/in_memory_vault_store.dart';
import 'support/memory_settings_service.dart';

/// Editor behaviour that lives in the widget layer: dirty tracking, reverting,
/// rename validation and the unsaved-changes prompt.
///
/// Persistence itself is covered by `vault_controller_test` and
/// `vault_repository_test`, which drive the real encrypted store.
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

  setUp(() async {
    store = InMemoryVaultStore();
    store.seed('editor test password', <String, String>{
      'GitHub': VaultEntry.create(
        'GitHub',
      ).copyWith(username: 'octocat').toStorageValue(),
      'Bank': VaultEntry.create(
        'Bank',
      ).copyWith(username: 'me').toStorageValue(),
    });
    settings = SettingsController(service: MemorySettingsService());
    settings.load();
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

  Future<void> pumpEditor(WidgetTester tester, String title) async {
    // Unlocking happens inside the test body so every future belongs to the
    // zone the widget test drives.
    await vault.unlock('editor test password');
    vault.select(title, markAsUsed: false);

    tester.view.physicalSize = const Size(1100, 900);
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
          home: Scaffold(
            body: Consumer<VaultController>(
              builder: (context, controller, _) => EntryDetailPanel(
                entry: controller.entry(controller.selectedTitle ?? title)!,
              ),
            ),
          ),
        ),
      ),
    );
    await pumpFrames(tester);
  }

  testWidgets('editing an entry raises the unsaved-changes banner', (
    tester,
  ) async {
    await pumpEditor(tester, 'GitHub');

    expect(find.text('Unsaved changes'), findsNothing);
    expect(find.text('Save'), findsNothing);

    await tester.enterText(
      find.widgetWithText(TextField, 'octocat'),
      'octocat-renamed',
    );
    await pumpFrames(tester);

    expect(find.text('Unsaved changes'), findsOneWidget);
    expect(find.text('Save'), findsOneWidget);
    expect(find.text('Revert'), findsOneWidget);
  });

  testWidgets('reverting drops the pending edit', (tester) async {
    await pumpEditor(tester, 'GitHub');

    await tester.enterText(
      find.widgetWithText(TextField, 'octocat'),
      'temporary',
    );
    await pumpFrames(tester);
    expect(find.text('Unsaved changes'), findsOneWidget);

    await tester.tap(find.text('Revert'));
    await pumpFrames(tester);

    expect(find.text('Unsaved changes'), findsNothing);
    expect(vault.entry('GitHub')!.username, 'octocat');
    expect(find.widgetWithText(TextField, 'octocat'), findsOneWidget);
  });

  testWidgets('switching entries with pending edits asks what to do', (
    tester,
  ) async {
    await pumpEditor(tester, 'GitHub');

    await tester.enterText(
      find.widgetWithText(TextField, 'octocat'),
      'pending change',
    );
    await pumpFrames(tester);

    vault.select('Bank', markAsUsed: false);
    // Bounded pumps: the dialog blurs its backdrop, which never "settles".
    await pumpFrames(tester, 12);

    expect(find.text('Unsaved changes'), findsOneWidget);
    expect(find.text('Save changes'), findsOneWidget);
    expect(find.text('Discard'), findsOneWidget);

    await tester.tap(find.text('Discard'));
    await pumpFrames(tester, 8);
    toasts.clear();
    await tester.pump();

    expect(vault.entry('GitHub')!.username, 'octocat');
    expect(store.writeCount, 0, reason: 'discarding must not write');
  });

  testWidgets('renaming to an existing name is refused before saving', (
    tester,
  ) async {
    await pumpEditor(tester, 'GitHub');

    await tester.enterText(find.widgetWithText(TextField, 'GitHub'), 'Bank');
    await pumpFrames(tester);
    await tester.tap(find.text('Save'));
    await pumpFrames(tester);

    expect(find.textContaining('already uses that name'), findsOneWidget);
    expect(vault.hasEntry('GitHub'), isTrue);
    expect(store.writeCount, 0, reason: 'nothing should have been written');
  });

  testWidgets('an empty name is refused', (tester) async {
    await pumpEditor(tester, 'GitHub');

    await tester.enterText(find.widgetWithText(TextField, 'GitHub'), '   ');
    await pumpFrames(tester);
    // A blank name is not treated as an edit, so submit the field directly.
    await tester.testTextInput.receiveAction(TextInputAction.done);
    await pumpFrames(tester);

    expect(find.textContaining('name is required'), findsOneWidget);
    expect(store.writeCount, 0);
  });

  testWidgets('legacy entries explain their format and keep their text', (
    tester,
  ) async {
    store.seed('editor test password', <String, String>{
      'Old server': 'root / toor',
    });
    await pumpEditor(tester, 'Old server');

    expect(find.textContaining('older vault'), findsOneWidget);
    expect(find.widgetWithText(TextField, 'root / toor'), findsOneWidget);
  });
}
