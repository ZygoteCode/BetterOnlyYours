import 'package:betteronlyyours/core/models/app_settings.dart';
import 'package:betteronlyyours/core/services/settings_service.dart';

/// Preferences kept in memory, so widget tests never touch the disk.
class MemorySettingsService implements SettingsService {
  MemorySettingsService([this._settings = const AppSettings()]);

  AppSettings _settings;

  @override
  String resolvePath() => 'memory://settings.json';

  @override
  AppSettings load() => _settings;

  @override
  Future<void> save(AppSettings settings) async {
    _settings = settings;
  }
}
