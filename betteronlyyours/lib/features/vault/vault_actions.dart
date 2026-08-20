import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../core/models/vault_entry.dart';
import '../../l10n/l10n.dart';
import '../../core/services/clipboard_service.dart';
import '../../shared/widgets/app_dialog.dart';
import '../../shared/widgets/app_menu.dart';
import '../../state/settings_controller.dart';
import '../../state/shell_controller.dart';
import '../../state/toast_controller.dart';
import '../../state/vault_controller.dart';
import 'entry_create_dialog.dart';

/// Entry operations shared by the list, the detail pane, the dashboard and the
/// command palette, so behaviour (and feedback) is identical everywhere.
class VaultActions {
  const VaultActions._();

  static Future<void> copyValue(
    BuildContext context, {
    required String value,
    required String label,
  }) async {
    if (value.isEmpty) return;
    final l10n = context.l10n;
    final clipboard = context.read<ClipboardService>();
    final toasts = context.read<ToastController>();
    final settings = context.read<SettingsController>().settings;

    final ok = await clipboard.copy(
      value,
      label: label,
      clearAfterSeconds: settings.clipboardClearSeconds,
    );

    if (!ok) {
      toasts.error(
        l10n.actionClipboardUnavailable,
        detail: l10n.actionClipboardUnavailableDetail,
      );
      return;
    }

    toasts.success(
      l10n.actionCopied(label),
      detail: settings.clipboardClearEnabled
          ? l10n.actionCopiedDetail(settings.clipboardClearSeconds)
          : null,
    );
  }

  static Future<void> copyUsername(BuildContext context, VaultEntry entry) {
    final l10n = context.l10n;
    if (entry.username.isEmpty) {
      context.read<ToastController>().warning(
        l10n.actionNoUsername,
        detail: l10n.actionNoUsernameDetail,
      );
      return Future<void>.value();
    }
    return copyValue(context, value: entry.username, label: l10n.labelUsername);
  }

  static Future<void> copyPassword(BuildContext context, VaultEntry entry) {
    final l10n = context.l10n;
    if (entry.password.isEmpty) {
      context.read<ToastController>().warning(
        l10n.actionNoPassword,
        detail: entry.isLegacyFormat
            ? l10n.actionNoPasswordLegacy
            : l10n.actionNoPasswordDetail,
      );
      return Future<void>.value();
    }
    return copyValue(context, value: entry.password, label: l10n.labelPassword);
  }

  static Future<void> openUrl(BuildContext context, VaultEntry entry) async {
    final l10n = context.l10n;
    final toasts = context.read<ToastController>();
    var raw = entry.url.trim();
    if (raw.isEmpty) {
      toasts.warning(l10n.actionNoWebsite);
      return;
    }
    if (!raw.contains('://')) raw = 'https://$raw';

    final uri = Uri.tryParse(raw);
    if (uri == null || (uri.scheme != 'http' && uri.scheme != 'https')) {
      toasts.error(l10n.actionBadUrl, detail: l10n.actionBadUrlDetail);
      return;
    }

    try {
      final launched = await launchUrl(
        uri,
        mode: LaunchMode.externalApplication,
      );
      if (!launched) {
        toasts.error(l10n.actionNoBrowser, detail: uri.host);
      }
    } catch (_) {
      toasts.error(l10n.actionLinkFailed, detail: uri.host);
    }
  }

  static Future<void> toggleFavorite(
    BuildContext context,
    VaultEntry entry,
  ) async {
    final l10n = context.l10n;
    final vault = context.read<VaultController>();
    final toasts = context.read<ToastController>();
    final becomingFavorite = !entry.favorite;
    await vault.toggleFavorite(entry.title);
    toasts.show(
      becomingFavorite ? l10n.actionFavoriteAdded : l10n.actionFavoriteRemoved,
      detail: entry.title,
      duration: const Duration(seconds: 2),
    );
  }

