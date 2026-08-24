import 'dart:async';

import 'package:material_ui/material_ui.dart';
import 'package:flutter/scheduler.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import '../../core/models/vault_entry.dart';
import '../../core/security/totp.dart';
import '../../l10n/l10n.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_dialog.dart';
import '../../shared/widgets/totp_ring.dart';
import '../../state/toast_controller.dart';
import '../../state/vault_controller.dart';
import 'totp_setup_dialog.dart';
import 'vault_actions.dart';

/// The two-factor block of an entry: the live code, its countdown, and the
/// only two things the user can do with the secret — replace it or remove it.
///
/// The secret itself is never rendered, copied or exported: it is unsealed
/// inside the controller for the microseconds it takes to compute the code.
class TotpSection extends StatefulWidget {
  const TotpSection({super.key, required this.entry});

  final VaultEntry entry;

  @override
  State<TotpSection> createState() => _TotpSectionState();
}

class _TotpSectionState extends State<TotpSection>
    with SingleTickerProviderStateMixin {
  final ValueNotifier<double> _progress = ValueNotifier<double>(1);
  final ValueNotifier<int> _seconds = ValueNotifier<int>(0);

  Ticker? _ticker;
  Timer? _timer;
  bool _smooth = true;

  TotpCode? _code;
  TotpConfig? _config;
  bool _unreadable = false;

  @override
  void initState() {
    super.initState();
    _refresh(force: true);
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // The ring follows the frame clock normally and a plain one-second timer
    // when the user asked for reduced motion.
    final smooth = context.motion.enabled;
    if (_ticker == null && _timer == null || smooth != _smooth) {
      _smooth = smooth;
      _startClock();
    }
  }

  @override
  void didUpdateWidget(TotpSection oldWidget) {
    super.didUpdateWidget(oldWidget);
    // A new secret must produce a new code on the spot, not at the next step.
    if (oldWidget.entry.totp != widget.entry.totp ||
        oldWidget.entry.title != widget.entry.title) {
      _refresh(force: true);
    }
  }

  @override
  void dispose() {
    _stopClock();
    _progress.dispose();
    _seconds.dispose();
    super.dispose();
  }

  void _startClock() {
    _stopClock();
    if (!widget.entry.hasTotp) return;
    if (_smooth) {
      _ticker = createTicker((_) => _refresh())..start();
    } else {
      _timer = Timer.periodic(const Duration(seconds: 1), (_) => _refresh());
    }
  }

  void _stopClock() {
    _ticker?.dispose();
    _ticker = null;
    _timer?.cancel();
    _timer = null;
  }

  /// Recomputes the countdown every tick and the code only when its time step
  /// rolls over, so the cipher work happens once per period, not per frame.
  void _refresh({bool force = false}) {
    if (!mounted) return;
    final entry = widget.entry;
    if (!entry.hasTotp) {
      if (_code != null || _config != null || _unreadable) {
        setState(() {
          _code = null;
          _config = null;
          _unreadable = false;
        });
      }
      return;
    }

    final vault = context.read<VaultController>();
    final now = DateTime.now();
    final period = _config?.period ?? _code?.period ?? TotpConfig.defaultPeriod;
    final counter = Totp.counterAt(now, period);

    if (force || _code == null || counter != _code!.counter) {
      final code = vault.totpCode(entry, at: now);
      final config = code == null ? null : vault.totpConfig(entry);
      setState(() {
        _code = code;
        _config = config;
        _unreadable = code == null;
      });
      if (code == null) {
        _progress.value = 0;
        _seconds.value = 0;
        return;
      }
    }

    final active = _code;
    if (active == null) return;
    final remaining = Totp.millisecondsRemainingAt(now, active.period);
    _progress.value = (remaining / (active.period * 1000)).clamp(0.0, 1.0);
    _seconds.value = (remaining / 1000).ceil();
  }

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final l10n = context.l10n;
    final entry = widget.entry;

    if (!entry.hasTotp) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(l10n.totpTitle, style: tokens.text.label),
          const SizedBox(height: Insets.sm),
          Text(l10n.totpEmptyHint, style: tokens.text.caption),
          const SizedBox(height: Insets.md),
          AppButton(
            label: l10n.totpAdd,
            icon: Icons.shield_outlined,
            variant: AppButtonVariant.secondary,
            size: AppButtonSize.small,
            onPressed: () => _setup(context),
          ),
        ],
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          children: <Widget>[
            Text(l10n.totpTitle, style: tokens.text.label),
            const SizedBox(width: Insets.sm),
            if (_config != null)
              Text(
                l10n.totpDetails(
                  _config!.algorithm.label,
                  _config!.effectiveDigits,
                  _config!.period,
                ),
                style: tokens.text.caption,
              ),
            const Spacer(),
            AppButton(
              label: l10n.totpReplace,
              icon: Icons.autorenew_rounded,
              variant: AppButtonVariant.ghost,
              size: AppButtonSize.small,
              onPressed: () => _setup(context),
            ),
            const SizedBox(width: Insets.xs),
            AppButton(
              label: l10n.totpRemove,
              icon: Icons.shield_outlined,
              variant: AppButtonVariant.ghost,
              size: AppButtonSize.small,
              onPressed: () => _remove(context),
            ),
          ],
        ),
        const SizedBox(height: Insets.sm),
        if (_unreadable)
          _UnreadableToken(entry: entry)
        else
          _CodeCard(
            code: _code,
            progress: _progress,
            seconds: _seconds,
            onCopy: () => _copy(context),
          ),
        const SizedBox(height: Insets.sm),
        Text(l10n.totpSecretHidden, style: tokens.text.caption),
      ],
    );
  }

  Future<void> _setup(BuildContext context) async {
    final saved = await showAppDialog<bool>(
      context: context,
      builder: (_) => TotpSetupDialog(entry: widget.entry),
    );
    if (saved == true && mounted) _refresh(force: true);
  }

  Future<void> _remove(BuildContext context) async {
    final l10n = context.l10n;
    final vault = context.read<VaultController>();
    final toasts = context.read<ToastController>();

    final confirmed = await showConfirmDialog(
      context: context,
      title: l10n.totpRemoveTitle,
      message: l10n.totpRemoveMessage,
      detail: widget.entry.title,
      confirmLabel: l10n.totpRemove,
      destructive: true,
      icon: Icons.shield_outlined,
    );
    if (!confirmed) return;

    final removed = await vault.removeTotp(widget.entry.title);
    if (removed) toasts.success(l10n.totpRemoved, detail: widget.entry.title);
  }

  Future<void> _copy(BuildContext context) {
    final code = _code;
    if (code == null) return Future<void>.value();
    return VaultActions.copyValue(
      context,
      value: code.value,
      label: context.l10n.totpLabel,
    );
  }
}

