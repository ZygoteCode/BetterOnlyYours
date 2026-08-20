import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import '../../l10n/l10n.dart';
import '../../core/models/vault_entry.dart';
import '../../core/services/password_generator.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_dialog.dart';
import '../../shared/widgets/app_text_field.dart';
import '../../state/settings_controller.dart';
import '../../state/toast_controller.dart';
import '../../state/vault_controller.dart';
import 'secret_field.dart';

/// Quick-create sheet. Title first, everything else optional, Enter submits.
class EntryCreateDialog extends StatefulWidget {
  const EntryCreateDialog({super.key, this.initialTitle});

  final String? initialTitle;

  @override
  State<EntryCreateDialog> createState() => _EntryCreateDialogState();
}

class _EntryCreateDialogState extends State<EntryCreateDialog> {
  late final TextEditingController _title = TextEditingController(
    text: widget.initialTitle ?? '',
  );
  final TextEditingController _username = TextEditingController();
  final TextEditingController _password = TextEditingController();
  final TextEditingController _url = TextEditingController();

  String? _error;
  bool _saving = false;

  @override
  void dispose() {
    _title.dispose();
    _username.dispose();
    _password.dispose();
    _url.dispose();
    super.dispose();
  }

  void _generate() {
    final options = context.read<SettingsController>().settings.generator;
    final generated = PasswordGenerator().generate(options);
    setState(() => _password.text = generated.value);
  }

  Future<void> _submit() async {
    final vault = context.read<VaultController>();
    final toasts = context.read<ToastController>();
    final navigator = Navigator.of(context);

    final title = _title.text.trim();
    if (title.isEmpty) {
      setState(() => _error = context.l10n.createDialogNameEmpty);
      return;
    }
    if (vault.hasEntry(title)) {
      setState(() => _error = context.l10n.createDialogNameExists(title));
      return;
    }

    setState(() {
      _error = null;
      _saving = true;
    });

    final entry = VaultEntry.create(title).copyWith(
      username: _username.text.trim(),
      password: _password.text,
      url: _url.text.trim(),
    );

    final ok = await vault.upsertEntry(entry);
    if (!mounted) return;

    if (ok) {
      vault.select(title);
      toasts.success(context.l10n.createDialogEntryCreated, detail: title);
      navigator.pop(title);
    } else {
      setState(() {
        _saving = false;
        _error = context.l10n.createDialogSaveFailed;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;

    return AppDialog(
      title: context.l10n.createDialogTitle,
      subtitle: context.l10n.createDialogSubtitle,
      icon: Icons.add_rounded,
      width: 520,
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          AppTextField(
            controller: _title,
            label: context.l10n.fieldName,
            hint: context.l10n.createDialogNameHint,
            autofocus: true,
            errorText: _error,
            textInputAction: TextInputAction.next,
            onChanged: (_) {
              if (_error != null) setState(() => _error = null);
            },
            onSubmitted: (_) => _submit(),
          ),
          const SizedBox(height: Insets.lg),
          AppTextField(
            controller: _username,
            label: context.l10n.fieldUsername,
            hint: context.l10n.createDialogOptional,
            prefixIcon: Icons.person_outline_rounded,
            textInputAction: TextInputAction.next,
            onSubmitted: (_) => _submit(),
          ),
          const SizedBox(height: Insets.lg),
          SecretField(
            controller: _password,
            label: context.l10n.fieldPassword,
            hint: context.l10n.createDialogPasswordHint,
            onGenerate: _generate,
            onSubmitted: (_) => _submit(),
          ),
          const SizedBox(height: Insets.lg),
          AppTextField(
            controller: _url,
            label: context.l10n.fieldWebsite,
            hint: context.l10n.createDialogOptional,
            prefixIcon: Icons.link_rounded,
            onSubmitted: (_) => _submit(),
          ),
          const SizedBox(height: Insets.md),
          Text(context.l10n.createDialogFooter, style: tokens.text.caption),
        ],
      ),
      actions: <Widget>[
        AppButton(
          label: context.l10n.commonCancel,
          variant: AppButtonVariant.ghost,
          onPressed: _saving ? null : () => Navigator.of(context).pop(),
        ),
        AppButton(
          label: context.l10n.createDialogCreate,
          icon: Icons.check_rounded,
          loading: _saving,
          onPressed: _submit,
        ),
      ],
    );
  }
}
