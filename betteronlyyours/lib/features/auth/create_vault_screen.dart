import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import '../../l10n/l10n.dart';
import '../../core/security/vault_exception.dart';
import '../../core/services/password_strength.dart';
import '../../shared/animations/entrance.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_text_field.dart';
import '../../shared/widgets/brand_mark.dart';
import '../../shared/widgets/strength_meter.dart';
import '../../state/vault_controller.dart';
import 'auth_backdrop.dart';

/// First-run experience: explain what the vault is, then create it.
class CreateVaultScreen extends StatefulWidget {
  const CreateVaultScreen({super.key});

  @override
  State<CreateVaultScreen> createState() => _CreateVaultScreenState();
}

class _CreateVaultScreenState extends State<CreateVaultScreen> {
  final TextEditingController _password = TextEditingController();
  final TextEditingController _confirm = TextEditingController();

  bool _revealed = false;
  bool _working = false;
  bool _acknowledged = false;
  String? _error;
  VaultException? _failure;

  static const int _minimumLength = 10;

  @override
  void dispose() {
    _password.dispose();
    _confirm.dispose();
    super.dispose();
  }

  bool get _lengthOk => _password.text.length >= _minimumLength;
  bool get _matches =>
      _password.text.isNotEmpty && _password.text == _confirm.text;

  Future<void> _create() async {
    if (_working) return;
    final vault = context.read<VaultController>();

    final l10n = context.l10n;
    if (!_lengthOk) {
      setState(() => _error = l10n.setupErrorFields(_minimumLength));
      return;
    }
    if (!_matches) {
      setState(() => _error = l10n.setupErrorMismatch);
      return;
    }
    if (!_acknowledged) {
      setState(() => _error = l10n.setupErrorAcknowledge);
      return;
    }

    setState(() {
      _error = null;
      _failure = null;
      _working = true;
    });

    final failure = await vault.createVault(_password.text);
    if (!mounted) return;

    if (failure != null) {
      setState(() {
        _working = false;
        _failure = failure;
      });
      return;
    }

    // The shell replaces this screen, but do not leave a spinner running in
    // case that transition is delayed.
    setState(() {
      _working = false;
      _password.clear();
      _confirm.clear();
    });
  }

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final vault = context.watch<VaultController>();
    final l10n = context.l10n;
    final strength = PasswordStrength.evaluate(_password.text);
    final path = vault.fileInfo?.path;