class _CodeCard extends StatelessWidget {
  const _CodeCard({
    required this.code,
    required this.progress,
    required this.seconds,
    required this.onCopy,
  });

  final TotpCode? code;
  final ValueNotifier<double> progress;
  final ValueNotifier<int> seconds;
  final VoidCallback onCopy;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final l10n = context.l10n;
    final value = code;

    return Semantics(
      button: true,
      label: l10n.totpCopy,
      child: InkWell(
        onTap: value == null ? null : onCopy,
        borderRadius: Corners.radiusMd,
        child: Container(
          padding: const EdgeInsets.symmetric(
            horizontal: Insets.lg,
            vertical: Insets.md,
          ),
          decoration: BoxDecoration(
            color: tokens.color.surface,
            borderRadius: Corners.radiusMd,
            border: Border.all(color: tokens.color.border),
          ),
          child: Row(
            children: <Widget>[
              ValueListenableBuilder<double>(
                valueListenable: progress,
                builder: (context, value, _) => ValueListenableBuilder<int>(
                  valueListenable: seconds,
                  builder: (context, remaining, _) =>
                      TotpRing(progress: value, secondsRemaining: remaining),
                ),
              ),
              const SizedBox(width: Insets.lg),
              Expanded(
                child: ValueListenableBuilder<double>(
                  valueListenable: progress,
                  builder: (context, ratio, _) => Text(
                    value?.grouped ?? '——— ———',
                    style: tokens.text.monoLarge.copyWith(
                      fontSize: 26,
                      letterSpacing: 3,
                      fontWeight: FontWeight.w600,
                      color: TotpRing.colorFor(tokens.color, ratio),
                    ),
                  ),
                ),
              ),
              AppButton(
                label: l10n.totpCopy,
                icon: Icons.copy_rounded,
                variant: AppButtonVariant.secondary,
                size: AppButtonSize.small,
                onPressed: value == null ? null : onCopy,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _UnreadableToken extends StatelessWidget {
  const _UnreadableToken({required this.entry});

  final VaultEntry entry;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final l10n = context.l10n;

    return Container(
      padding: const EdgeInsets.all(Insets.md),
      decoration: BoxDecoration(
        color: tokens.color.warning.withValues(alpha: 0.08),
        borderRadius: Corners.radiusMd,
        border: Border.all(color: tokens.color.warning.withValues(alpha: 0.4)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(
            Icons.report_gmailerrorred_rounded,
            size: 18,
            color: tokens.color.warning,
          ),
          const SizedBox(width: Insets.sm),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(l10n.totpUnreadable, style: tokens.text.bodyStrong),
                const SizedBox(height: Insets.xxs),
                Text(l10n.totpUnreadableDetail, style: tokens.text.caption),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
