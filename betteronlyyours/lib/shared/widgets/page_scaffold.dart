import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';
import '../animations/entrance.dart';

/// Shared layout for the full-width destinations (generator, security,
/// settings): headline, optional actions, and a width-constrained body so
/// text never stretches across a 4K display.
class PageScaffold extends StatelessWidget {
  const PageScaffold({
    super.key,
    required this.title,
    required this.child,
    this.subtitle,
    this.actions = const <Widget>[],
    this.maxWidth = 860,
    this.icon,
  });

  final String title;
  final String? subtitle;
  final Widget child;
  final List<Widget> actions;
  final double maxWidth;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;

    return Scrollbar(
      child: SingleChildScrollView(
        padding: EdgeInsets.all(tokens.panePadding),
        child: Center(
          child: ConstrainedBox(
            constraints: BoxConstraints(maxWidth: maxWidth),
            child: EntranceFade(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: <Widget>[
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisSize: MainAxisSize.min,
                          children: <Widget>[
                            Row(
                              children: <Widget>[
                                if (icon != null) ...<Widget>[
                                  Icon(
                                    icon,
                                    size: 22,
                                    color: tokens.color.accent,
                                  ),
                                  const SizedBox(width: Insets.md),
                                ],
                                Flexible(
                                  child: Text(
                                    title,
                                    style: tokens.text.display,
                                  ),
                                ),
                              ],
                            ),
                            if (subtitle != null) ...<Widget>[
                              const SizedBox(height: Insets.xs),
                              Text(subtitle!, style: tokens.text.secondary),
                            ],
                          ],
                        ),
                      ),
                      if (actions.isNotEmpty)
                        Wrap(spacing: Insets.sm, children: actions),
                    ],
                  ),
                  const SizedBox(height: Insets.xxl),
                  child,
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