    return Stack(
      children: <Widget>[
        const Positioned.fill(child: AuthBackdrop()),
        Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(Insets.xxl),
            child: EntranceFade(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 560),
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
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              mainAxisSize: MainAxisSize.min,
                              children: <Widget>[
                                const BrandWordmark(fontSize: 16),
                                const SizedBox(height: 2),
                                Text(
                                  l10n.setupTitle,
                                  style: tokens.text.caption,
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: Insets.xxl),
                      Text(l10n.setupHeadline, style: tokens.text.pageTitle),
                      const SizedBox(height: Insets.sm),
                      Text(l10n.setupIntro, style: tokens.text.secondary),
                      const SizedBox(height: Insets.xl),
                      AppTextField(
                        controller: _password,
                        label: l10n.authMasterPassword,
                        hint: l10n.setupPasswordHint,
                        obscureText: !_revealed,
                        monospace: true,
                        autofocus: true,
                        enabled: !_working,
                        prefixIcon: Icons.key_rounded,
                        onChanged: (_) => setState(() => _error = null),
                        suffix: AppIconButton(
                          icon: _revealed
                              ? Icons.visibility_off_outlined
                              : Icons.visibility_outlined,
                          tooltip: _revealed ? l10n.setupHide : l10n.setupShow,
                          dense: true,
                          size: 16,
                          onPressed: () =>
                              setState(() => _revealed = !_revealed),
                        ),
                      ),
                      if (_password.text.isNotEmpty) ...<Widget>[
                        const SizedBox(height: Insets.md),
                        StrengthMeter(strength: strength),
                      ],
                      const SizedBox(height: Insets.lg),
                      AppTextField(
                        controller: _confirm,
                        label: l10n.setupConfirmLabel,
                        hint: l10n.setupConfirmHint,
                        obscureText: !_revealed,
                        monospace: true,
                        enabled: !_working,
                        prefixIcon: Icons.check_rounded,
                        errorText: _error,
                        onChanged: (_) => setState(() => _error = null),
                        onSubmitted: (_) => _create(),
                      ),
                      const SizedBox(height: Insets.lg),
                      _Requirement(
                        met: _lengthOk,
                        label: l10n.setupRequirementLength(_minimumLength),
                      ),
                      _Requirement(
                        met: _matches,
                        label: l10n.setupRequirementMatch,
                      ),
                      const SizedBox(height: Insets.lg),
                      _Acknowledgement(
                        value: _acknowledged,
                        onChanged: (value) => setState(() {
                          _acknowledged = value;
                          _error = null;
                        }),
                      ),
                      if (_failure != null) ...<Widget>[
                        const SizedBox(height: Insets.lg),
                        Container(
                          padding: const EdgeInsets.all(Insets.md),
                          decoration: BoxDecoration(
                            color: palette.danger.withValues(alpha: 0.1),
                            borderRadius: Corners.radiusSm,
                            border: Border.all(
                              color: palette.danger.withValues(alpha: 0.4),
                            ),
                          ),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: <Widget>[
                              Text(
                                _failure!.localizedTitle(l10n),
                                style: tokens.text.bodyStrong.copyWith(
                                  color: palette.danger,
                                ),
                              ),
                              const SizedBox(height: 2),
                              Text(
                                _failure!.localizedHint(l10n),
                                style: tokens.text.caption,
                              ),
                            ],
                          ),
                        ),
                      ],
                      const SizedBox(height: Insets.xl),
                      AppButton(
                        label: l10n.setupCreateButton,
                        icon: Icons.shield_rounded,
                        expand: true,
                        loading: _working,
                        onPressed: _create,
                      ),
                      if (path != null) ...<Widget>[
                        const SizedBox(height: Insets.lg),
                        Text(
                          l10n.setupVaultFile(path),
                          textAlign: TextAlign.center,
                          style: tokens.text.caption,
                        ),
                      ],
                    ],
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

class _Requirement extends StatelessWidget {
  const _Requirement({required this.met, required this.label});

  final bool met;
  final String label;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    return Padding(
      padding: const EdgeInsets.only(bottom: Insets.xs),
      child: Row(
        children: <Widget>[
          AnimatedContainer(
            duration: context.motion.fast,
            width: 16,
            height: 16,
            decoration: BoxDecoration(
              color: met
                  ? palette.success.withValues(alpha: 0.18)
                  : palette.surface,
              borderRadius: Corners.radiusXs,
              border: Border.all(
                color: met
                    ? palette.success.withValues(alpha: 0.6)
                    : palette.border,
              ),
            ),
            child: Icon(
              met ? Icons.check_rounded : Icons.remove_rounded,
              size: 11,
              color: met ? palette.success : palette.textTertiary,
            ),
          ),
          const SizedBox(width: Insets.sm),
          Text(
            label,
            style: tokens.text.caption.copyWith(
              color: met ? palette.textSecondary : palette.textTertiary,
            ),
          ),
        ],
      ),
    );
  }
}

class _Acknowledgement extends StatelessWidget {
  const _Acknowledgement({required this.value, required this.onChanged});

  final bool value;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;

    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTap: () => onChanged(!value),
      child: Container(
        padding: const EdgeInsets.all(Insets.md),
        decoration: BoxDecoration(
          color: palette.surface,
          borderRadius: Corners.radiusSm,
          border: Border.all(
            color: value
                ? palette.accent.withValues(alpha: 0.5)
                : palette.border,
          ),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Checkbox(
              value: value,
              onChanged: (next) => onChanged(next ?? false),
              visualDensity: VisualDensity.compact,
              materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
            ),
            const SizedBox(width: Insets.sm),
            Expanded(
              child: Text(
                context.l10n.setupAcknowledgement,
                style: tokens.text.secondary,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
