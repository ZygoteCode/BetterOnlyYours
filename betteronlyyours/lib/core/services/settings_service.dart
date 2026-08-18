import 'dart:convert';
import 'dart:io';

import '../models/app_settings.dart';
import '../storage/app_paths.dart';

/// Reads and writes the plain-JSON preference file.
///
/// This file never contains secrets, entry names or usage history — only
/// interface and behaviour preferences.
class SettingsService {
  SettingsService({String? path}) : _explicitPath = path;

  final String? _explicitPath;

  String resolvePath() => _explicitPath ?? AppPaths.settingsPath();

  /// Synchronous because preferences are needed to build the first frame.
  AppSettings load() {
    try {
      final file = File(resolvePath());
      if (!file.existsSync()) return const AppSettings();
      final decoded = jsonDecode(file.readAsStringSync());
      if (decoded is! Map<String, dynamic>) return const AppSettings();
      return AppSettings.fromJson(decoded);
    } catch (_) {
      // A damaged preference file must never block access to the vault.
      return const AppSettings();
    }
  }

  Future<void> save(AppSettings settings) async {
    final path = resolvePath();
    final file = File(path);
    final temp = File('$path.tmp');
    if (!file.parent.existsSync()) {
      await file.parent.create(recursive: true);
    }
    await temp.writeAsString(
      const JsonEncoder.withIndent('  ').convert(settings.toJson()),
      flush: true,
    );
    await temp.rename(path);
  }
}
