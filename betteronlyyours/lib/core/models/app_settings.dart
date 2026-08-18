import 'generator_options.dart';

enum AppThemeVariant {
  midnight('Midnight', 'Deep indigo base with violet accent'),
  obsidian('Obsidian', 'Neutral graphite with cyan accent'),
  violet('Violet', 'Saturated violet, higher contrast accents');

  const AppThemeVariant(this.label, this.description);

  final String label;
  final String description;
}

enum UiDensity {
  comfortable('Comfortable'),
  compact('Compact');

  const UiDensity(this.label);

  final String label;
}

/// Non-sensitive preferences. Stored as plain JSON next to the vault; it never
/// contains secrets, entry names or usage history.
class AppSettings {
  const AppSettings({
    this.theme = AppThemeVariant.midnight,
    this.density = UiDensity.comfortable,
    this.reduceMotion = false,
    this.autoLockMinutes = 0,
    this.clipboardClearSeconds = 30,
    this.confirmDelete = true,
    this.revealSecretsByDefault = false,
    this.sidebarWidth = 320,
    this.globalHotkeyEnabled = true,
    this.rememberWindowBounds = true,
    this.generator = const GeneratorOptions(),
    this.windowBounds,
    this.extra = const <String, dynamic>{},
  });

  /// 0 means "never".
  static const List<int> autoLockChoices = <int>[0, 1, 5, 15, 30, 60];

  /// 0 means "never".
  static const List<int> clipboardChoices = <int>[0, 15, 30, 60, 120];

  static const double minSidebarWidth = 248;
  static const double maxSidebarWidth = 460;

  final AppThemeVariant theme;
  final UiDensity density;
  final bool reduceMotion;
  final int autoLockMinutes;
  final int clipboardClearSeconds;
  final bool confirmDelete;
  final bool revealSecretsByDefault;
  final double sidebarWidth;
  final bool globalHotkeyEnabled;
  final bool rememberWindowBounds;
  final GeneratorOptions generator;
  final WindowBounds? windowBounds;
  final Map<String, dynamic> extra;

  bool get autoLockEnabled => autoLockMinutes > 0;
  bool get clipboardClearEnabled => clipboardClearSeconds > 0;

  AppSettings copyWith({
    AppThemeVariant? theme,
    UiDensity? density,
    bool? reduceMotion,
    int? autoLockMinutes,
    int? clipboardClearSeconds,
    bool? confirmDelete,
    bool? revealSecretsByDefault,
    double? sidebarWidth,
    bool? globalHotkeyEnabled,
    bool? rememberWindowBounds,
    GeneratorOptions? generator,
    WindowBounds? windowBounds,
  }) {
    return AppSettings(
      theme: theme ?? this.theme,
      density: density ?? this.density,
      reduceMotion: reduceMotion ?? this.reduceMotion,
      autoLockMinutes: autoLockMinutes ?? this.autoLockMinutes,
      clipboardClearSeconds:
          clipboardClearSeconds ?? this.clipboardClearSeconds,
      confirmDelete: confirmDelete ?? this.confirmDelete,
      revealSecretsByDefault:
          revealSecretsByDefault ?? this.revealSecretsByDefault,
      sidebarWidth: (sidebarWidth ?? this.sidebarWidth).clamp(
        minSidebarWidth,
        maxSidebarWidth,
      ),
      globalHotkeyEnabled: globalHotkeyEnabled ?? this.globalHotkeyEnabled,
      rememberWindowBounds: rememberWindowBounds ?? this.rememberWindowBounds,
      generator: generator ?? this.generator,
      windowBounds: windowBounds ?? this.windowBounds,
      extra: extra,
    );
  }

