import 'dart:async';
import 'dart:typed_data';

import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import '../../core/models/vault_entry.dart';
import '../../core/security/totp.dart';
import '../../core/security/vault_crypto.dart';
import '../../l10n/l10n.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_dialog.dart';
import '../../shared/widgets/app_text_field.dart';
import '../../shared/widgets/tag_chip.dart';
import '../../shared/widgets/totp_ring.dart';
import '../../state/toast_controller.dart';
import '../../state/vault_controller.dart';

/// Where a two-factor secret enters the vault — the only place it is ever
/// accepted, and the last time it is visible.
///
/// Accepts a bare base32 setup key or a full `otpauth://` link, and previews
/// the code it produces so a typo is caught before the secret disappears
/// behind the seal.
class TotpSetupDialog extends StatefulWidget {
  const TotpSetupDialog({super.key, required this.entry});

  final VaultEntry entry;

  @override
  State<TotpSetupDialog> createState() => _TotpSetupDialogState();
}

class _TotpSetupDialogState extends State<TotpSetupDialog> {
  final TextEditingController _input = TextEditingController();

  Timer? _ticker;
  Uint8List? _secret;
  TotpConfig _config = const TotpConfig();
  TotpCode? _preview;
  String? _error;
  bool _reveal = false;
  bool _advanced = false;
  bool _fromUri = false;
  bool _saving = false;

  bool get _isReplacement => widget.entry.hasTotp;
  bool get _canSave => _secret != null && !_saving;

  @override
  void initState() {
    super.initState();
    _ticker = Timer.periodic(
      const Duration(milliseconds: 250),
      (_) => _updatePreview(),
    );
  }

  @override
  void dispose() {
    _ticker?.cancel();
    // The typed secret leaves no copy behind when the dialog closes.
    VaultCrypto.wipe(_secret);
    _input.clear();
    _input.dispose();
    super.dispose();
  }

  void _onInputChanged(String raw) {
    VaultCrypto.wipe(_secret);
    _secret = null;
    _preview = null;
    _fromUri = false;

    final text = raw.trim();
    if (text.isEmpty) {
      setState(() => _error = null);
      return;
    }

    try {
      final parsed = OtpAuthUri.parseSecretOrUri(text, defaults: _config);
      _secret = parsed.secret;
      _fromUri = OtpAuthUri.looksLikeUri(text);
      if (_fromUri) _config = parsed.config;
      setState(() => _error = null);
      _updatePreview();
    } on FormatException catch (error) {
      setState(() => _error = error.message);
    }
  }

  void _updatePreview() {
    final secret = _secret;
    if (secret == null || !mounted) return;
    setState(() {
      _preview = Totp.generate(secret: secret, config: _config);
    });
  }

  void _applyConfig(TotpConfig config) {
    setState(() => _config = config.normalized());
    _updatePreview();
  }

  Future<void> _save() async {
    final secret = _secret;
    if (secret == null) return;

    final l10n = context.l10n;
    final navigator = Navigator.of(context);
    final vault = context.read<VaultController>();
    final toasts = context.read<ToastController>();

    setState(() => _saving = true);
    final stored = await vault.setTotp(
      widget.entry.title,
      secret: secret,
      config: _config,
    );
    if (!mounted) return;

    if (!stored) {
      setState(() {
        _saving = false;
        _error = l10n.totpSaveFailed;
      });
      return;
    }

    toasts.success(
      _isReplacement ? l10n.totpReplaced : l10n.totpSaved,
      detail: widget.entry.title,
    );
    navigator.pop(true);
  }

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final l10n = context.l10n;

