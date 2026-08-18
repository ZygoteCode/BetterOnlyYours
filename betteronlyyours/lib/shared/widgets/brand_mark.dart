import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';

/// The BetterOnlyYours mark: a rounded shield in the accent gradient.
class BrandMark extends StatelessWidget {
  const BrandMark({
    super.key,
    this.size = 22,
    this.glow = false,
    this.locked = true,
  });

  final double size;
  final bool glow;
  final bool locked;

  @override
  Widget build(BuildContext context) {
    final palette = context.colors;
    return AnimatedContainer(
      duration: context.motion.slow,
      curve: context.motion.standard,
      width: size,
      height: size,
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: <Color>[
            palette.accentStrong,
            palette.accent,
            palette.secondary,
          ],
          stops: const <double>[0, 0.55, 1],
        ),
        borderRadius: BorderRadius.circular(size * 0.3),
        boxShadow: glow
            ? <BoxShadow>[
                BoxShadow(
                  color: palette.accent.withValues(alpha: 0.45),
                  blurRadius: size * 0.9,
                  spreadRadius: -size * 0.2,
                ),
              ]
            : const <BoxShadow>[],
      ),
      child: Center(
        child: Icon(
          locked ? Icons.lock_rounded : Icons.lock_open_rounded,
          size: size * 0.52,
          color: Colors.white.withValues(alpha: 0.94),
        ),
      ),
    );
  }
}

/// Wordmark used on the title bar and the authentication screens.
class BrandWordmark extends StatelessWidget {
  const BrandWordmark({super.key, this.fontSize = 13, this.dim = false});

  final double fontSize;
  final bool dim;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    return RichText(
      text: TextSpan(
        children: <TextSpan>[
          TextSpan(
            text: 'Better',
            style: tokens.text.bodyStrong.copyWith(
              fontSize: fontSize,
              color: dim ? palette.textSecondary : palette.textPrimary,
              letterSpacing: 0.2,
            ),
          ),
          TextSpan(
            text: 'OnlyYours',
            style: tokens.text.bodyStrong.copyWith(
              fontSize: fontSize,
              color: palette.accent,
              letterSpacing: 0.2,
            ),
          ),
        ],
      ),
    );
  }
}
