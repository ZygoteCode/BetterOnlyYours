import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../app/theme/tokens.dart';

/// The single text input component.
///
/// Handles the desktop focus quirk where a field created during a build never
/// receives focus unless the request happens after the first frame.
class AppTextField extends StatefulWidget {
  const AppTextField({
    super.key,
    required this.controller,
    this.label,
    this.hint,
    this.helper,
    this.errorText,
    this.obscureText = false,
    this.monospace = false,
    this.autofocus = false,
    this.enabled = true,
    this.readOnly = false,
    this.maxLines = 1,
    this.minLines,
    this.expands = false,
    this.prefixIcon,
    this.suffix,
    this.focusNode,
    this.onChanged,
    this.onSubmitted,
    this.onTap,
    this.textInputAction,
    this.inputFormatters,
    this.textAlignVertical,
    this.semanticLabel,
  });

  final TextEditingController controller;
  final String? label;
  final String? hint;
  final String? helper;
  final String? errorText;
  final bool obscureText;
  final bool monospace;
  final bool autofocus;
  final bool enabled;
  final bool readOnly;
  final int? maxLines;
  final int? minLines;
  final bool expands;
  final IconData? prefixIcon;
  final Widget? suffix;
  final FocusNode? focusNode;
  final ValueChanged<String>? onChanged;
  final ValueChanged<String>? onSubmitted;
  final VoidCallback? onTap;
  final TextInputAction? textInputAction;
  final List<TextInputFormatter>? inputFormatters;
  final TextAlignVertical? textAlignVertical;
  final String? semanticLabel;

  @override
  State<AppTextField> createState() => _AppTextFieldState();
}

class _AppTextFieldState extends State<AppTextField> {
  FocusNode? _ownedNode;
  bool _focused = false;

  FocusNode get _node => widget.focusNode ?? (_ownedNode ??= FocusNode());

  @override
  void initState() {
    super.initState();
    _node.addListener(_onFocusChanged);
    if (widget.autofocus) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted && _node.canRequestFocus && !_node.hasFocus) {
          _node.requestFocus();
        }
      });
    }
  }

  @override
  void dispose() {
    _node.removeListener(_onFocusChanged);
    _ownedNode?.dispose();
    super.dispose();
  }

  void _onFocusChanged() {
    if (!mounted) return;
    setState(() => _focused = _node.hasFocus);
  }

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final hasError = widget.errorText != null && widget.errorText!.isNotEmpty;

    final borderColor = hasError
        ? palette.danger
        : (_focused ? palette.accent : palette.border);

    final field = AnimatedContainer(
      duration: context.motion.fast,
      curve: context.motion.standard,
      decoration: BoxDecoration(
        color: widget.enabled
            ? palette.surface
            : palette.surface.withValues(alpha: 0.5),
        borderRadius: Corners.radiusSm,
        border: Border.all(color: borderColor, width: _focused ? 1.4 : 1),
        boxShadow: _focused && !hasError
            ? <BoxShadow>[
                BoxShadow(
                  color: palette.accent.withValues(alpha: 0.18),
                  blurRadius: 14,
                  spreadRadius: -4,
                ),
              ]
            : const <BoxShadow>[],
      ),
      padding: EdgeInsets.symmetric(
        horizontal: Insets.md,
        vertical: widget.expands || (widget.maxLines ?? 1) > 1
            ? Insets.md
            : Insets.xs,
      ),
      child: Row(
        crossAxisAlignment: widget.expands || (widget.maxLines ?? 1) > 1
            ? CrossAxisAlignment.start
            : CrossAxisAlignment.center,
        children: <Widget>[
          if (widget.prefixIcon != null) ...<Widget>[
            Icon(
              widget.prefixIcon,
              size: 16,
              color: _focused ? palette.accent : palette.textTertiary,
            ),
            const SizedBox(width: Insets.sm),
          ],
          Expanded(
            child: TextField(
              controller: widget.controller,
              focusNode: _node,
              obscureText: widget.obscureText,
              enabled: widget.enabled,
              readOnly: widget.readOnly,
              maxLines: widget.expands ? null : widget.maxLines,
              minLines: widget.expands ? null : widget.minLines,
              expands: widget.expands,
              onChanged: widget.onChanged,
              onSubmitted: widget.onSubmitted,
              onTap: widget.onTap,
              textInputAction: widget.textInputAction,
              inputFormatters: widget.inputFormatters,
              textAlignVertical:
                  widget.textAlignVertical ?? TextAlignVertical.center,
              cursorColor: palette.accent,
              cursorWidth: 1.6,
              cursorRadius: const Radius.circular(2),
              style: widget.monospace
                  ? tokens.text.mono
                  : tokens.text.body.copyWith(height: 1.4),
              decoration: InputDecoration(
                isDense: true,
                border: InputBorder.none,
                enabledBorder: InputBorder.none,
                focusedBorder: InputBorder.none,
                disabledBorder: InputBorder.none,
                contentPadding: EdgeInsets.symmetric(
                  vertical: widget.expands || (widget.maxLines ?? 1) > 1
                      ? 0
                      : Insets.sm + 2,
                ),
                hintText: widget.hint,
                hintStyle:
                    (widget.monospace ? tokens.text.mono : tokens.text.body)
                        .copyWith(color: palette.textTertiary),
              ),
            ),
          ),
          if (widget.suffix != null) ...<Widget>[
            const SizedBox(width: Insets.xs),
            widget.suffix!,
          ],
        ],
      ),
    );

    if (widget.label == null && widget.helper == null && !hasError) {
      return Semantics(
        textField: true,
        label: widget.semanticLabel ?? widget.hint,
        child: field,
      );
    }

    return Semantics(
      textField: true,
      label: widget.semanticLabel ?? widget.label ?? widget.hint,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          if (widget.label != null) ...<Widget>[
            Text(widget.label!.toUpperCase(), style: tokens.text.label),
            const SizedBox(height: Insets.xs + 2),
          ],
          widget.expands ? Expanded(child: field) : field,
          if (hasError) ...<Widget>[
            const SizedBox(height: Insets.xs + 2),
            Row(
              children: <Widget>[
                Icon(
                  Icons.error_outline_rounded,
                  size: 13,
                  color: palette.danger,
                ),
                const SizedBox(width: Insets.xs),
                Expanded(
                  child: Text(
                    widget.errorText!,
                    style: tokens.text.caption.copyWith(color: palette.danger),
                  ),
                ),
              ],
            ),
          ] else if (widget.helper != null) ...<Widget>[
            const SizedBox(height: Insets.xs + 2),
            Text(widget.helper!, style: tokens.text.caption),
          ],
        ],
      ),
    );
  }
}
