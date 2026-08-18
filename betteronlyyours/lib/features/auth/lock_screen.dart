import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import 'auth_backdrop.dart';
import '../../core/security/vault_exception.dart';
import '../../shared/animations/entrance.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_text_field.dart';
import '../../shared/widgets/brand_mark.dart';
import '../../state/shell_controller.dart';
import '../../state/vault_controller.dart';

/// Authentication screen for an existing vault.
class LockScreen extends StatefulWidget {
  const LockScreen({super.key});

  @override
  State<LockScreen> createState() => _LockScreenState();
}

class _LockScreenState extends State<LockScreen>
    with SingleTickerProviderStateMixin {
  final TextEditingController _password = TextEditingController();
  final FocusNode _focus = FocusNode();

  late final AnimationController _shake = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 420),
  );

  VaultException? _error;
  bool _revealed = false;
  bool _capsLock = false;
  bool _working = false;

  @override
  void initState() {
    super.initState();
    _capsLock = HardwareKeyboard.instance.lockModesEnabled.contains(
      KeyboardLockMode.capsLock,
    );
  }

  @override
  void dispose() {
    _password.dispose();
    _focus.dispose();
    _shake.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_working || _password.text.isEmpty) return;
    final vault = context.read<VaultController>();
    final shell = context.read<ShellController>();

    setState(() {
      _working = true;
      _error = null;
    });

    final failure = await vault.unlock(_password.text);
    if (!mounted) return;

    if (failure == null) {
      _password.clear();
      setState(() => _working = false);
      if (shell.consumePendingPalette()) {
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (mounted) shell.openCommandPalette();
        });
      }
      return;
    }

    setState(() {
      _working = false;
      _error = failure;
      _password.clear();
    });
    if (context.motion.enabled) {
      _shake.forward(from: 0);
    }
    _focus.requestFocus();
  }

  void _syncCapsLock() {
    final capsLock = HardwareKeyboard.instance.lockModesEnabled.contains(
      KeyboardLockMode.capsLock,
    );
    if (capsLock != _capsLock) setState(() => _capsLock = capsLock);
  }

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final error = _error;

    return Stack(
      children: <Widget>[
        const Positioned.fill(child: AuthBackdrop()),
        Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(Insets.xxl),
            child: EntranceFade(
              child: AnimatedBuilder(
                animation: _shake,
                builder: (context, child) {
                  // Damped horizontal oscillation: three quick passes that
                  // fade out, signalling rejection without being jarring.
                  final offset =
                      math.sin(_shake.value * math.pi * 6) *
                      9 *
                      (1 - _shake.value);
                  return Transform.translate(
                    offset: Offset(offset, 0),
                    child: child,
                  );
                },
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 420),
                  child: Container(
                    padding: const EdgeInsets.all(Insets.xxxl),
                    decoration: BoxDecoration(
                      color: palette.overlay,
                      borderRadius: Corners.radiusLg,
                      border: Border.all(color: palette.border),
                      boxShadow: tokens.overlayShadow,
                    ),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: <Widget>[
                        Row(
                          children: <Widget>[
                            const BrandMark(size: 40, glow: true),
                            const SizedBox(width: Insets.md),
                            Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              mainAxisSize: MainAxisSize.min,
                              children: <Widget>[
                                const BrandWordmark(fontSize: 16),
                                const SizedBox(height: 2),
                                Text(
                                  'Local encrypted vault',
                                  style: tokens.text.caption,
                                ),
                              ],
                            ),
                          ],
                        ),
                        const SizedBox(height: Insets.xxl),
                        Text('Vault locked', style: tokens.text.pageTitle),
                        const SizedBox(height: Insets.xs),
                        Text(
                          'Enter your master password to decrypt this vault.',
                          style: tokens.text.secondary,
                        ),
                        const SizedBox(height: Insets.xl),
                        Focus(
                          onKeyEvent: (node, event) {
                            _syncCapsLock();
                            return KeyEventResult.ignored;
                          },
                          child: AppTextField(
                            controller: _password,
                            focusNode: _focus,
                            hint: 'Master password',
                            semanticLabel: 'Master password',
                            obscureText: !_revealed,
                            monospace: true,
                            autofocus: true,
                            enabled: !_working,
                            prefixIcon: Icons.key_rounded,
                            errorText: error?.title,
                            helper: error == null && _capsLock
                                ? 'Caps Lock is on'
                                : null,
                            onSubmitted: (_) => _submit(),
                            onChanged: (_) {
                              if (_error != null) setState(() => _error = null);
                            },
                            suffix: AppIconButton(
                              icon: _revealed
                                  ? Icons.visibility_off_outlined
                                  : Icons.visibility_outlined,
                              tooltip: _revealed
                                  ? 'Hide password'
                                  : 'Show password',
                              dense: true,
                              size: 16,
                              onPressed: () =>
                                  setState(() => _revealed = !_revealed),
                            ),
                          ),
                        ),
                        if (error != null) ...<Widget>[
                          const SizedBox(height: Insets.sm),
                          Text(
                            error.hint,
                            style: tokens.text.caption.copyWith(
                              color: palette.textSecondary,
                            ),
                          ),
                        ],
                        if (_capsLock && error == null) ...<Widget>[
                          const SizedBox(height: Insets.sm),
                          Row(
                            children: <Widget>[
                              Icon(
                                Icons.keyboard_capslock_rounded,
                                size: 14,
                                color: palette.warning,
                              ),
                              const SizedBox(width: Insets.xs),
                              Text(
                                'Caps Lock is on',
                                style: tokens.text.caption.copyWith(
                                  color: palette.warning,
                                ),
                              ),
                            ],
                          ),
                        ],
                        const SizedBox(height: Insets.xl),
                        AppButton(
                          label: 'Unlock vault',
                          icon: Icons.lock_open_rounded,
                          expand: true,
                          loading: _working,
                          onPressed: _submit,
                        ),
                        const SizedBox(height: Insets.lg),
                        Text(
                          'Everything stays on this machine. There is no '
                          'account, no sync and no password reset.',
                          textAlign: TextAlign.center,
                          style: tokens.text.caption,
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }
}
