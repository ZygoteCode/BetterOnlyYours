import 'package:flutter/material.dart';

import '../../core/models/app_settings.dart';
import 'palette.dart';

/// Spacing scale. Everything in the UI is a multiple of 4.
class Insets {
  const Insets._();

  static const double xxs = 2;
  static const double xs = 4;
  static const double sm = 8;
  static const double md = 12;
  static const double lg = 16;
  static const double xl = 20;
  static const double xxl = 24;
  static const double xxxl = 32;
  static const double huge = 40;
}

class Corners {
  const Corners._();

  static const double xs = 6;
  static const double sm = 8;
  static const double md = 12;
  static const double lg = 16;
  static const double xl = 20;
  static const double pill = 999;

  static const BorderRadius radiusXs = BorderRadius.all(Radius.circular(xs));
  static const BorderRadius radiusSm = BorderRadius.all(Radius.circular(sm));
  static const BorderRadius radiusMd = BorderRadius.all(Radius.circular(md));
  static const BorderRadius radiusLg = BorderRadius.all(Radius.circular(lg));
  static const BorderRadius radiusXl = BorderRadius.all(Radius.circular(xl));
}

/// Layout breakpoints for the desktop shell.
class Breakpoints {
  const Breakpoints._();

  /// Below this the shell switches to a single-pane, stacked layout.
  static const double singlePane = 880;

  /// Below this the navigation rail collapses to icons only.
  static const double railLabels = 1180;

  /// At or above this the entry inspector column is shown.
  static const double inspector = 1500;
}

/// Motion tokens. When the user turns animations off every duration collapses
/// to zero, so no widget needs to special-case the setting.
@immutable
class AppMotion {
  const AppMotion({required this.enabled});

  final bool enabled;

  Duration get instant => _scaled(90);
  Duration get fast => _scaled(150);
  Duration get normal => _scaled(220);
  Duration get slow => _scaled(320);
  Duration get page => _scaled(420);

  Curve get standard => Curves.easeOutCubic;
  Curve get emphasized => Curves.easeOutExpo;
  Curve get exit => Curves.easeInCubic;
  Curve get overshoot => Curves.easeOutBack;

  /// With animations off every duration collapses to a single millisecond
  /// rather than zero: implicit animations (AnimatedSize in particular) assert
  /// on zero-length durations, and 1 ms is imperceptible.
  Duration _scaled(int milliseconds) =>
      enabled ? Duration(milliseconds: milliseconds) : _off;

  static const Duration _off = Duration(milliseconds: 1);
}

/// Typography scale. Proportional type for the interface, monospace reserved
/// for secrets and technical values.
@immutable
class AppTypography {
  const AppTypography({
    required this.display,
    required this.pageTitle,
    required this.sectionTitle,
    required this.cardTitle,
    required this.body,
    required this.bodyStrong,
    required this.secondary,
    required this.label,
    required this.caption,
    required this.mono,
    required this.monoLarge,
  });

  static const List<String> uiFallback = <String>[
    'Segoe UI Variable Text',
    'Segoe UI',
    'Inter',
    'Roboto',
    'Arial',
  ];

  static const List<String> monoFallback = <String>[
    'Cascadia Mono',
    'Cascadia Code',
    'Consolas',
    'JetBrains Mono',
    'Courier New',
  ];

  final TextStyle display;
  final TextStyle pageTitle;
  final TextStyle sectionTitle;
  final TextStyle cardTitle;
  final TextStyle body;
  final TextStyle bodyStrong;
  final TextStyle secondary;
  final TextStyle label;
  final TextStyle caption;
  final TextStyle mono;
  final TextStyle monoLarge;

