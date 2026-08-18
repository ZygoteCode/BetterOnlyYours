import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import '../../core/services/clipboard_service.dart';
import '../../core/utils/formatting.dart';
import '../../shared/widgets/hover_builder.dart';
import '../../state/settings_controller.dart';
import '../../state/vault_controller.dart';

/// Thin footer with session, persistence and clipboard state.
class AppStatusBar extends StatelessWidget {
  const AppStatusBar({super.key});

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final vault = context.watch<VaultController>();
    final clipboard = context.watch<ClipboardService>();
    final settings = context.watch<SettingsController>().settings;

    return Container(
      height: 26,
      padding: const EdgeInsets.symmetric(horizontal: Insets.md),
      decoration: BoxDecoration(
        color: palette.backgroundElevated,
        border: Border(top: BorderSide(color: palette.border)),
      ),
      child: Row(
        children: <Widget>[
          _Dot(color: palette.success),
          const SizedBox(width: Insets.sm),
          Text(
            'Unlocked · ${Formatting.plural(vault.entryCount, 'entry', 'entries')}',
            style: tokens.text.caption,
          ),
          const SizedBox(width: Insets.lg),
          _SaveStatus(vault: vault),
          const Spacer(),
          if (clipboard.hasPendingClear) ...<Widget>[
            Icon(
              Icons.content_paste_rounded,
              size: 12,
              color: palette.secondary,
            ),
            const SizedBox(width: Insets.xs + 2),
            Text(
              '${clipboard.label ?? 'Secret'} clears in '
              '${clipboard.secondsRemaining}s',
              style: tokens.text.caption.copyWith(color: palette.secondary),
            ),
            const SizedBox(width: Insets.sm),
            HoverBuilder(
              onTap: clipboard.keepClipboard,
              builder: (context, state) => Text(
                'keep',
                style: tokens.text.caption.copyWith(
                  color: state.hovered
                      ? palette.textPrimary
                      : palette.textTertiary,
                ),
              ),
            ),
            const SizedBox(width: Insets.lg),
          ],
          Icon(
            settings.autoLockEnabled
                ? Icons.timer_outlined
                : Icons.timer_off_outlined,
            size: 12,
            color: palette.textTertiary,
          ),
          const SizedBox(width: Insets.xs + 2),
          Text(
            settings.autoLockEnabled
                ? 'Auto-lock ${settings.autoLockMinutes}m'
                : 'Auto-lock off',
            style: tokens.text.caption,
          ),
        ],
      ),
    );
  }
}

class _SaveStatus extends StatelessWidget {
  const _SaveStatus({required this.vault});

  final VaultController vault;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;

    switch (vault.saveState) {
      case SaveState.saving:
        return Row(
          children: <Widget>[
            SizedBox(
              width: 10,
              height: 10,
              child: CircularProgressIndicator(
                strokeWidth: 1.6,
                color: palette.accent,
              ),
            ),
            const SizedBox(width: Insets.sm),
            Text('Encrypting…', style: tokens.text.caption),
          ],
        );
      case SaveState.failed:
        return HoverBuilder(
          onTap: vault.retrySave,
          builder: (context, state) => Row(
            children: <Widget>[
              Icon(
                Icons.error_outline_rounded,
                size: 12,
                color: palette.danger,
              ),
              const SizedBox(width: Insets.xs + 2),
              Text(
                'Save failed — click to retry',
                style: tokens.text.caption.copyWith(
                  color: palette.danger,
                  decoration: state.hovered
                      ? TextDecoration.underline
                      : TextDecoration.none,
                  decorationColor: palette.danger,
                ),
              ),
            ],
          ),
        );
      case SaveState.saved:
      case SaveState.idle:
        return Text(
          vault.lastSavedAt == null
              ? 'No changes yet'
              : 'Saved ${Formatting.relativeTime(vault.lastSavedAt)}',
          style: tokens.text.caption,
        );
    }
  }
}

class _Dot extends StatelessWidget {
  const _Dot({required this.color});

  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 6,
      height: 6,
      decoration: BoxDecoration(
        color: color,
        shape: BoxShape.circle,
        boxShadow: <BoxShadow>[
          BoxShadow(color: color.withValues(alpha: 0.5), blurRadius: 6),
        ],
      ),
    );
  }
}
