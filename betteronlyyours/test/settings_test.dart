import 'dart:io';

import 'package:betteronlyyours/core/models/app_settings.dart';
import 'package:betteronlyyours/core/models/generator_options.dart';
import 'package:betteronlyyours/core/services/settings_service.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('AppSettings', () {
    test('defaults keep the vault open until the user asks otherwise', () {
      const settings = AppSettings();
      expect(settings.autoLockMinutes, 0);
      expect(settings.autoLockEnabled, isFalse);
      expect(settings.clipboardClearSeconds, 30);
      expect(settings.confirmDelete, isTrue);
      expect(settings.revealSecretsByDefault, isFalse);
    });

    test('json round trip preserves every field', () {
      const settings = AppSettings(
        theme: AppThemeVariant.obsidian,
        density: UiDensity.compact,
        reduceMotion: true,
        autoLockMinutes: 15,
        clipboardClearSeconds: 60,
        confirmDelete: false,
        revealSecretsByDefault: true,
        sidebarWidth: 360,
        globalHotkeyEnabled: false,
        generator: GeneratorOptions(mode: GeneratorMode.passphrase, words: 7),
        windowBounds: WindowBounds(
          left: 10,
          top: 20,
          width: 1200,
          height: 800,
          maximized: true,
        ),
      );

      final parsed = AppSettings.fromJson(settings.toJson());

      expect(parsed.theme, AppThemeVariant.obsidian);
      expect(parsed.density, UiDensity.compact);
      expect(parsed.reduceMotion, isTrue);
      expect(parsed.autoLockMinutes, 15);
      expect(parsed.clipboardClearSeconds, 60);
      expect(parsed.confirmDelete, isFalse);
      expect(parsed.revealSecretsByDefault, isTrue);
      expect(parsed.sidebarWidth, 360);
      expect(parsed.globalHotkeyEnabled, isFalse);
      expect(parsed.generator.mode, GeneratorMode.passphrase);
      expect(parsed.generator.words, 7);
      expect(parsed.windowBounds?.width, 1200);
      expect(parsed.windowBounds?.maximized, isTrue);
    });

    test('unknown or invalid values fall back to defaults', () {
      final parsed = AppSettings.fromJson(<String, dynamic>{
        'theme': 'neon-pink',
        'autoLockMinutes': 7,
        'clipboardClearSeconds': 'soon',
        'sidebarWidth': 4000,
        'somethingNew': 42,
      });

      expect(parsed.theme, AppThemeVariant.midnight);
      expect(parsed.autoLockMinutes, 0);
      expect(parsed.clipboardClearSeconds, 30);
      expect(parsed.sidebarWidth, AppSettings.maxSidebarWidth);
      expect(parsed.toJson()['somethingNew'], 42);
    });

    test('window bounds are rejected when implausible', () {
      expect(
        WindowBounds.fromJson(<String, dynamic>{
          'left': 0,
          'top': 0,
          'width': 10,
          'height': 10,
        }),
        isNull,
      );
      expect(WindowBounds.fromJson('nope'), isNull);
    });
  });

  group('SettingsService', () {
    late Directory tempDir;

    setUp(() async {
      tempDir = await Directory.systemTemp.createTemp('boy_settings_test');
    });

    tearDown(() async {
      if (tempDir.existsSync()) await tempDir.delete(recursive: true);
    });

    test('saves and reloads settings', () async {
      final path = '${tempDir.path}${Platform.pathSeparator}settings.json';
      final service = SettingsService(path: path);

      await service.save(
        const AppSettings(theme: AppThemeVariant.violet, autoLockMinutes: 30),
      );
      final loaded = service.load();

      expect(loaded.theme, AppThemeVariant.violet);
      expect(loaded.autoLockMinutes, 30);
    });

    test('a missing or damaged file yields defaults', () async {
      final path = '${tempDir.path}${Platform.pathSeparator}settings.json';
      expect(
        SettingsService(path: path).load().theme,
        AppThemeVariant.midnight,
      );

      File(path).writeAsStringSync('{ this is not json');
      expect(SettingsService(path: path).load().autoLockMinutes, 0);
    });
  });
}