  factory AppTypography.build(AppPalette palette, UiDensity density) {
    final scale = density == UiDensity.compact ? 0.94 : 1.0;

    TextStyle ui(
      double size,
      FontWeight weight,
      Color color, {
      double height = 1.35,
      double letterSpacing = 0,
    }) {
      return TextStyle(
        fontFamilyFallback: uiFallback,
        fontSize: size * scale,
        fontWeight: weight,
        color: color,
        height: height,
        letterSpacing: letterSpacing,
      );
    }

    TextStyle mono(double size, Color color, {FontWeight? weight}) {
      return TextStyle(
        fontFamily: monoFallback.first,
        fontFamilyFallback: monoFallback,
        fontSize: size * scale,
        fontWeight: weight ?? FontWeight.w500,
        color: color,
        height: 1.4,
        letterSpacing: 0.4,
      );
    }

    return AppTypography(
      display: ui(30, FontWeight.w700, palette.textPrimary, height: 1.2),
      pageTitle: ui(22, FontWeight.w700, palette.textPrimary, height: 1.25),
      sectionTitle: ui(16, FontWeight.w600, palette.textPrimary),
      cardTitle: ui(14, FontWeight.w600, palette.textPrimary),
      body: ui(13.5, FontWeight.w400, palette.textPrimary, height: 1.5),
      bodyStrong: ui(13.5, FontWeight.w600, palette.textPrimary, height: 1.5),
      secondary: ui(12.5, FontWeight.w400, palette.textSecondary, height: 1.45),
      label: ui(11, FontWeight.w600, palette.textTertiary, letterSpacing: 0.7),
      caption: ui(11.5, FontWeight.w400, palette.textTertiary),
      mono: mono(12.5, palette.textPrimary),
      monoLarge: mono(15, palette.textPrimary, weight: FontWeight.w600),
    );
  }
}

/// The design system, exposed to widgets as a [ThemeExtension].
@immutable
class AppTokens extends ThemeExtension<AppTokens> {
  const AppTokens({
    required this.color,
    required this.text,
    required this.motion,
    required this.density,
  });

  final AppPalette color;
  final AppTypography text;
  final AppMotion motion;
  final UiDensity density;

  bool get isCompact => density == UiDensity.compact;

  double get rowHeight => isCompact ? 44 : 52;
  double get controlHeight => isCompact ? 36 : 40;
  double get panePadding => isCompact ? Insets.lg : Insets.xxl;
  double get cardPadding => isCompact ? Insets.lg : Insets.xl;
  double get titleBarHeight => 40;

  List<BoxShadow> get cardShadow => <BoxShadow>[
    BoxShadow(
      color: color.shadow.withValues(alpha: 0.35),
      blurRadius: 24,
      spreadRadius: -8,
      offset: const Offset(0, 10),
    ),
  ];

  List<BoxShadow> get overlayShadow => <BoxShadow>[
    BoxShadow(
      color: color.shadow.withValues(alpha: 0.55),
      blurRadius: 60,
      spreadRadius: -12,
      offset: const Offset(0, 24),
    ),
  ];

  List<BoxShadow> accentGlow({double opacity = 0.28, double blur = 28}) =>
      <BoxShadow>[
        BoxShadow(
          color: color.accent.withValues(alpha: opacity),
          blurRadius: blur,
          spreadRadius: -10,
          offset: const Offset(0, 8),
        ),
      ];

  @override
  AppTokens copyWith({
    AppPalette? color,
    AppTypography? text,
    AppMotion? motion,
    UiDensity? density,
  }) {
    return AppTokens(
      color: color ?? this.color,
      text: text ?? this.text,
      motion: motion ?? this.motion,
      density: density ?? this.density,
    );
  }

  @override
  AppTokens lerp(ThemeExtension<AppTokens>? other, double t) {
    if (other is! AppTokens) return this;
    return t < 0.5 ? this : other;
  }
}

extension AppThemeContext on BuildContext {
  AppTokens get tokens =>
      Theme.of(this).extension<AppTokens>() ??
      AppTokens(
        color: AppPalette.midnight,
        text: AppTypography.build(AppPalette.midnight, UiDensity.comfortable),
        motion: const AppMotion(enabled: true),
        density: UiDensity.comfortable,
      );

  AppPalette get colors => tokens.color;
  AppTypography get typography => tokens.text;
  AppMotion get motion => tokens.motion;
}
