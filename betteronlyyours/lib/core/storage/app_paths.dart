import 'dart:io';

import 'package:path/path.dart' as p;

/// Resolves where the vault and the preference file live.
///
/// Deliberately synchronous and plugin-free: these paths are needed during
/// start-up, before the first frame, and any platform-channel call at that
/// point would block on a message loop that is not pumping yet.
///
/// Discovery order keeps existing installs working: a vault sitting next to
/// the executable (or in the working directory, which is how early builds
/// stored it) is always preferred over the per-user data folder.
class AppPaths {
  const AppPaths._();

  static const String vaultFileName = 'credentials.plf';
  static const String settingsFileName = 'settings.json';

  /// Optional absolute path override, useful for portable installs.
  static const String vaultPathEnvVar = 'BETTERONLYYOURS_VAULT';

  /// Matches the CompanyName/ProductName in `windows/runner/Runner.rc`, which
  /// is the same layout `%APPDATA%\<company>\<product>` that path_provider
  /// would produce.
  static const String _dataFolder = 'BetterOnlyYours';

  static String? _cachedVaultPath;
  static String? _cachedSettingsPath;

  static String vaultPath() => _cachedVaultPath ??= _resolveVaultPath();

  static String settingsPath() =>
      _cachedSettingsPath ??= p.join(dataDirectory(), settingsFileName);

  /// Only for tests, so path resolution can be re-evaluated.
  static void resetCacheForTesting() {
    _cachedVaultPath = null;
    _cachedSettingsPath = null;
  }

  static String dataDirectory() {
    final appData = Platform.environment['APPDATA'];
    if (appData == null || appData.trim().isEmpty) {
      return Directory.current.path;
    }
    return p.join(appData, _dataFolder, _dataFolder);
  }

  static String _resolveVaultPath() {
    final override = Platform.environment[vaultPathEnvVar];
    if (override != null && override.trim().isNotEmpty) {
      return p.normalize(p.absolute(override.trim()));
    }

    final legacyCwd = p.join(Directory.current.path, vaultFileName);
    if (File(legacyCwd).existsSync()) return legacyCwd;

    final portable = p.join(
      p.dirname(Platform.resolvedExecutable),
      vaultFileName,
    );
    if (File(portable).existsSync()) return portable;

    return p.join(dataDirectory(), vaultFileName);
  }
}
