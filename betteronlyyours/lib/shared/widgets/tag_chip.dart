import 'package:material_ui/material_ui.dart';

import '../../app/theme/tokens.dart';
import 'hover_builder.dart';

/// Small pill used for tags and filters.
class TagChip extends StatelessWidget {
  const TagChip({
    super.key,
    required this.label,
    this.selected = false,
    this.onTap,
    this.onRemove,
    this.count,
    this.icon,
    this.color,
  });

  final String label;
  final bool selected;
  final VoidCallback? onTap;
  final VoidCallback? onRemove;
  final int? count;
  final IconData? icon;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final accent = color ?? palette.accent;

    return HoverBuilder(
      onTap: onTap,
      enabled: onTap != null,
      canRequestFocus: onTap != null,
      builder: (context, state) {
        final active = selected || state.active;
        return AnimatedContainer(
          duration: context.motion.fast,
          curve: context.motion.standard,
          padding: EdgeInsets.only(
            left: icon == null ? Insets.md : Insets.sm,
            right: onRemove != null ? Insets.xs : Insets.md,
            top: Insets.xs + 1,
            bottom: Insets.xs + 1,
          ),
          decoration: BoxDecoration(
            color: selected
                ? accent.withValues(alpha: 0.18)
                : (state.active ? palette.surfaceHigh : palette.surface),
            borderRadius: Corners.radiusSm,
            border: Border.all(
              color: selected
                  ? accent.withValues(alpha: 0.55)
                  : (active ? palette.borderStrong : palette.border),
            ),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              if (icon != null) ...<Widget>[
                Icon(
                  icon,
                  size: 13,
                  color: selected ? accent : palette.textSecondary,
                ),
                const SizedBox(width: Insets.xs + 2),
              ],
              Text(
                label,
                style: tokens.text.caption.copyWith(
                  color: selected ? accent : palette.textSecondary,
                  fontWeight: selected ? FontWeight.w600 : FontWeight.w500,
                  fontSize: 11.5,
                ),
              ),
              if (count != null) ...<Widget>[
                const SizedBox(width: Insets.xs + 2),
                Text(
                  '$count',
                  style: tokens.text.caption.copyWith(
                    color: palette.textTertiary,
                    fontSize: 11,
                  ),
                ),
              ],
              if (onRemove != null) ...<Widget>[
                const SizedBox(width: Insets.xs),
                GestureDetector(
                  onTap: onRemove,
                  child: Icon(
                    Icons.close_rounded,
                    size: 13,
                    color: palette.textTertiary,
                  ),
                ),
              ],
            ],
          ),
        );
      },
    );
  }
}
