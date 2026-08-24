import 'dart:async';

import 'package:material_ui/material_ui.dart';
import 'package:hotkey_manager/hotkey_manager.dart';
import 'package:provider/provider.dart';
import 'package:window_manager/window_manager.dart';

import 'app/app.dart';
import 'app/app_info.dart';
import 'app/hotkey_service.dart';
import 'app/window_controller.dart';
import 'core/models/app_settings.dart';
import 'core/services/clipboard_service.dart';
import 'state/settings_controller.dart';
import 'state/shell_controller.dart';
import 'state/toast_controller.dart';
import 'state/vault_controller.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await windowManager.ensureInitialized();
  await hotKeyManager.unregisterAll();

  // Preferences are read synchronously: the theme is needed for the first
  // frame, and start-up must not wait on anything that needs the platform
  // message loop before runApp().
  final settings = SettingsController()..load();

  final toasts = ToastController();
  final shell = ShellController();
  final clipboard = ClipboardService();
  final vault = VaultController(toasts: toasts, settings: settings);
  final window = WindowController(settings: settings);
  final hotkeys = HotkeyService(shell: shell, vault: vault);

  final storedBounds = settings.settings.rememberWindowBounds
      ? settings.settings.windowBounds
      : null;

  final windowOptions = WindowOptions(
    size: storedBounds == null
        ? const Size(1180, 780)
        : Size(storedBounds.width, storedBounds.height),
    minimumSize: const Size(720, 520),
    center: storedBounds == null,
    backgroundColor: const Color(0x00000000),
    skipTaskbar: false,
    titleBarStyle: TitleBarStyle.hidden,
    title: AppInfo.name,
  );

  // Not awaited: the callback runs when the window is ready, in parallel with
  // the first frame.
  unawaited(
    windowManager.waitUntilReadyToShow(windowOptions, () async {
      if (storedBounds != null && _boundsLookSane(storedBounds)) {
        await windowManager.setBounds(
          Rect.fromLTWH(
            storedBounds.left,
            storedBounds.top,
            storedBounds.width,
            storedBounds.height,
          ),
        );
      }
      await windowManager.show();
      await windowManager.focus();
      if (storedBounds?.maximized ?? false) {
        await windowManager.maximize();
      }
    }),
  );

  runApp(
    MultiProvider(
      providers: [
        ChangeNotifierProvider<SettingsController>.value(value: settings),
        ChangeNotifierProvider<ToastController>.value(value: toasts),
        ChangeNotifierProvider<ShellController>.value(value: shell),
        ChangeNotifierProvider<ClipboardService>.value(value: clipboard),
        ChangeNotifierProvider<VaultController>.value(value: vault),
        ChangeNotifierProvider<WindowController>.value(value: window),
        Provider<HotkeyService>.value(value: hotkeys),
      ],
      child: const BetterOnlyYoursApp(),
    ),
  );

  // Everything that talks to the platform happens after the first frame.
  WidgetsBinding.instance.addPostFrameCallback((_) async {
    await window.initialize();

    // Flush usage metadata and preferences before the process goes away, then
    // hand the system-wide shortcut back: a hotkey still registered makes
    // Windows wait on the process during teardown.
    window.onBeforeClose = () async {
      await vault.flushPendingWrites();
      await settings.flush();
      await hotkeys.unregister();
    };

    unawaited(vault.initialize());
    if (settings.settings.globalHotkeyEnabled) {
      unawaited(hotkeys.register());
    }
  });
}

/// Guards against restoring a window onto a monitor that no longer exists.
bool _boundsLookSane(WindowBounds bounds) {
  return bounds.left > -8000 &&
      bounds.top > -8000 &&
      bounds.left < 20000 &&
      bounds.top < 20000;
}
