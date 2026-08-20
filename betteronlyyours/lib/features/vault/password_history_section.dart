import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import '../../l10n/l10n.dart';
import '../../core/models/vault_entry.dart';
import '../../core/utils/formatting.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_dialog.dart';
import '../../shared/widgets/hover_builder.dart';
import '../../state/settings_controller.dart';
import '../../state/toast_controller.dart';
import '../../state/vault_controller.dart';
import 'vault_actions.dart';

/// Superseded passwords of an entry: recover from a bad rotation, or wipe the
/// trail. Values stay hidden until explicitly revealed, like every other
/// secret in the app.
class PasswordHistorySection extends StatelessWidget {
  const PasswordHistorySection({super.key, required this.entry});

  final VaultEntry entry;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final keepHistory = context
        .watch<SettingsController>()
        .settings
        .keepPasswordHistory;

    if (entry.passwordHistory.isEmpty) {
      if (!keepHistory) return const SizedBox.shrink();
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(context.l10n.historyTitle, style: tokens.text.label),
          const SizedBox(height: Insets.sm),
          Text(context.l10n.historyEmptyHint, style: tokens.text.caption),
        ],
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          children: <Widget>[
            Text(context.l10n.historyTitle, style: tokens.text.label),
            const SizedBox(width: Insets.sm),
            Text(
              context.l10n.historyCountOf(
                entry.passwordHistory.length,
                VaultEntry.maxPasswordHistory,
              ),
              style: tokens.text.caption,
            ),
            const Spacer(),
            AppButton(
              label: context.l10n.historyClear,
              icon: Icons.delete_sweep_outlined,
              variant: AppButtonVariant.ghost,
              size: AppButtonSize.small,
              onPressed: () => _clearHistory(context),
            ),
          ],
        ),
        const SizedBox(height: Insets.sm),
        for (final record in entry.passwordHistory)
          _HistoryRow(
            key: ValueKey<String>(
              '${record.replacedAt.millisecondsSinceEpoch}-'
              '${record.password.hashCode}',
            ),
            entry: entry,
            record: record,
          ),
        const SizedBox(height: Insets.sm),
        Text(
          context.l10n.historyCap(VaultEntry.maxPasswordHistory),
          style: tokens.text.caption,
        ),
      ],
    );
  }

  Future<void> _clearHistory(BuildContext context) async {
    final l10n = context.l10n;
    final vault = context.read<VaultController>();
    final toasts = context.read<ToastController>();

    final confirmed = await showConfirmDialog(
      context: context,
      title: l10n.historyClearTitle,
      message: l10n.historyClearMessage,
      detail: entry.title,
      confirmLabel: l10n.historyClear,
      destructive: true,
      icon: Icons.delete_sweep_outlined,
    );
    if (!confirmed) return;

    final cleared = await vault.clearPasswordHistory(entry.title);
    if (cleared) {
      toasts.success(l10n.historyCleared, detail: entry.title);
    }
  }
}

class _HistoryRow extends StatefulWidget {
  const _HistoryRow({super.key, required this.entry, required this.record});

  final VaultEntry entry;
  final VaultPasswordRecord record;

  @override
  State<_HistoryRow> createState() => _HistoryRowState();
}

class _HistoryRowState extends State<_HistoryRow> {
  bool _revealed = false;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final l10n = context.l10n;
    final record = widget.record;

    return HoverBuilder(
      canRequestFocus: false,
      builder: (context, state) => AnimatedContainer(
        duration: context.motion.fast,
        margin: const EdgeInsets.only(bottom: Insets.xs),
        padding: const EdgeInsets.symmetric(
          horizontal: Insets.md,
          vertical: Insets.sm,
        ),
        decoration: BoxDecoration(
          color: state.active ? palette.surfaceHigh : palette.surface,
          borderRadius: Corners.radiusSm,
          border: Border.all(color: palette.border),
        ),
        child: Row(
          children: <Widget>[
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Text(
                    _revealed
                        ? record.password
                        : '•' * record.password.length.clamp(8, 24),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: tokens.text.mono,
                  ),
                  const SizedBox(height: 2),
                  Text(
                    l10n.historyReplacedAt(
                      formatRelativeTime(l10n, record.replacedAt),
                      Formatting.absoluteTime(record.replacedAt),
                    ),
                    style: tokens.text.caption,
                  ),
                ],
              ),
            ),
            AppIconButton(
              icon: _revealed
                  ? Icons.visibility_off_outlined
                  : Icons.visibility_outlined,
              tooltip: _revealed ? l10n.historyHide : l10n.historyReveal,
              dense: true,
              size: 15,
              onPressed: () => setState(() => _revealed = !_revealed),
            ),
            AppIconButton(
              icon: Icons.copy_rounded,
              tooltip: l10n.historyCopy,
              dense: true,
              size: 15,
              onPressed: () => VaultActions.copyValue(
                context,
                value: record.password,
                label: l10n.historyPreviousPassword,
              ),
            ),
            AppIconButton(
              icon: Icons.restore_rounded,
              tooltip: l10n.historyRestore,
              dense: true,
              size: 15,
              onPressed: () => _restore(context),
            ),
            AppIconButton(
              icon: Icons.close_rounded,
              tooltip: l10n.historyForget,
              dense: true,
              size: 15,
              danger: true,
              onPressed: () => _forget(context),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _restore(BuildContext context) async {
    final l10n = context.l10n;
    final vault = context.read<VaultController>();
    final toasts = context.read<ToastController>();

    final confirmed = await showConfirmDialog(
      context: context,
      title: l10n.historyRestoreTitle,
      message: l10n.historyRestoreMessage,
      detail: widget.entry.title,
      confirmLabel: l10n.historyRestoreConfirm,
      icon: Icons.restore_rounded,
    );
    if (!confirmed) return;

    final restored = await vault.restorePassword(
      widget.entry.title,
      widget.record,
    );
    if (restored) {
      toasts.success(l10n.historyRestored, detail: widget.entry.title);
    }
  }

  Future<void> _forget(BuildContext context) async {
    final l10n = context.l10n;
    final vault = context.read<VaultController>();
    final toasts = context.read<ToastController>();

    final forgotten = await vault.forgetPassword(
      widget.entry.title,
      widget.record,
    );
    if (forgotten) {
      toasts.show(l10n.historyForgotten, detail: widget.entry.title);
    }
  }
}
