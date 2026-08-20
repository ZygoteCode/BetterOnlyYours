import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import '../../l10n/l10n.dart';
import '../../core/services/password_strength.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_dialog.dart';
import '../../shared/widgets/app_text_field.dart';
import '../../shared/widgets/strength_meter.dart';
import '../../state/toast_controller.dart';
import '../../state/vault_controller.dart';

/// Re-keys the vault: the whole file is re-encrypted with a key derived from
/// the new password. The old file is kept as `.bak` by the atomic write.
class ChangeMasterPasswordDialog extends StatefulWidget {
  const ChangeMasterPasswordDialog({super.key});

  @override
  State<ChangeMasterPasswordDialog> createState() =>
      _ChangeMasterPasswordDialogState();
}

class _ChangeMasterPasswordDialogState
    extends State<ChangeMasterPasswordDialog> {
  final TextEditingController _current = TextEditingController();
  final TextEditingController _next = TextEditingController();
  final TextEditingController _confirm = TextEditingController();

  bool _working = false;
  String? _error;

  @override
  void dispose() {
    _current.dispose();
    _next.dispose();
    _confirm.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_working) return;
    final l10n = context.l10n;
    final vault = context.read<VaultController>();
    final toasts = context.read<ToastController>();
    final navigator = Navigator.of(context);

    if (_next.text.length < 10) {
      setState(() => _error = l10n.changePasswordTooShort(10));
      return;
    }
    if (_next.text != _confirm.text) {
      setState(() => _error = context.l10n.changePasswordMismatch);
      return;
    }

    setState(() {
      _error = null;
      _working = true;
    });

    final failure = await vault.changeMasterPassword(_current.text, _next.text);
    if (!mounted) return;

    if (failure == null) {
      toasts.success(
        context.l10n.changePasswordDone,
        detail: context.l10n.changePasswordDoneDetail,
      );
      navigator.pop(true);
      return;
    }

    setState(() {
      _working = false;
      _error = failure.localizedTitle(l10n);
    });
  }

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final strength = PasswordStrength.evaluate(_next.text);

    return AppDialog(
      title: context.l10n.securityChangeMasterPassword,
      subtitle: context.l10n.changePasswordSubtitle,
      icon: Icons.password_rounded,
      width: 480,
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          AppTextField(
            controller: _current,
            label: context.l10n.changePasswordCurrent,
            obscureText: true,
            monospace: true,
            autofocus: true,
            enabled: !_working,
          ),
          const SizedBox(height: Insets.lg),
          AppTextField(
            controller: _next,
            label: context.l10n.changePasswordNew,
            obscureText: true,
            monospace: true,
            enabled: !_working,
            onChanged: (_) => setState(() => _error = null),
          ),
          if (_next.text.isNotEmpty) ...<Widget>[
            const SizedBox(height: Insets.md),
            StrengthMeter(strength: strength),
          ],
          const SizedBox(height: Insets.lg),
          AppTextField(
            controller: _confirm,
            label: context.l10n.changePasswordConfirm,
            obscureText: true,
            monospace: true,
            enabled: !_working,
            errorText: _error,
            onChanged: (_) => setState(() => _error = null),
            onSubmitted: (_) => _submit(),
          ),
          const SizedBox(height: Insets.lg),
          Text(
            context.l10n.changePasswordBackupNote,
            style: tokens.text.caption,
          ),
        ],
      ),
      actions: <Widget>[
        AppButton(
          label: context.l10n.commonCancel,
          variant: AppButtonVariant.ghost,
          onPressed: _working ? null : () => Navigator.of(context).pop(false),
        ),
        AppButton(
          label: context.l10n.changePasswordAction,
          icon: Icons.check_rounded,
          loading: _working,
          onPressed: _submit,
        ),
      ],
    );
  }
}
