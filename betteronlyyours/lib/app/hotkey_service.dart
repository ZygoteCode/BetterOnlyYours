import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:hotkey_manager/hotkey_manager.dart';
import 'package:window_manager/window_manager.dart';

import '../state/shell_controller.dart';
import '../state/vault_controller.dart';

/// System-wide shortcut (Ctrl+Alt+P) that brings the window forward and opens
/// the command palette — after unlocking, when the vault is locked.
///
/// Registration failure is never fatal: the app keeps working and Settings
/// shows what happened with a retry.
class HotkeyService {
  HotkeyService({
    required ShellController shell,
    required VaultController vault,
  }) : _shell = shell,
       _vault = vault;

  final ShellController _shell;
  final VaultController _vault;

  static final HotKey _paletteHotKey = HotKey(
    key: PhysicalKeyboardKey.keyP,
    modifiers: <HotKeyModifier>[HotKeyModifier.control, HotKeyModifier.alt],
    scope: HotKeyScope.system,
  );

  bool _registered = false;

  Future<void> apply({required bool enabled}) async {
    if (!enabled) {
      await unregister();
      _shell.setHotkeyStatus(registered: false);
      return;
    }
    await register();
  }

  Future<void> register() async {
    try {
      await hotKeyManager.unregisterAll();
      await hotKeyManager.register(
        _paletteHotKey,
        keyDownHandler: (_) => _onHotKey(),
      );
      _registered = true;
      _shell.setHotkeyStatus(registered: true);
    } catch (error) {
      _registered = false;
      _shell.setHotkeyStatus(
        registered: false,
        error:
            'Ctrl+Alt+P could not be registered — another application is '
            'probably using it.',
      );
      debugPrint('Global hotkey registration failed: $error');
    }
  }

  Future<void> unregister() async {
    if (!_registered) return;
    try {
      await hotKeyManager.unregisterAll();
    } catch (_) {
      // Nothing actionable: the shortcut simply stays registered.
    }
    _registered = false;
  }

  Future<void> _onHotKey() async {
    try {
      if (await windowManager.isMinimized()) {
        await windowManager.restore();
      }
      await windowManager.show();
      await windowManager.focus();
    } catch (error) {
      debugPrint('Could not bring the window forward: $error');
    }

    if (_vault.isUnlocked) {
      _shell.openCommandPalette();
    } else {
      _shell.requestPaletteAfterUnlock();
    }
  }
}
