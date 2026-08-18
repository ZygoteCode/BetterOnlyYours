import 'dart:async';

import 'package:flutter/foundation.dart';

import '../core/models/app_settings.dart';
import '../core/models/generator_options.dart';
import '../core/services/settings_service.dart';

/// Owns user preferences and persists them with a short debounce so that
/// dragging a slider does not hammer the disk.
class SettingsController extends ChangeNotifier {
  SettingsController({SettingsService? service})
    : _service = service ?? SettingsService();

  final SettingsService _service;

  AppSettings _settings = const AppSettings();
  bool _loaded = false;
  Timer? _debounce;

  AppSettings get settings => _settings;
  bool get isLoaded => _loaded;

  /// Reads preferences from disk. Synchronous on purpose: the theme has to be
  /// known before the first frame, and start-up must not await anything that
  /// depends on the platform message loop.
  void load() {
    _settings = _service.load();
    _loaded = true;
    notifyListeners();
  }

  void update(
    AppSettings Function(AppSettings current) transform, {
    bool immediate = false,
  }) {
    final next = transform(_settings);
    if (identical(next, _settings)) return;
    _settings = next;
    notifyListeners();
    _schedulePersist(immediate: immediate);
  }

  void setTheme(AppThemeVariant theme) =>
      update((s) => s.copyWith(theme: theme), immediate: true);

  void setDensity(UiDensity density) =>
      update((s) => s.copyWith(density: density), immediate: true);

  void setReduceMotion(bool value) =>
      update((s) => s.copyWith(reduceMotion: value), immediate: true);

  void setAutoLockMinutes(int minutes) =>
      update((s) => s.copyWith(autoLockMinutes: minutes), immediate: true);

  void setClipboardClearSeconds(int seconds) => update(
    (s) => s.copyWith(clipboardClearSeconds: seconds),
    immediate: true,
  );

  void setConfirmDelete(bool value) =>
      update((s) => s.copyWith(confirmDelete: value), immediate: true);

  void setRevealSecretsByDefault(bool value) =>
      update((s) => s.copyWith(revealSecretsByDefault: value), immediate: true);

  void setGlobalHotkeyEnabled(bool value) =>
      update((s) => s.copyWith(globalHotkeyEnabled: value), immediate: true);

  void setRememberWindowBounds(bool value) =>
      update((s) => s.copyWith(rememberWindowBounds: value), immediate: true);

  void setSidebarWidth(double width) =>
      update((s) => s.copyWith(sidebarWidth: width));

  void setGenerator(GeneratorOptions options) =>
      update((s) => s.copyWith(generator: options));

  void setWindowBounds(WindowBounds bounds) =>
      update((s) => s.copyWith(windowBounds: bounds));

  Future<void> flush() async {
    _debounce?.cancel();
    _debounce = null;
    await _persist();
  }

  void _schedulePersist({bool immediate = false}) {
    _debounce?.cancel();
    if (immediate) {
      unawaited(_persist());
      return;
    }
    _debounce = Timer(const Duration(milliseconds: 600), () {
      unawaited(_persist());
    });
  }

  Future<void> _persist() async {
    try {
      await _service.save(_settings);
    } catch (error) {
      debugPrint('Settings could not be saved: $error');
    }
  }

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }
}
