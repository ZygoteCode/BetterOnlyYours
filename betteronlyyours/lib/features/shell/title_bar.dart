import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import '../../app/window_controller.dart';
import '../../shared/widgets/brand_mark.dart';
import '../../shared/widgets/hover_builder.dart';

/// Custom window frame header: drag region, brand, contextual label and the
/// window controls. Double-clicking the bar maximises or restores, matching
/// the platform behaviour users expect.
class AppTitleBar extends StatelessWidget {
  const AppTitleBar({
    super.key,
    this.contextLabel,
    this.actions = const <Widget>[],
  });

  final String? contextLabel;
  final List<Widget> actions;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final window = context.watch<WindowController>();

    return SizedBox(
      height: tokens.titleBarHeight,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: palette.backgroundElevated,
          border: Border(bottom: BorderSide(color: palette.border)),
        ),
        child: Row(
          children: <Widget>[
            Expanded(
              child: GestureDetector(
                behavior: HitTestBehavior.opaque,
                onPanStart: (_) => window.startDragging(),
                onDoubleTap: window.toggleMaximize,
                child: Padding(
                  padding: const EdgeInsets.only(left: Insets.md),
                  child: Row(
                    children: <Widget>[
                      const BrandMark(size: 18, locked: false),
                      const SizedBox(width: Insets.sm),
                      const BrandWordmark(fontSize: 12.5),
                      if (contextLabel != null) ...<Widget>[
                        Container(
                          width: 1,
                          height: 14,
                          margin: const EdgeInsets.symmetric(
                            horizontal: Insets.md,
                          ),
                          color: palette.border,
                        ),
                        Flexible(
                          child: Text(
                            contextLabel!,
                            overflow: TextOverflow.ellipsis,
                            style: tokens.text.caption.copyWith(
                              color: palette.textSecondary,
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ),
            ),
            if (actions.isNotEmpty) ...<Widget>[
              ...actions,
              const SizedBox(width: Insets.sm),
            ],
            _WindowButton(
              icon: Icons.remove_rounded,
              tooltip: 'Minimize',
              onPressed: window.minimize,
            ),
            _WindowButton(
              icon: window.isMaximized
                  ? Icons.filter_none_rounded
                  : Icons.crop_square_rounded,
              tooltip: window.isMaximized ? 'Restore' : 'Maximize',
              iconSize: window.isMaximized ? 12 : 14,
              onPressed: window.toggleMaximize,
            ),
            _WindowButton(
              icon: Icons.close_rounded,
              tooltip: 'Close',
              danger: true,
              onPressed: window.close,
            ),
          ],
        ),
      ),
    );
  }
}

class _WindowButton extends StatelessWidget {
  const _WindowButton({
    required this.icon,
    required this.tooltip,
    required this.onPressed,
    this.danger = false,
    this.iconSize = 15,
  });

  final IconData icon;
  final String tooltip;
  final VoidCallback onPressed;
  final bool danger;
  final double iconSize;

  @override
  Widget build(BuildContext context) {
    final palette = context.colors;
    return Tooltip(
      message: tooltip,
      child: HoverBuilder(
        onTap: onPressed,
        canRequestFocus: false,
        builder: (context, state) {
          final background = state.pressed
              ? (danger
                    ? palette.danger.withValues(alpha: 0.85)
                    : palette.surfaceHigh.withValues(alpha: 0.8))
              : state.hovered
              ? (danger ? palette.danger : palette.surfaceHigh)
              : Colors.transparent;

          return AnimatedContainer(
            duration: context.motion.instant,
            width: 46,
            height: context.tokens.titleBarHeight,
            color: background,
            child: Icon(
              icon,
              size: iconSize,
              color: state.hovered && danger
                  ? Colors.white
                  : palette.textSecondary,
            ),
          );
        },
      ),
    );
  }
}
