import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

/// Rebuilds its child with hover / pressed / focused state.
///
/// Used instead of Material ink effects so desktop interactions feel crisp
/// and every component animates with the same tokens.
class HoverBuilder extends StatefulWidget {
  const HoverBuilder({
    super.key,
    required this.builder,
    this.onTap,
    this.onSecondaryTapDown,
    this.onDoubleTap,
    this.cursor = SystemMouseCursors.click,
    this.focusNode,
    this.autofocus = false,
    this.canRequestFocus = true,
    this.enabled = true,
  });

  final Widget Function(BuildContext context, HoverState state) builder;
  final VoidCallback? onTap;
  final GestureTapDownCallback? onSecondaryTapDown;
  final VoidCallback? onDoubleTap;
  final MouseCursor cursor;
  final FocusNode? focusNode;
  final bool autofocus;
  final bool canRequestFocus;
  final bool enabled;

  @override
  State<HoverBuilder> createState() => _HoverBuilderState();
}

class HoverState {
  const HoverState({
    required this.hovered,
    required this.pressed,
    required this.focused,
  });

  final bool hovered;
  final bool pressed;
  final bool focused;

  bool get active => hovered || pressed || focused;
}

class _HoverBuilderState extends State<HoverBuilder> {
  bool _hovered = false;
  bool _pressed = false;
  bool _focused = false;

  void _set(void Function() mutate) {
    if (!mounted) return;
    setState(mutate);
  }

  @override
  Widget build(BuildContext context) {
    final enabled = widget.enabled && widget.onTap != null;

    Widget child = widget.builder(
      context,
      HoverState(
        hovered: _hovered && widget.enabled,
        pressed: _pressed,
        focused: _focused,
      ),
    );

    child = GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTap: widget.enabled ? widget.onTap : null,
      onDoubleTap: widget.enabled ? widget.onDoubleTap : null,
      onSecondaryTapDown: widget.enabled ? widget.onSecondaryTapDown : null,
      onTapDown: enabled ? (_) => _set(() => _pressed = true) : null,
      onTapUp: enabled ? (_) => _set(() => _pressed = false) : null,
      onTapCancel: enabled ? () => _set(() => _pressed = false) : null,
      child: child,
    );

    return Focus(
      focusNode: widget.focusNode,
      autofocus: widget.autofocus,
      canRequestFocus: widget.canRequestFocus && widget.enabled,
      onFocusChange: (value) => _set(() => _focused = value),
      onKeyEvent: (node, event) {
        if (!widget.enabled || widget.onTap == null) {
          return KeyEventResult.ignored;
        }
        final isActivation =
            event.logicalKey == LogicalKeyboardKey.enter ||
            event.logicalKey == LogicalKeyboardKey.space ||
            event.logicalKey == LogicalKeyboardKey.numpadEnter;
        if (event is KeyDownEvent && isActivation) {
          widget.onTap!.call();
          return KeyEventResult.handled;
        }
        return KeyEventResult.ignored;
      },
      child: MouseRegion(
        cursor: widget.enabled ? widget.cursor : SystemMouseCursors.basic,
        onEnter: (_) => _set(() => _hovered = true),
        onExit: (_) => _set(() {
          _hovered = false;
          _pressed = false;
        }),
        child: child,
      ),
    );
  }
}
