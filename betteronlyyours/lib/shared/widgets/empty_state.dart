import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';
import '../animations/entrance.dart';
import 'app_button.dart';

/// Guided empty state: says what is missing and what to do about it.
class EmptyState extends StatelessWidget {
  const EmptyState({
    super.key,
    required this.icon,
    required this.title,
    required this.message,
    this.actionLabel,
    this.onAction,
    this.secondaryActionLabel,
    this.onSecondaryAction,
    this.compact = false,
    this.hint,
  });

  final IconData icon;
  final String title;
  final String message;
  final String? actionLabel;
  final VoidCallback? onAction;
  final String? secondaryActionLabel;
  final VoidCallback? onSecondaryAction;
  final bool compact;
  final String? hint;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;

    return Center(
      child: EntranceFade(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 420),
          child: Padding(
            padding: const EdgeInsets.all(Insets.xxl),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.center,
              children: <Widget>[
                Container(
                  width: compact ? 44 : 60,
                  height: compact ? 44 : 60,
                  decoration: BoxDecoration(
                    color: palette.surface,
                    borderRadius: Corners.radiusLg,
                    border: Border.all(color: palette.border),
                  ),
                  child: Icon(
                    icon,
                    size: compact ? 20 : 26,
                    color: palette.textSecondary,
                  ),
                ),
                SizedBox(height: compact ? Insets.md : Insets.lg),
                Text(
                  title,
                  textAlign: TextAlign.center,
                  style: compact
                      ? tokens.text.sectionTitle
                      : tokens.text.pageTitle.copyWith(fontSize: 18),
                ),
                const SizedBox(height: Insets.sm),
                Text(
                  message,
                  textAlign: TextAlign.center,
                  style: tokens.text.secondary,
                ),
                if (actionLabel != null || secondaryActionLabel != null) ...[
                  const SizedBox(height: Insets.xl),
                  Wrap(
                    spacing: Insets.sm,
                    runSpacing: Insets.sm,
                    alignment: WrapAlignment.center,
                    children: <Widget>[
                      if (actionLabel != null)
                        AppButton(
                          label: actionLabel!,
                          onPressed: onAction,
                          icon: Icons.add_rounded,
                        ),
                      if (secondaryActionLabel != null)
                        AppButton(
                          label: secondaryActionLabel!,
                          onPressed: onSecondaryAction,
                          variant: AppButtonVariant.secondary,
                        ),
                    ],
                  ),
                ],
                if (hint != null) ...<Widget>[
                  const SizedBox(height: Insets.lg),
                  Text(
                    hint!,
                    textAlign: TextAlign.center,
                    style: tokens.text.caption,
                  ),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}
