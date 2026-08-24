import 'dart:math' as math;

import 'package:material_ui/material_ui.dart';

import '../../app/theme/palette.dart';
import '../../app/theme/tokens.dart';

/// The countdown every authenticator draws around a token: a ring that empties
/// as the code ages, warms from accent to amber to red on the way out, and
/// carries the remaining seconds in its middle in the same colour.
class TotpRing extends StatelessWidget {
  const TotpRing({
    super.key,
    required this.progress,
    required this.secondsRemaining,
    this.size = 46,
    this.strokeWidth = 3.5,
  });

  /// 1.0 right after a refresh, 0.0 at expiry.
  final double progress;
  final int secondsRemaining;
  final double size;
  final double strokeWidth;

  /// Accent while there is time, amber when it gets short, red at the end.
  /// Interpolated rather than stepped, so the change reads as the code ageing
  /// instead of a state flipping.
  static Color colorFor(AppPalette palette, double progress) {
    final value = progress.clamp(0.0, 1.0);
    if (value >= 0.5) return palette.accent;
    if (value >= 0.2) {
      return Color.lerp(palette.warning, palette.accent, (value - 0.2) / 0.3)!;
    }
    return Color.lerp(palette.danger, palette.warning, value / 0.2)!;
  }

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final color = colorFor(tokens.color, progress);

    return SizedBox(
      width: size,
      height: size,
      child: Semantics(
        label: '$secondsRemaining',
        child: CustomPaint(
          painter: _TotpRingPainter(
            progress: progress.clamp(0.0, 1.0),
            color: color,
            track: tokens.color.surfaceHigh,
            strokeWidth: strokeWidth,
          ),
          child: Center(
            child: Text(
              '$secondsRemaining',
              style: tokens.text.mono.copyWith(
                color: color,
                fontSize: size * 0.3,
                fontWeight: FontWeight.w600,
                height: 1,
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _TotpRingPainter extends CustomPainter {
  const _TotpRingPainter({
    required this.progress,
    required this.color,
    required this.track,
    required this.strokeWidth,
  });

  final double progress;
  final Color color;
  final Color track;
  final double strokeWidth;

  @override
  void paint(Canvas canvas, Size size) {
    final center = Offset(size.width / 2, size.height / 2);
    final radius = (math.min(size.width, size.height) - strokeWidth) / 2;

    // The disc behind the digits warms up as the code runs out, so the state
    // is readable from the corner of the eye without counting seconds.
    canvas.drawCircle(
      center,
      radius - strokeWidth / 2,
      Paint()..color = color.withValues(alpha: 0.06 + 0.12 * (1 - progress)),
    );

    canvas.drawCircle(
      center,
      radius,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = strokeWidth
        ..color = track,
    );

    if (progress <= 0) return;

    canvas.drawArc(
      Rect.fromCircle(center: center, radius: radius),
      -math.pi / 2,
      2 * math.pi * progress,
      false,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = strokeWidth
        ..strokeCap = StrokeCap.round
        ..color = color,
    );
  }

  @override
  bool shouldRepaint(_TotpRingPainter old) =>
      old.progress != progress ||
      old.color != color ||
      old.track != track ||
      old.strokeWidth != strokeWidth;
}