    return AppDialog(
      title: _isReplacement ? l10n.totpSetupReplaceTitle : l10n.totpSetupTitle,
      subtitle: widget.entry.title,
      icon: Icons.shield_outlined,
      width: 520,
      scrollable: true,
      content: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          AppTextField(
            controller: _input,
            label: l10n.totpSecretLabel,
            hint: 'JBSWY3DPEHPK3PXP',
            helper: l10n.totpSecretHelper,
            errorText: _error,
            monospace: true,
            autofocus: true,
            obscureText: !_reveal,
            onChanged: _onInputChanged,
            suffix: AppIconButton(
              icon: _reveal
                  ? Icons.visibility_off_rounded
                  : Icons.visibility_rounded,
              tooltip: _reveal ? l10n.authHidePassword : l10n.authShowPassword,
              dense: true,
              size: 15,
              onPressed: () => setState(() => _reveal = !_reveal),
            ),
          ),
          if (_fromUri) ...<Widget>[
            const SizedBox(height: Insets.sm),
            Row(
              children: <Widget>[
                Icon(
                  Icons.check_circle_outline_rounded,
                  size: 15,
                  color: tokens.color.success,
                ),
                const SizedBox(width: Insets.xs),
                Expanded(
                  child: Text(
                    _config.issuer.isEmpty && _config.account.isEmpty
                        ? l10n.totpUriDetected
                        : l10n.totpUriDetectedFor(
                            <String>[
                              _config.issuer,
                              _config.account,
                            ].where((part) => part.isNotEmpty).join(' · '),
                          ),
                    style: tokens.text.caption,
                  ),
                ),
              ],
            ),
          ],
          const SizedBox(height: Insets.lg),
          _PreviewCard(code: _preview, config: _config),
          const SizedBox(height: Insets.md),
          AppButton(
            label: _advanced ? l10n.totpHideAdvanced : l10n.totpAdvanced,
            icon: _advanced ? Icons.expand_less_rounded : Icons.tune_rounded,
            variant: AppButtonVariant.ghost,
            size: AppButtonSize.small,
            onPressed: () => setState(() => _advanced = !_advanced),
          ),
          if (_advanced) ...<Widget>[
            const SizedBox(height: Insets.md),
            _ConfigRow(
              label: l10n.totpAlgorithm,
              children: <Widget>[
                for (final algorithm in TotpAlgorithm.values)
                  TagChip(
                    label: algorithm.label,
                    selected: _config.algorithm == algorithm,
                    onTap: () =>
                        _applyConfig(_config.copyWith(algorithm: algorithm)),
                  ),
              ],
            ),
            const SizedBox(height: Insets.md),
            _ConfigRow(
              label: l10n.totpDigits,
              children: <Widget>[
                for (final digits in TotpConfig.commonDigits)
                  TagChip(
                    label: '$digits',
                    selected:
                        _config.kind == TotpKind.standard &&
                        _config.digits == digits,
                    onTap: () => _applyConfig(_config.copyWith(digits: digits)),
                  ),
              ],
            ),
            const SizedBox(height: Insets.md),
            _ConfigRow(
              label: l10n.totpPeriod,
              children: <Widget>[
                for (final period in TotpConfig.commonPeriods)
                  TagChip(
                    label: l10n.totpSecondsShort(period),
                    selected: _config.period == period,
                    onTap: () => _applyConfig(_config.copyWith(period: period)),
                  ),
              ],
            ),
            const SizedBox(height: Insets.md),
            _ConfigRow(
              label: l10n.totpKind,
              children: <Widget>[
                TagChip(
                  label: l10n.totpKindStandard,
                  selected: _config.kind == TotpKind.standard,
                  onTap: () =>
                      _applyConfig(_config.copyWith(kind: TotpKind.standard)),
                ),
                TagChip(
                  label: l10n.totpKindSteam,
                  selected: _config.kind == TotpKind.steam,
                  onTap: () => _applyConfig(
                    _config.copyWith(
                      kind: TotpKind.steam,
                      algorithm: TotpAlgorithm.sha1,
                      period: 30,
                    ),
                  ),
                ),
              ],
            ),
          ],
          const SizedBox(height: Insets.lg),
          Container(
            padding: const EdgeInsets.all(Insets.md),
            decoration: BoxDecoration(
              color: tokens.color.surface,
              borderRadius: Corners.radiusMd,
              border: Border.all(color: tokens.color.border),
            ),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Icon(
                  Icons.lock_outline_rounded,
                  size: 16,
                  color: tokens.color.textTertiary,
                ),
                const SizedBox(width: Insets.sm),
                Expanded(
                  child: Text(
                    l10n.totpWriteOnlyNotice,
                    style: tokens.text.caption,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
      actions: <Widget>[
        AppButton(
          label: l10n.commonCancel,
          variant: AppButtonVariant.ghost,
          onPressed: () => Navigator.of(context).pop(false),
        ),
        AppButton(
          label: _isReplacement ? l10n.totpReplace : l10n.totpSave,
          icon: Icons.shield_outlined,
          loading: _saving,
          onPressed: _canSave ? _save : null,
        ),
      ],
    );
  }
}

class _PreviewCard extends StatelessWidget {
  const _PreviewCard({required this.code, required this.config});

  final TotpCode? code;
  final TotpConfig config;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final l10n = context.l10n;
    final progress = code?.progress ?? 0;

    return Container(
      padding: const EdgeInsets.all(Insets.md),
      decoration: BoxDecoration(
        color: tokens.color.surface,
        borderRadius: Corners.radiusMd,
        border: Border.all(color: tokens.color.border),
      ),
      child: Row(
        children: <Widget>[
          TotpRing(
            progress: code == null ? 0 : progress,
            secondsRemaining: code?.secondsRemaining ?? 0,
            size: 42,
          ),
          const SizedBox(width: Insets.lg),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(l10n.totpPreview, style: tokens.text.caption),
                const SizedBox(height: Insets.xxs),
                Text(
                  code?.grouped ?? '——— ———',
                  style: tokens.text.monoLarge.copyWith(
                    fontSize: 22,
                    letterSpacing: 2.5,
                    fontWeight: FontWeight.w600,
                    color: code == null
                        ? tokens.color.textTertiary
                        : TotpRing.colorFor(tokens.color, progress),
                  ),
                ),
              ],
            ),
          ),
          Text(
            l10n.totpDetails(
              config.algorithm.label,
              config.effectiveDigits,
              config.period,
            ),
            style: tokens.text.caption,
          ),
        ],
      ),
    );
  }
}

class _ConfigRow extends StatelessWidget {
  const _ConfigRow({required this.label, required this.children});

  final String label;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        SizedBox(
          width: 96,
          child: Padding(
            padding: const EdgeInsets.only(top: Insets.xs),
            child: Text(label, style: context.tokens.text.caption),
          ),
        ),
        Expanded(
          child: Wrap(
            spacing: Insets.xs + 2,
            runSpacing: Insets.xs + 2,
            children: children,
          ),
        ),
      ],
    );
  }
}