  Map<String, dynamic> toJson() => <String, dynamic>{
    ...extra,
    'theme': theme.name,
    'density': density.name,
    'reduceMotion': reduceMotion,
    'autoLockMinutes': autoLockMinutes,
    'clipboardClearSeconds': clipboardClearSeconds,
    'confirmDelete': confirmDelete,
    'revealSecretsByDefault': revealSecretsByDefault,
    'sidebarWidth': sidebarWidth,
    'globalHotkeyEnabled': globalHotkeyEnabled,
    'rememberWindowBounds': rememberWindowBounds,
    'generator': generator.toJson(),
    if (windowBounds != null) 'windowBounds': windowBounds!.toJson(),
  };

  factory AppSettings.fromJson(Map<String, dynamic> json) {
    const fallback = AppSettings();
    const known = <String>{
      'theme',
      'density',
      'reduceMotion',
      'autoLockMinutes',
      'clipboardClearSeconds',
      'confirmDelete',
      'revealSecretsByDefault',
      'sidebarWidth',
      'globalHotkeyEnabled',
      'rememberWindowBounds',
      'generator',
      'windowBounds',
    };
    final generatorJson = json['generator'];
    return AppSettings(
      theme: AppThemeVariant.values.firstWhere(
        (v) => v.name == json['theme'],
        orElse: () => fallback.theme,
      ),
      density: UiDensity.values.firstWhere(
        (v) => v.name == json['density'],
        orElse: () => fallback.density,
      ),
      reduceMotion: json['reduceMotion'] == true,
      autoLockMinutes: _clampChoice(
        json['autoLockMinutes'],
        autoLockChoices,
        fallback.autoLockMinutes,
      ),
      clipboardClearSeconds: _clampChoice(
        json['clipboardClearSeconds'],
        clipboardChoices,
        fallback.clipboardClearSeconds,
      ),
      confirmDelete: json['confirmDelete'] is bool
          ? json['confirmDelete'] as bool
          : fallback.confirmDelete,
      revealSecretsByDefault: json['revealSecretsByDefault'] == true,
      sidebarWidth: _double(
        json['sidebarWidth'],
        fallback.sidebarWidth,
      ).clamp(minSidebarWidth, maxSidebarWidth),
      globalHotkeyEnabled: json['globalHotkeyEnabled'] is bool
          ? json['globalHotkeyEnabled'] as bool
          : fallback.globalHotkeyEnabled,
      rememberWindowBounds: json['rememberWindowBounds'] is bool
          ? json['rememberWindowBounds'] as bool
          : fallback.rememberWindowBounds,
      generator: generatorJson is Map<String, dynamic>
          ? GeneratorOptions.fromJson(generatorJson)
          : fallback.generator,
      windowBounds: WindowBounds.fromJson(json['windowBounds']),
      extra: <String, dynamic>{
        for (final entry in json.entries)
          if (!known.contains(entry.key)) entry.key: entry.value,
      },
    );
  }

  static double _double(Object? value, double fallback) =>
      value is num ? value.toDouble() : fallback;

  static int _clampChoice(Object? value, List<int> choices, int fallback) {
    if (value is! num) return fallback;
    final asInt = value.toInt();
    return choices.contains(asInt) ? asInt : fallback;
  }
}

class WindowBounds {
  const WindowBounds({
    required this.left,
    required this.top,
    required this.width,
    required this.height,
    this.maximized = false,
  });

  final double left;
  final double top;
  final double width;
  final double height;
  final bool maximized;

  Map<String, dynamic> toJson() => <String, dynamic>{
    'left': left,
    'top': top,
    'width': width,
    'height': height,
    'maximized': maximized,
  };

  static WindowBounds? fromJson(Object? raw) {
    if (raw is! Map) return null;
    final left = raw['left'];
    final top = raw['top'];
    final width = raw['width'];
    final height = raw['height'];
    if (left is! num || top is! num || width is! num || height is! num) {
      return null;
    }
    if (width < 200 || height < 200) return null;
    return WindowBounds(
      left: left.toDouble(),
      top: top.toDouble(),
      width: width.toDouble(),
      height: height.toDouble(),
      maximized: raw['maximized'] == true,
    );
  }
}
