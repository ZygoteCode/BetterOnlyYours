import 'dart:async';
import 'dart:io';

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
  WindowController({
    required this._settings,
    WindowShutdown? shutdown,
    this._flushBudget = defaultFlushBudget,
    this._destroyBudget = defaultDestroyBudget,
  }) : _shutdown = shutdown ?? const WindowShutdown();

  /// Longest the pre-close flush may run before the app closes anyway.
  static const Duration defaultFlushBudget = Duration(milliseconds: 1500);

  /// Grace period the platform gets to tear the window down before the
  /// process is ended outright.
  static const Duration defaultDestroyBudget = Duration(milliseconds: 900);

  final SettingsController _settings;
  final WindowShutdown _shutdown;
  final Duration _flushBudget;
  final Duration _destroyBudget;

  bool _isMaximized = false;
  bool _isFocused = true;
  bool _closing = false;
  Future<void>? _closeFuture;
  Timer? _boundsDebounce;
  Timer? _exitWatchdog;

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
  void onWindowClose() => closeNow();

  /// Runs the shutdown sequence. Public so a test can drive it without the
  /// platform sending a real close event.
  @visibleForTesting
  Future<void> closeNow() {
    if (_closing) return _closeFuture ?? Future<void>.value();
    _closing = true;
    return _closeFuture = _finishClose();
  }

  /// Shutdown, in the order that keeps the app feeling instant:
  ///
  /// 1. the window disappears straight away, so the click on the close button
  ///    is acknowledged even if the disk is busy;
  /// 2. pending writes get the flush budget and no more — every write is
  ///    atomic, so abandoning one can cost the last unsaved change but never
  ///    the file;
  /// 3. the platform window is destroyed, guarded by the destroy budget: a plugin
  ///    or engine stalling during teardown used to leave a frozen window on
  ///    screen for several seconds, and now ends the process instead.
  Future<void> _finishClose() async {
    _boundsDebounce?.cancel();
    _boundsDebounce = null;

    try {
      await _shutdown.hideWindow();
    } catch (_) {
      // Hiding is cosmetic; carry on with the real teardown.
    }

    try {
      final pending = onBeforeClose?.call();
      if (pending != null) await pending.timeout(_flushBudget);
    } catch (_) {
      // Closing must never hang on a failed or slow flush.
    }

    _exitWatchdog = Timer(_destroyBudget, _shutdown.terminateProcess);
    try {
      await _shutdown.destroyWindow();
      _exitWatchdog?.cancel();
      _exitWatchdog = null;
    } catch (_) {
      _exitWatchdog?.cancel();
      _exitWatchdog = null;
      _shutdown.terminateProcess();
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
    _exitWatchdog?.cancel();
    windowManager.removeListener(this);
    super.dispose();
  }
}

/// The platform side of shutting the app down.
///
/// Isolated behind an object so the close sequence — including the watchdog
/// that ends the process — can be exercised in tests without a real window and
/// without terminating the test runner.
class WindowShutdown {
  const WindowShutdown();

  Future<void> hideWindow() => windowManager.hide();

  Future<void> destroyWindow() => windowManager.destroy();

  void terminateProcess() => exit(0);
}
