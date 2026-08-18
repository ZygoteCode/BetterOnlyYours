import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';
import '../../core/services/password_strength.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_text_field.dart';
import '../../shared/widgets/strength_meter.dart';

/// Password input with reveal, copy and generate affordances.
///
/// Secrets are hidden by default; revealing is always an explicit action.
class SecretField extends StatefulWidget {
  const SecretField({
    super.key,
    required this.controller,
    this.label,
    this.hint = 'Password',
    this.revealByDefault = false,
    this.showStrength = true,
    this.onGenerate,
    this.onCopy,
    this.onChanged,
    this.onSubmitted,
    this.focusNode,
    this.autofocus = false,
    this.enabled = true,
  });

  final TextEditingController controller;
  final String? label;
  final String hint;
  final bool revealByDefault;
  final bool showStrength;
  final VoidCallback? onGenerate;
  final VoidCallback? onCopy;
  final ValueChanged<String>? onChanged;
  final ValueChanged<String>? onSubmitted;
  final FocusNode? focusNode;
  final bool autofocus;
  final bool enabled;

  @override
  State<SecretField> createState() => _SecretFieldState();
}

class _SecretFieldState extends State<SecretField> {
  late bool _revealed = widget.revealByDefault;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final value = widget.controller.text;
    final strength = PasswordStrength.evaluate(value);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        AppTextField(
          controller: widget.controller,
          label: widget.label,
          hint: widget.hint,
          obscureText: !_revealed,
          monospace: true,
          enabled: widget.enabled,
          focusNode: widget.focusNode,
          autofocus: widget.autofocus,
          onChanged: (text) {
            widget.onChanged?.call(text);
            setState(() {});
          },
          onSubmitted: widget.onSubmitted,
          suffix: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              AppIconButton(
                icon: _revealed
                    ? Icons.visibility_off_outlined
                    : Icons.visibility_outlined,
                tooltip: _revealed ? 'Hide password' : 'Reveal password',
                dense: true,
                size: 16,
                onPressed: widget.enabled
                    ? () => setState(() => _revealed = !_revealed)
                    : null,
              ),
              if (widget.onCopy != null)
                AppIconButton(
                  icon: Icons.copy_rounded,
                  tooltip: 'Copy password  ·  Ctrl+Shift+C',
                  dense: true,
                  size: 15,
                  onPressed: value.isEmpty ? null : widget.onCopy,
                ),
              if (widget.onGenerate != null)
                AppIconButton(
                  icon: Icons.auto_awesome_rounded,
                  tooltip: 'Generate a new password',
                  dense: true,
                  size: 15,
                  onPressed: widget.enabled ? widget.onGenerate : null,
                ),
            ],
          ),
        ),
        if (widget.showStrength && value.isNotEmpty) ...<Widget>[
          const SizedBox(height: Insets.sm),
          StrengthMeter(strength: strength, compact: true),
          if (strength.suggestions.isNotEmpty) ...<Widget>[
            const SizedBox(height: Insets.xs),
            Text(strength.suggestions.first, style: tokens.text.caption),
          ],
        ],
      ],
    );
  }
}
