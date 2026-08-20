import 'package:material_ui/material_ui.dart';

import '../../app/theme/palette.dart';
import '../../app/theme/tokens.dart';
import 'hover_builder.dart';

enum AppButtonVariant { primary, secondary, ghost, danger }

enum AppButtonSize { medium, small }

/// The single button component used across the app.
class AppButton extends StatelessWidget {
  const AppButton({
    super.key,
    required this.label,
    this.onPressed,
    this.icon,
    this.variant = AppButtonVariant.primary,
    this.size = AppButtonSize.medium,
    this.loading = false,
    this.expand = false,
    this.tooltip,
    this.autofocus = false,
  });

  final String label;
  final VoidCallback? onPressed;
  final IconData? icon;
  final AppButtonVariant variant;
  final AppButtonSize size;
  final bool loading;
  final bool expand;
  final String? tooltip;
  final bool autofocus;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final enabled = onPressed != null && !loading;
    final height = size == AppButtonSize.small ? 34.0 : tokens.controlHeight;

    Widget button = HoverBuilder(
      onTap: enabled ? onPressed : null,
      enabled: enabled,
      autofocus: autofocus,
      builder: (context, state) {
        final style = _resolveStyle(palette, state, enabled);
        return AnimatedContainer(
          duration: context.motion.fast,
          curve: context.motion.standard,
          height: height,
          padding: EdgeInsets.symmetric(
            horizontal: size == AppButtonSize.small ? Insets.md : Insets.lg,
          ),
          decoration: BoxDecoration(
            color: style.background,
            borderRadius: Corners.radiusSm,
            border: Border.all(color: style.border),
            boxShadow: style.glow,
          ),
          transform: state.pressed
              ? (Matrix4.identity()..translateByDouble(0, 1, 0, 1))
              : Matrix4.identity(),
          child: Row(
            mainAxisSize: expand ? MainAxisSize.max : MainAxisSize.min,
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              if (loading) ...<Widget>[
                SizedBox(
                  width: 14,
                  height: 14,
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    color: style.foreground,
                  ),
                ),
                const SizedBox(width: Insets.sm),
              ] else if (icon != null) ...<Widget>[
                Icon(icon, size: 16, color: style.foreground),
                const SizedBox(width: Insets.sm),
              ],
              Flexible(
                child: Text(
                  label,
                  overflow: TextOverflow.ellipsis,
                  style: tokens.text.bodyStrong.copyWith(
                    color: style.foreground,
                    fontSize: size == AppButtonSize.small ? 12.5 : 13.5,
                  ),
                ),
              ),
            ],
          ),
        );
      },
    );

    if (expand) {
      button = SizedBox(width: double.infinity, child: button);
    }
    if (tooltip != null) {
      button = Tooltip(message: tooltip!, child: button);
    }
    return Semantics(
      button: true,
      enabled: enabled,
      label: label,
      child: button,
    );
  }

  _ButtonStyle _resolveStyle(
    AppPalette palette,
    HoverState state,
    bool enabled,
  ) {
    if (!enabled) {
      return _ButtonStyle(
        background: palette.surface,
        foreground: palette.textTertiary,
        border: palette.border,
      );
    }

    switch (variant) {
      case AppButtonVariant.primary:
        return _ButtonStyle(
          background: state.pressed
              ? palette.accent
              : (state.active ? palette.accentStrong : palette.accent),
          foreground: Colors.white,
          border: Colors.transparent,
          glow: state.active
              ? <BoxShadow>[
                  BoxShadow(
                    color: palette.accent.withValues(alpha: 0.35),
                    blurRadius: 18,
                    spreadRadius: -6,
                    offset: const Offset(0, 6),
                  ),
                ]
              : const <BoxShadow>[],
        );
      case AppButtonVariant.secondary:
        return _ButtonStyle(
          background: state.active ? palette.surfaceHigh : palette.surface,
          foreground: palette.textPrimary,
          border: state.active ? palette.borderStrong : palette.border,
        );
      case AppButtonVariant.ghost:
        return _ButtonStyle(
          background: state.active
              ? palette.surfaceHigh.withValues(alpha: 0.7)
              : Colors.transparent,
          foreground: state.active
              ? palette.textPrimary
              : palette.textSecondary,
          border: Colors.transparent,
        );
      case AppButtonVariant.danger:
        return _ButtonStyle(
          background: state.active
              ? palette.danger.withValues(alpha: 0.18)
              : palette.danger.withValues(alpha: 0.12),
          foreground: palette.danger,
          border: palette.danger.withValues(alpha: state.active ? 0.65 : 0.35),
        );
    }
  }
}

class _ButtonStyle {
  const _ButtonStyle({
    required this.background,
    required this.foreground,
    required this.border,
    this.glow = const <BoxShadow>[],
  });

  final Color background;
  final Color foreground;
  final Color border;
  final List<BoxShadow> glow;
}

/// Icon-only action. Always tooltipped, because icons alone are not labels.
class AppIconButton extends StatelessWidget {
  const AppIconButton({
    super.key,
    required this.icon,
    required this.tooltip,
    this.onPressed,
    this.size = 18,
    this.color,
    this.active = false,
    this.danger = false,
    this.dense = false,
  });

  final IconData icon;
  final String tooltip;
  final VoidCallback? onPressed;
  final double size;
  final Color? color;
  final bool active;
  final bool danger;
  final bool dense;

  @override
  Widget build(BuildContext context) {
    final palette = context.colors;
    final enabled = onPressed != null;
    final box = dense ? 30.0 : 34.0;

    return Tooltip(
      message: tooltip,
      child: Semantics(
        button: true,
        enabled: enabled,
        label: tooltip,
        child: HoverBuilder(
          onTap: onPressed,
          enabled: enabled,
          builder: (context, state) {
            final baseColor = danger
                ? palette.danger
                : (color ?? (active ? palette.accent : palette.textSecondary));
            return AnimatedContainer(
              duration: context.motion.fast,
              curve: context.motion.standard,
              width: box,
              height: box,
              decoration: BoxDecoration(
                color: state.active
                    ? (danger
                          ? palette.danger.withValues(alpha: 0.14)
                          : palette.surfaceHigh)
                    : (active
                          ? palette.accent.withValues(alpha: 0.12)
                          : Colors.transparent),
                borderRadius: Corners.radiusSm,
                border: Border.all(
                  color: state.focused
                      ? palette.accent.withValues(alpha: 0.8)
                      : Colors.transparent,
                ),
              ),
              child: Center(
                child: AnimatedScale(
                  duration: context.motion.fast,
                  curve: context.motion.standard,
                  scale: state.pressed ? 0.9 : 1,
                  child: Icon(
                    icon,
                    size: size,
                    color: enabled
                        ? (state.active
                              ? _emphasize(baseColor, palette)
                              : baseColor)
                        : palette.textTertiary.withValues(alpha: 0.5),
                  ),
                ),
              ),
            );
          },
        ),
      ),
    );
  }

  Color _emphasize(Color color, AppPalette palette) =>
      color == palette.textSecondary ? palette.textPrimary : color;
}
