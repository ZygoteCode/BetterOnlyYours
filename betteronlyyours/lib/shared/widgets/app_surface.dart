import 'package:material_ui/material_ui.dart';

import '../../app/theme/tokens.dart';
import 'hover_builder.dart';

/// Standard content card.
class AppCard extends StatelessWidget {
  const AppCard({
    super.key,
    required this.child,
    this.padding,
    this.onTap,
    this.accent,
    this.dense = false,
    this.elevated = false,
  });

  final Widget child;
  final EdgeInsetsGeometry? padding;
  final VoidCallback? onTap;
  final Color? accent;
  final bool dense;
  final bool elevated;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final resolvedPadding =
        padding ?? EdgeInsets.all(dense ? Insets.md : tokens.cardPadding);

    return HoverBuilder(
      onTap: onTap,
      enabled: onTap != null,
      canRequestFocus: onTap != null,
      cursor: onTap == null
          ? SystemMouseCursors.basic
          : SystemMouseCursors.click,
      builder: (context, state) {
        final interactive = onTap != null && state.active;
        return AnimatedContainer(
          duration: context.motion.fast,
          curve: context.motion.standard,
          padding: resolvedPadding,
          decoration: BoxDecoration(
            color: interactive ? palette.surfaceHigh : palette.surface,
            borderRadius: Corners.radiusMd,
            border: Border.all(
              color: state.focused
                  ? palette.accent.withValues(alpha: 0.7)
                  : (interactive ? palette.borderStrong : palette.border),
            ),
            boxShadow: elevated ? tokens.cardShadow : const <BoxShadow>[],
          ),
          child: accent == null
              ? child
              : Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    Container(
                      width: 3,
                      margin: const EdgeInsets.only(right: Insets.md),
                      decoration: BoxDecoration(
                        color: accent,
                        borderRadius: Corners.radiusXs,
                      ),
                    ),
                    Expanded(child: child),
                  ],
                ),
        );
      },
    );
  }
}

/// Section heading with optional trailing actions.
class SectionHeader extends StatelessWidget {
  const SectionHeader({
    super.key,
    required this.title,
    this.subtitle,
    this.trailing,
    this.icon,
  });

  final String title;
  final String? subtitle;
  final Widget? trailing;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    return Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: <Widget>[
        if (icon != null) ...<Widget>[
          Icon(icon, size: 16, color: tokens.color.textSecondary),
          const SizedBox(width: Insets.sm),
        ],
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(title, style: tokens.text.sectionTitle),
              if (subtitle != null) ...<Widget>[
                const SizedBox(height: 2),
                Text(subtitle!, style: tokens.text.secondary),
              ],
            ],
          ),
        ),
        ?trailing,
      ],
    );
  }
}

/// Label/value row used by the security centre and the inspector.
class InfoRow extends StatelessWidget {
  const InfoRow({
    super.key,
    required this.label,
    required this.value,
    this.monospace = false,
    this.valueColor,
    this.trailing,
    this.selectable = false,
  });

  final String label;
  final String value;
  final bool monospace;
  final Color? valueColor;
  final Widget? trailing;
  final bool selectable;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final style = (monospace ? tokens.text.mono : tokens.text.body).copyWith(
      color: valueColor ?? tokens.color.textPrimary,
    );

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: Insets.sm),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          SizedBox(
            width: 168,
            child: Text(label, style: tokens.text.secondary),
          ),
          Expanded(
            child: selectable
                ? SelectableText(value, style: style)
                : Text(value, style: style),
          ),
          if (trailing != null) ...<Widget>[
            const SizedBox(width: Insets.sm),
            trailing!,
          ],
        ],
      ),
    );
  }
}

/// Compact metric tile for the dashboard.
class StatTile extends StatelessWidget {
  const StatTile({
    super.key,
    required this.label,
    required this.value,
    required this.icon,
    this.accent,
    this.onTap,
    this.caption,
  });

  final String label;
  final String value;
  final IconData icon;
  final Color? accent;
  final VoidCallback? onTap;
  final String? caption;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final color = accent ?? tokens.color.accent;

    return AppCard(
      onTap: onTap,
      padding: const EdgeInsets.all(Insets.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Row(
            children: <Widget>[
              Container(
                width: 30,
                height: 30,
                decoration: BoxDecoration(
                  color: color.withValues(alpha: 0.14),
                  borderRadius: Corners.radiusSm,
                ),
                child: Icon(icon, size: 16, color: color),
              ),
              const SizedBox(width: Insets.sm),
              Expanded(
                child: Text(
                  label,
                  style: tokens.text.secondary,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
            ],
          ),
          const SizedBox(height: Insets.md),
          Text(value, style: tokens.text.display.copyWith(fontSize: 26)),
          if (caption != null) ...<Widget>[
            const SizedBox(height: Insets.xs),
            Text(caption!, style: tokens.text.caption),
          ],
        ],
      ),
    );
  }
}

/// Thin horizontal rule matching the design system.
class AppDivider extends StatelessWidget {
  const AppDivider({super.key, this.vertical = false, this.margin});

  final bool vertical;
  final EdgeInsetsGeometry? margin;

  @override
  Widget build(BuildContext context) {
    final color = context.colors.border;
    return Container(
      margin: margin,
      width: vertical ? 1 : null,
      height: vertical ? null : 1,
      color: color,
    );
  }
}
