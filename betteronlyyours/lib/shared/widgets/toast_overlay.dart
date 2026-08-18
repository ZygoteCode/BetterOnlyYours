import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import '../../state/toast_controller.dart';
import 'hover_builder.dart';

/// Bottom-right notification stack. Non-blocking, never covers the primary
/// action area, and animates in and out.
class ToastLayer extends StatefulWidget {
  const ToastLayer({super.key});

  @override
  State<ToastLayer> createState() => _ToastLayerState();
}

class _ToastLayerState extends State<ToastLayer> {
  final List<ToastMessage> _rendered = <ToastMessage>[];
  final Set<int> _leaving = <int>{};
  ToastController? _controller;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final controller = context.read<ToastController>();
    if (identical(controller, _controller)) return;
    _controller?.removeListener(_sync);
    _controller = controller..addListener(_sync);
    _sync();
  }

  @override
  void dispose() {
    _controller?.removeListener(_sync);
    super.dispose();
  }

  void _sync() {
    final controller = _controller;
    if (controller == null || !mounted) return;

    final current = controller.messages;
    final currentIds = current.map((m) => m.id).toSet();

    setState(() {
      for (final message in current) {
        if (!_rendered.any((m) => m.id == message.id)) {
          _rendered.add(message);
        }
      }
      for (final message in List<ToastMessage>.from(_rendered)) {
        if (!currentIds.contains(message.id) && _leaving.add(message.id)) {
          Future<void>.delayed(const Duration(milliseconds: 220), () {
            if (!mounted) return;
            setState(() {
              _rendered.removeWhere((m) => m.id == message.id);
              _leaving.remove(message.id);
            });
          });
        }
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    if (_rendered.isEmpty) return const SizedBox.shrink();

    return Positioned(
      right: Insets.xl,
      bottom: Insets.xl,
      child: IgnorePointer(
        ignoring: false,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.end,
          children: <Widget>[
            for (final message in _rendered)
              Padding(
                padding: const EdgeInsets.only(top: Insets.sm),
                child: _ToastCard(
                  key: ValueKey<int>(message.id),
                  message: message,
                  leaving: _leaving.contains(message.id),
                  onDismiss: () => _controller?.dismiss(message.id),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _ToastCard extends StatefulWidget {
  const _ToastCard({
    super.key,
    required this.message,
    required this.leaving,
    required this.onDismiss,
  });

  final ToastMessage message;
  final bool leaving;
  final VoidCallback onDismiss;

  @override
  State<_ToastCard> createState() => _ToastCardState();
}

class _ToastCardState extends State<_ToastCard> {
  bool _entered = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) setState(() => _entered = true);
    });
  }

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final message = widget.message;

    final color = switch (message.kind) {
      ToastKind.info => palette.accent,
      ToastKind.success => palette.success,
      ToastKind.warning => palette.warning,
      ToastKind.error => palette.danger,
    };
    final icon = switch (message.kind) {
      ToastKind.info => Icons.info_outline_rounded,
      ToastKind.success => Icons.check_circle_outline_rounded,
      ToastKind.warning => Icons.warning_amber_rounded,
      ToastKind.error => Icons.error_outline_rounded,
    };

    final visible = _entered && !widget.leaving;

    return AnimatedSlide(
      duration: context.motion.normal,
      curve: context.motion.standard,
      offset: visible ? Offset.zero : const Offset(0.08, 0.1),
      child: AnimatedOpacity(
        duration: context.motion.normal,
        curve: context.motion.standard,
        opacity: visible ? 1 : 0,
        child: Material(
          color: Colors.transparent,
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 380),
            child: Container(
              padding: const EdgeInsets.all(Insets.md),
              decoration: BoxDecoration(
                color: palette.overlay,
                borderRadius: Corners.radiusMd,
                border: Border.all(color: palette.border),
                boxShadow: tokens.cardShadow,
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Container(
                    width: 28,
                    height: 28,
                    decoration: BoxDecoration(
                      color: color.withValues(alpha: 0.14),
                      borderRadius: Corners.radiusSm,
                    ),
                    child: Icon(icon, size: 15, color: color),
                  ),
                  const SizedBox(width: Insets.md),
                  Flexible(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: <Widget>[
                        Text(message.title, style: tokens.text.bodyStrong),
                        if (message.detail != null) ...<Widget>[
                          const SizedBox(height: 2),
                          Text(message.detail!, style: tokens.text.secondary),
                        ],
                        if (message.action != null) ...<Widget>[
                          const SizedBox(height: Insets.sm),
                          HoverBuilder(
                            onTap: () {
                              message.action!.onPressed();
                              widget.onDismiss();
                            },
                            builder: (context, state) => Text(
                              message.action!.label,
                              style: tokens.text.bodyStrong.copyWith(
                                color: color,
                                decoration: state.hovered
                                    ? TextDecoration.underline
                                    : TextDecoration.none,
                                decorationColor: color,
                              ),
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                  const SizedBox(width: Insets.sm),
                  HoverBuilder(
                    onTap: widget.onDismiss,
                    builder: (context, state) => Icon(
                      Icons.close_rounded,
                      size: 15,
                      color: state.hovered
                          ? palette.textPrimary
                          : palette.textTertiary,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
