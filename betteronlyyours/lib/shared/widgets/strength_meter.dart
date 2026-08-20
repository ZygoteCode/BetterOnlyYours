import 'package:material_ui/material_ui.dart';

import '../../app/theme/tokens.dart';
import '../../l10n/l10n.dart';
import '../../core/services/password_strength.dart';

/// Segmented strength indicator.
///
/// Communicates with shape *and* colour (filled segments plus a label), so it
/// still reads without colour perception.
class StrengthMeter extends StatelessWidget {
  const StrengthMeter({
    super.key,
    required this.strength,
    this.showLabel = true,
    this.showEntropy = true,
    this.compact = false,
  });

  final PasswordStrength strength;
  final bool showLabel;
  final bool showEntropy;
  final bool compact;

  static Color colorFor(BuildContext context, StrengthLevel level) {
    final palette = context.colors;
    return switch (level) {
      StrengthLevel.empty => palette.border,
      StrengthLevel.veryWeak => palette.danger,
      StrengthLevel.weak => palette.danger,
      StrengthLevel.fair => palette.warning,
      StrengthLevel.strong => palette.secondary,
      StrengthLevel.excellent => palette.success,
    };
  }

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final l10n = context.l10n;
    final color = colorFor(context, strength.level);
    final filled = switch (strength.level) {
      StrengthLevel.empty => 0,
      StrengthLevel.veryWeak => 1,
      StrengthLevel.weak => 2,
      StrengthLevel.fair => 3,
      StrengthLevel.strong => 4,
      StrengthLevel.excellent => 5,
    };

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Row(
          children: List<Widget>.generate(5, (index) {
            final active = index < filled;
            return Expanded(
              child: Padding(
                padding: EdgeInsets.only(right: index == 4 ? 0 : Insets.xs),
                child: AnimatedContainer(
                  duration: context.motion.normal,
                  curve: context.motion.standard,
                  height: compact ? 3 : 5,
                  decoration: BoxDecoration(
                    color: active
                        ? color
                        : palette.surfaceHigh.withValues(alpha: 0.9),
                    borderRadius: Corners.radiusXs,
                  ),
                ),
              ),
            );
          }),
        ),
        if (showLabel || showEntropy) ...<Widget>[
          const SizedBox(height: Insets.sm),
          Row(
            children: <Widget>[
              if (showLabel)
                Text(
                  strength.level.localizedLabel(l10n),
                  style: tokens.text.caption.copyWith(
                    color: color,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              const Spacer(),
              if (showEntropy && strength.level != StrengthLevel.empty)
                Text(
                  l10n.strengthBits(strength.entropyBits.round()),
                  style: tokens.text.caption,
                ),
            ],
          ),
        ],
      ],
    );
  }
}
