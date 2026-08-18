import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:window_manager/window_manager.dart';

import '../core/models/app_settings.dart';
import '../state/settings_controller.dart';

/// Owns desktop window state.
///
/// Minimising, blurring or maximising the window deliberately has **no**
/// effect on the vault session: the user can copy a secret, alt-tab, paste it
/// and come back to an unlocked vault.
class WindowController extends ChangeNotifier with WindowListener {
  WindowController({required SettingsController settings})
    : _settings = settings;

  final SettingsController _settings;

  bool _isMaximized = false;
  bool _isFocused = true;
  bool _closing = false;
  Timer? _boundsDebounce;

  /// Runs before the window is destroyed, so pending writes can be flushed.
  Future<void> Function()? onBeforeClose;

  bool get isMaximized => _isMaximized;
  bool get isFocused => _isFocused;

  Future<void> initialize() async {
    windowManager.addListener(this);
    try {
      await windowManager.setPreventClose(true);
      _isMaximized = await windowManager.isMaximized();
    } catch (_) {
      _isMaximized = false;
    }
    notifyListeners();
  }

  @override
  void onWindowClose() {
    if (_closing) return;
    _closing = true;
    unawaited(_finishClose());
  }

  Future<void> _finishClose() async {
    try {
      _boundsDebounce?.cancel();
      await onBeforeClose?.call();
    } catch (_) {
      // Closing must never hang on a failed flush.
    } finally {
      await windowManager.destroy();
    }
  }

  Future<void> minimize() => windowManager.minimize();

  Future<void> close() => windowManager.close();

  Future<void> toggleMaximize() async {
    if (await windowManager.isMaximized()) {
      await windowManager.unmaximize();
    } else {
      await windowManager.maximize();
    }
  }

  Future<void> startDragging() => windowManager.startDragging();

  @override
  void onWindowMaximize() {
    _isMaximized = true;
    notifyListeners();
  }

  @override
  void onWindowUnmaximize() {
    _isMaximized = false;
    notifyListeners();
  }

  @override
  void onWindowFocus() {
    _isFocused = true;
    notifyListeners();
  }

  @override
  void onWindowBlur() {
    // Losing focus must never lock the vault.
    _isFocused = false;
    notifyListeners();
  }

  @override
  void onWindowMinimize() {
    // Intentionally empty: minimising is not a security event.
  }

  @override
  void onWindowRestore() {
    // Nothing to do; the session was never touched.
  }

  @override
  void onWindowResized() => _scheduleBoundsSave();

  @override
  void onWindowMoved() => _scheduleBoundsSave();

  void _scheduleBoundsSave() {
    if (!_settings.settings.rememberWindowBounds) return;
    _boundsDebounce?.cancel();
    _boundsDebounce = Timer(const Duration(milliseconds: 500), () async {
      try {
        final maximized = await windowManager.isMaximized();
        if (maximized) {
          final existing = _settings.settings.windowBounds;
          if (existing != null) {
            _settings.setWindowBounds(
              WindowBounds(
                left: existing.left,
                top: existing.top,
                width: existing.width,
                height: existing.height,
                maximized: true,
              ),
            );
          }
          return;
        }
        final bounds = await windowManager.getBounds();
        _settings.setWindowBounds(
          WindowBounds(
            left: bounds.left,
            top: bounds.top,
            width: bounds.width,
            height: bounds.height,
          ),
        );
      } catch (_) {
        // Window queries can fail while the window is being destroyed.
      }
    });
  }

  @override
  void dispose() {
    _boundsDebounce?.cancel();
    windowManager.removeListener(this);
    super.dispose();
  }
}
