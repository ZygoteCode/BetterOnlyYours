import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';

/// Soft accent glow behind the lock and vault-creation cards.
class AuthBackdrop extends StatelessWidget {
  const AuthBackdrop({super.key});

  @override
  Widget build(BuildContext context) {
    final palette = context.colors;
    return DecoratedBox(
      decoration: BoxDecoration(
        gradient: RadialGradient(
          center: const Alignment(0, -0.35),
          radius: 1.1,
          colors: <Color>[
            palette.accent.withValues(alpha: 0.14),
            palette.background,
          ],
          stops: const <double>[0, 0.7],
        ),
      ),
      child: const SizedBox.expand(),
    );
  }
}
