import 'package:material_ui/material_ui.dart';

import '../../core/models/app_settings.dart';
import 'palette.dart';
import 'tokens.dart';

/// Builds the single source of truth for the application's look.
class AppTheme {
  const AppTheme._();

  static ThemeData build(AppSettings settings) {
    final palette = AppPalette.of(settings.theme);
    final typography = AppTypography.build(palette, settings.density);
    final tokens = AppTokens(
      color: palette,
      text: typography,
      motion: AppMotion(enabled: !settings.reduceMotion),
      density: settings.density,
    );

    final colorScheme = ColorScheme.dark(
      primary: palette.accent,
      onPrimary: Colors.white,
      secondary: palette.secondary,
      onSecondary: palette.background,
      surface: palette.surface,
      onSurface: palette.textPrimary,
      surfaceContainerHighest: palette.surfaceHigh,
      outline: palette.border,
      error: palette.danger,
      onError: Colors.white,
    );

    return ThemeData(
      useMaterial3: true,
      brightness: Brightness.dark,
      colorScheme: colorScheme,
      scaffoldBackgroundColor: palette.background,
      canvasColor: palette.background,
      dividerColor: palette.border,
      splashFactory: NoSplash.splashFactory,
      highlightColor: Colors.transparent,
      hoverColor: palette.surfaceHigh,
      visualDensity: settings.density == UiDensity.compact
          ? VisualDensity.compact
          : VisualDensity.standard,
      extensions: <ThemeExtension<dynamic>>[tokens],
      fontFamilyFallback: AppTypography.uiFallback,
      textTheme: TextTheme(
        displaySmall: typography.display,
        headlineSmall: typography.pageTitle,
        titleLarge: typography.pageTitle,
        titleMedium: typography.sectionTitle,
        titleSmall: typography.cardTitle,
        bodyLarge: typography.body,
        bodyMedium: typography.body,
        bodySmall: typography.secondary,
        labelLarge: typography.bodyStrong,
        labelMedium: typography.label,
        labelSmall: typography.caption,
      ),
      iconTheme: IconThemeData(color: palette.textSecondary, size: 18),
      dividerTheme: DividerThemeData(
        color: palette.border,
        space: 1,
        thickness: 1,
      ),
      textSelectionTheme: TextSelectionThemeData(
        cursorColor: palette.accent,
        selectionColor: palette.accent.withValues(alpha: 0.32),
        selectionHandleColor: palette.accent,
      ),
      tooltipTheme: TooltipThemeData(
        waitDuration: const Duration(milliseconds: 420),
        padding: const EdgeInsets.symmetric(
          horizontal: Insets.md,
          vertical: Insets.sm,
        ),
        margin: const EdgeInsets.only(bottom: Insets.xs),
        decoration: BoxDecoration(
          color: palette.overlay,
          borderRadius: Corners.radiusSm,
          border: Border.all(color: palette.border),
          boxShadow: tokens.cardShadow,
        ),
        textStyle: typography.secondary.copyWith(color: palette.textPrimary),
      ),
      scrollbarTheme: ScrollbarThemeData(
        thumbColor: WidgetStateProperty.resolveWith((states) {
          if (states.contains(WidgetState.dragged)) {
            return palette.accent.withValues(alpha: 0.85);
          }
          if (states.contains(WidgetState.hovered)) {
            return palette.borderStrong.withValues(alpha: 0.95);
          }
          return palette.borderStrong.withValues(alpha: 0.6);
        }),
        trackColor: WidgetStateProperty.all(Colors.transparent),
        thickness: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.hovered) ? 10 : 7,
        ),
        radius: const Radius.circular(Corners.pill),
        crossAxisMargin: 2,
        mainAxisMargin: 4,
        interactive: true,
      ),
      progressIndicatorTheme: ProgressIndicatorThemeData(
        color: palette.accent,
        linearTrackColor: palette.surfaceHigh,
        circularTrackColor: Colors.transparent,
      ),
      sliderTheme: SliderThemeData(
        activeTrackColor: palette.accent,
        inactiveTrackColor: palette.surfaceHigh,
        thumbColor: palette.accentStrong,
        overlayColor: palette.accent.withValues(alpha: 0.16),
        trackHeight: 4,
        valueIndicatorColor: palette.overlay,
        valueIndicatorTextStyle: typography.mono,
      ),
      switchTheme: SwitchThemeData(
        thumbColor: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.selected)
              ? Colors.white
              : palette.textTertiary,
        ),
        trackColor: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.selected)
              ? palette.accent
              : palette.surfaceHigh,
        ),
        trackOutlineColor: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.selected)
              ? Colors.transparent
              : palette.border,
        ),
      ),
      checkboxTheme: CheckboxThemeData(
        fillColor: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.selected)
              ? palette.accent
              : Colors.transparent,
        ),
        checkColor: WidgetStateProperty.all(Colors.white),
        side: BorderSide(color: palette.borderStrong, width: 1.4),
        shape: RoundedRectangleBorder(borderRadius: Corners.radiusXs),
      ),
      menuTheme: MenuThemeData(
        style: MenuStyle(
          backgroundColor: WidgetStateProperty.all(palette.overlay),
          surfaceTintColor: WidgetStateProperty.all(Colors.transparent),
          elevation: WidgetStateProperty.all(0),
          padding: WidgetStateProperty.all(
            const EdgeInsets.symmetric(vertical: Insets.xs),
          ),
          shape: WidgetStateProperty.all(
            RoundedRectangleBorder(
              borderRadius: Corners.radiusMd,
              side: BorderSide(color: palette.border),
            ),
          ),
        ),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: palette.overlay,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: Corners.radiusLg,
          side: BorderSide(color: palette.border),
        ),
      ),
    );
  }
}
