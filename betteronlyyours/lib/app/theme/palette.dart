import 'package:flutter/widgets.dart';

import '../../core/models/app_settings.dart';

/// Every colour used by the interface. Widgets never hard-code colours; they
/// read them from here through `context.colors`.
@immutable
class AppPalette {
  const AppPalette({
    required this.variant,
    required this.background,
    required this.backgroundElevated,
    required this.surface,
    required this.surfaceHigh,
    required this.overlay,
    required this.border,
    required this.borderStrong,
    required this.textPrimary,
    required this.textSecondary,
    required this.textTertiary,
    required this.accent,
    required this.accentStrong,
    required this.secondary,
    required this.success,
    required this.warning,
    required this.danger,
    required this.entryAccents,
  });

  final AppThemeVariant variant;

  /// Deepest layer: the window itself.
  final Color background;

  /// Panels sitting on the window (navigation rail, list column).
  final Color backgroundElevated;

  /// Cards and inputs.
  final Color surface;

  /// Hover / pressed / selected fills.
  final Color surfaceHigh;

  /// Dialogs and floating surfaces.
  final Color overlay;

  final Color border;
  final Color borderStrong;

  final Color textPrimary;
  final Color textSecondary;
  final Color textTertiary;

  final Color accent;
  final Color accentStrong;
  final Color secondary;

  final Color success;
  final Color warning;
  final Color danger;

  /// Deterministic per-entry accents for avatars.
  final List<Color> entryAccents;

  Color get shadow => const Color(0xFF000000);

  Color accentFill(double opacity) => accent.withValues(alpha: opacity);

  static AppPalette of(AppThemeVariant variant) => switch (variant) {
    AppThemeVariant.midnight => midnight,
    AppThemeVariant.obsidian => obsidian,
    AppThemeVariant.violet => violet,
  };

  static const List<Color> _sharedEntryAccents = <Color>[
    Color(0xFF7C5CFF),
    Color(0xFF2FD8F5),
    Color(0xFF2FD8A8),
    Color(0xFFF5B942),
    Color(0xFFFF7A8A),
    Color(0xFF4F8BFF),
    Color(0xFFE86BC0),
    Color(0xFF9BE15D),
  ];

  static const AppPalette midnight = AppPalette(
    variant: AppThemeVariant.midnight,
    background: Color(0xFF060A17),
    backgroundElevated: Color(0xFF0A1020),
    surface: Color(0xFF0F1730),
    surfaceHigh: Color(0xFF16203E),
    overlay: Color(0xFF0B1224),
    border: Color(0xFF1B2645),
    borderStrong: Color(0xFF2A3862),
    textPrimary: Color(0xFFECF0FB),
    textSecondary: Color(0xFF9EA9C9),
    textTertiary: Color(0xFF6A7597),
    accent: Color(0xFF7C5CFF),
    accentStrong: Color(0xFF9277FF),
    secondary: Color(0xFF2FD8F5),
    success: Color(0xFF2FD8A8),
    warning: Color(0xFFF5B942),
    danger: Color(0xFFFF5D73),
    entryAccents: _sharedEntryAccents,
  );

  static const AppPalette obsidian = AppPalette(
    variant: AppThemeVariant.obsidian,
    background: Color(0xFF08090C),
    backgroundElevated: Color(0xFF0D0F14),
    surface: Color(0xFF13161D),
    surfaceHigh: Color(0xFF1A1E27),
    overlay: Color(0xFF101319),
    border: Color(0xFF20242E),
    borderStrong: Color(0xFF333A48),
    textPrimary: Color(0xFFEDEFF3),
    textSecondary: Color(0xFF9AA1AE),
    textTertiary: Color(0xFF6B7280),
    accent: Color(0xFF35C9E8),
    accentStrong: Color(0xFF5BD8F2),
    secondary: Color(0xFF9E7BFF),
    success: Color(0xFF3ED6A4),
    warning: Color(0xFFE9B155),
    danger: Color(0xFFF2647A),
    entryAccents: <Color>[
      Color(0xFF35C9E8),
      Color(0xFF9E7BFF),
      Color(0xFF3ED6A4),
      Color(0xFFE9B155),
      Color(0xFFF2647A),
      Color(0xFF6E9BFF),
      Color(0xFFD87BC4),
      Color(0xFFA8DC6A),
    ],
  );

  static const AppPalette violet = AppPalette(
    variant: AppThemeVariant.violet,
    background: Color(0xFF0A0618),
    backgroundElevated: Color(0xFF120A28),
    surface: Color(0xFF191036),
    surfaceHigh: Color(0xFF231648),
    overlay: Color(0xFF150C2E),
    border: Color(0xFF2A1B55),
    borderStrong: Color(0xFF3E2A7A),
    textPrimary: Color(0xFFF1ECFF),
    textSecondary: Color(0xFFB1A3D6),
    textTertiary: Color(0xFF7C6BA8),
    accent: Color(0xFF9F7BFF),
    accentStrong: Color(0xFFB79BFF),
    secondary: Color(0xFF63E6FF),
    success: Color(0xFF48E0B4),
    warning: Color(0xFFFFC55C),
    danger: Color(0xFFFF6E86),
    entryAccents: <Color>[
      Color(0xFF9F7BFF),
      Color(0xFF63E6FF),
      Color(0xFF48E0B4),
      Color(0xFFFFC55C),
      Color(0xFFFF6E86),
      Color(0xFF6E9BFF),
      Color(0xFFEF7ED0),
      Color(0xFFB6EA6E),
    ],
  );
}