  static Future<void> duplicate(BuildContext context, VaultEntry entry) async {
    final l10n = context.l10n;
    final vault = context.read<VaultController>();
    final toasts = context.read<ToastController>();
    final ok = await vault.duplicateEntry(entry.title);
    if (ok) toasts.success(l10n.actionDuplicated, detail: entry.title);
  }

  static Future<void> deleteEntry(
    BuildContext context,
    VaultEntry entry,
  ) async {
    final l10n = context.l10n;
    final vault = context.read<VaultController>();
    final toasts = context.read<ToastController>();
    final settings = context.read<SettingsController>().settings;

    if (settings.confirmDelete) {
      final confirmed = await showConfirmDialog(
        context: context,
        title: l10n.actionDeleteTitle,
        message: l10n.actionDeleteMessage,
        detail: entry.title,
        confirmLabel: l10n.commonDelete,
        destructive: true,
        icon: Icons.delete_outline_rounded,
      );
      if (!confirmed) return;
    }

    final ok = await vault.deleteEntry(entry.title);
    if (!ok) return;

    toasts.show(
      l10n.actionDeleted,
      detail: entry.title,
      kind: ToastKind.warning,
      duration: const Duration(seconds: 8),
      action: ToastAction(
        label: l10n.commonUndo,
        onPressed: () => vault.restoreLastDeleted(),
      ),
    );
  }

  static Future<void> lockVault(BuildContext context) async {
    final vault = context.read<VaultController>();
    final shell = context.read<ShellController>();
    shell.closeCommandPalette();
    shell.reset();
    vault.lock();
  }

  /// Selects [title] and makes sure the vault view is on screen.
  static void openEntry(BuildContext context, String title) {
    final vault = context.read<VaultController>();
    final shell = context.read<ShellController>();
    vault.select(title);
    if (shell.destination != ShellDestination.vault &&
        shell.destination != ShellDestination.favorites &&
        shell.destination != ShellDestination.recent) {
      shell.goTo(ShellDestination.vault);
    }
    shell.showDetail();
  }

  static Future<String?> createEntry(
    BuildContext context, {
    String? initialTitle,
  }) {
    return showAppDialog<String>(
      context: context,
      builder: (_) => EntryCreateDialog(initialTitle: initialTitle),
    );
  }

  /// Right-click menu shared by list rows and the detail header.
  static List<AppMenuEntry?> contextMenu(
    BuildContext context,
    VaultEntry entry,
  ) {
    final l10n = context.l10n;
    return <AppMenuEntry?>[
      AppMenuEntry(
        label: l10n.menuOpen,
        icon: Icons.open_in_new_rounded,
        onSelected: () => openEntry(context, entry.title),
      ),
      AppMenuEntry(
        label: l10n.menuCopyUsername,
        icon: Icons.person_outline_rounded,
        shortcut: 'Ctrl+Shift+U',
        enabled: entry.username.isNotEmpty,
        onSelected: () => copyUsername(context, entry),
      ),
      AppMenuEntry(
        label: l10n.menuCopyPassword,
        icon: Icons.key_rounded,
        shortcut: 'Ctrl+Shift+C',
        enabled: entry.password.isNotEmpty,
        onSelected: () => copyPassword(context, entry),
      ),
      if (entry.url.isNotEmpty)
        AppMenuEntry(
          label: l10n.menuOpenWebsite,
          icon: Icons.language_rounded,
          onSelected: () => openUrl(context, entry),
        ),
      null,
      AppMenuEntry(
        label: entry.favorite ? l10n.menuRemoveFavorite : l10n.menuAddFavorite,
        icon: entry.favorite ? Icons.star_rounded : Icons.star_outline_rounded,
        shortcut: 'Ctrl+D',
        onSelected: () => toggleFavorite(context, entry),
      ),
      AppMenuEntry(
        label: l10n.menuDuplicate,
        icon: Icons.copy_all_rounded,
        onSelected: () => duplicate(context, entry),
      ),
      null,
      AppMenuEntry(
        label: l10n.commonDelete,
        icon: Icons.delete_outline_rounded,
        shortcut: 'Del',
        destructive: true,
        onSelected: () => deleteEntry(context, entry),
      ),
    ];
  }
}
