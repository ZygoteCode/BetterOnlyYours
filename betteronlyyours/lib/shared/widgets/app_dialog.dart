import 'dart:ui';

import 'package:material_ui/material_ui.dart';

import '../../app/theme/tokens.dart';
import '../../l10n/l10n.dart';
import 'app_button.dart';

/// Shows a modal with the app's own transition: quick fade + subtle scale,
/// with a blurred backdrop. Escape and barrier taps dismiss it.
Future<T?> showAppDialog<T>({
  required BuildContext context,
  required WidgetBuilder builder,
  bool dismissible = true,
}) {
  final motion = context.motion;
  return showGeneralDialog<T>(
    context: context,
    barrierDismissible: dismissible,
    barrierLabel: MaterialLocalizations.of(context).modalBarrierDismissLabel,
    barrierColor: Colors.black.withValues(alpha: 0.55),
    transitionDuration: motion.normal,
    pageBuilder: (context, _, _) => builder(context),
    transitionBuilder: (context, animation, _, child) {
      final curved = CurvedAnimation(
        parent: animation,
        curve: motion.emphasized,
        reverseCurve: motion.exit,
      );
      return FadeTransition(
        opacity: curved,
        child: BackdropFilter(
          filter: ImageFilter.blur(
            sigmaX: 6 * curved.value,
            sigmaY: 6 * curved.value,
          ),
          child: ScaleTransition(
            scale: Tween<double>(begin: 0.97, end: 1).animate(curved),
            child: child,
          ),
        ),
      );
    },
  );
}

/// Standard dialog frame: icon + title + optional subtitle, content, actions.
class AppDialog extends StatelessWidget {
  const AppDialog({
    super.key,
    required this.title,
    required this.content,
    this.subtitle,
    this.icon,
    this.iconColor,
    this.actions = const <Widget>[],
    this.width = 460,
    this.scrollable = false,
  });

  final String title;
  final String? subtitle;
  final Widget content;
  final IconData? icon;
  final Color? iconColor;
  final List<Widget> actions;
  final double width;
  final bool scrollable;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final accent = iconColor ?? palette.accent;

    return Center(
      child: ConstrainedBox(
        constraints: BoxConstraints(
          maxWidth: width,
          maxHeight: MediaQuery.sizeOf(context).height - 96,
        ),
        child: Material(
          color: Colors.transparent,
          child: Container(
            decoration: BoxDecoration(
              color: palette.overlay,
              borderRadius: Corners.radiusLg,
              border: Border.all(color: palette.border),
              boxShadow: tokens.overlayShadow,
            ),
            padding: const EdgeInsets.all(Insets.xxl),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    if (icon != null) ...<Widget>[
                      Container(
                        width: 36,
                        height: 36,
                        decoration: BoxDecoration(
                          color: accent.withValues(alpha: 0.14),
                          borderRadius: Corners.radiusSm,
                        ),
                        child: Icon(icon, size: 18, color: accent),
                      ),
                      const SizedBox(width: Insets.md),
                    ],
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: <Widget>[
                          Text(
                            title,
                            style: tokens.text.pageTitle.copyWith(fontSize: 18),
                          ),
                          if (subtitle != null) ...<Widget>[
                            const SizedBox(height: Insets.xs),
                            Text(subtitle!, style: tokens.text.secondary),
                          ],
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: Insets.xl),
                if (scrollable)
                  Flexible(child: SingleChildScrollView(child: content))
                else
                  Flexible(child: content),
                if (actions.isNotEmpty) ...<Widget>[
                  const SizedBox(height: Insets.xxl),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.end,
                    children: <Widget>[
                      for (var i = 0; i < actions.length; i++) ...<Widget>[
                        if (i > 0) const SizedBox(width: Insets.sm),
                        actions[i],
                      ],
                    ],
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

/// Confirmation used for destructive actions. Always names the target.
Future<bool> showConfirmDialog({
  required BuildContext context,
  required String title,
  required String message,
  String? confirmLabel,
  String? cancelLabel,
  bool destructive = false,
  String? detail,
  IconData icon = Icons.help_outline_rounded,
}) async {
  final result = await showAppDialog<bool>(
    context: context,
    builder: (dialogContext) {
      final tokens = dialogContext.tokens;
      return AppDialog(
        title: title,
        icon: icon,
        iconColor: destructive ? tokens.color.danger : tokens.color.accent,
        width: 420,
        content: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(message, style: tokens.text.body),
            if (detail != null) ...<Widget>[
              const SizedBox(height: Insets.md),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(Insets.md),
                decoration: BoxDecoration(
                  color: tokens.color.surface,
                  borderRadius: Corners.radiusSm,
                  border: Border.all(color: tokens.color.border),
                ),
                child: Text(detail, style: tokens.text.mono),
              ),
            ],
          ],
        ),
        actions: <Widget>[
          AppButton(
            label: cancelLabel ?? dialogContext.l10n.commonCancel,
            variant: AppButtonVariant.ghost,
            onPressed: () => Navigator.of(dialogContext).pop(false),
          ),
          AppButton(
            label: confirmLabel ?? dialogContext.l10n.commonConfirm,
            variant: destructive
                ? AppButtonVariant.danger
                : AppButtonVariant.primary,
            autofocus: true,
            onPressed: () => Navigator.of(dialogContext).pop(true),
          ),
        ],
      );
    },
  );
  return result ?? false;
}
