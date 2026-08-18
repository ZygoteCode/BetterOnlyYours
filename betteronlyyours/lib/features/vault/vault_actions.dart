import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../core/models/vault_entry.dart';
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
        'Clipboard unavailable',
        detail: 'Windows refused the copy — another app may be holding it.',
      );
      return;
    }

    toasts.success(
      '$label copied',
      detail: settings.clipboardClearEnabled
          ? 'Clipboard clears in ${settings.clipboardClearSeconds}s '
                'unless you copy something else.'
          : null,
    );
  }

  static Future<void> copyUsername(BuildContext context, VaultEntry entry) {
    if (entry.username.isEmpty) {
      context.read<ToastController>().warning(
        'No username on this entry',
        detail: 'Add one from the entry details.',
      );
      return Future<void>.value();
    }
    return copyValue(context, value: entry.username, label: 'Username');
  }

  static Future<void> copyPassword(BuildContext context, VaultEntry entry) {
    if (entry.password.isEmpty) {
      context.read<ToastController>().warning(
        'No password on this entry',
        detail: entry.isLegacyFormat
            ? 'This entry still holds free-form notes only.'
            : 'Add or generate one from the entry details.',
      );
      return Future<void>.value();
    }
    return copyValue(context, value: entry.password, label: 'Password');
  }

  static Future<void> openUrl(BuildContext context, VaultEntry entry) async {
    final toasts = context.read<ToastController>();
    var raw = entry.url.trim();
    if (raw.isEmpty) {
      toasts.warning('No website on this entry');
      return;
    }
    if (!raw.contains('://')) raw = 'https://$raw';

    final uri = Uri.tryParse(raw);
    if (uri == null || (uri.scheme != 'http' && uri.scheme != 'https')) {
      toasts.error(
        'That address cannot be opened',
        detail: 'Only http and https links are launched.',
      );
      return;
    }

    try {
      final launched = await launchUrl(
        uri,
        mode: LaunchMode.externalApplication,
      );
      if (!launched) {
        toasts.error('No browser responded', detail: uri.host);
      }
    } catch (_) {
      toasts.error('The link could not be opened', detail: uri.host);
    }
  }

  static Future<void> toggleFavorite(
    BuildContext context,
    VaultEntry entry,
  ) async {
    final vault = context.read<VaultController>();
    final toasts = context.read<ToastController>();
    final becomingFavorite = !entry.favorite;
    await vault.toggleFavorite(entry.title);
    toasts.show(
      becomingFavorite ? 'Added to favorites' : 'Removed from favorites',
      detail: entry.title,
      duration: const Duration(seconds: 2),
    );
  }

  static Future<void> duplicate(BuildContext context, VaultEntry entry) async {
    final vault = context.read<VaultController>();
    final toasts = context.read<ToastController>();
    final ok = await vault.duplicateEntry(entry.title);
    if (ok) toasts.success('Entry duplicated', detail: entry.title);
  }

  static Future<void> deleteEntry(
    BuildContext context,
    VaultEntry entry,
  ) async {
    final vault = context.read<VaultController>();
    final toasts = context.read<ToastController>();
    final settings = context.read<SettingsController>().settings;

    if (settings.confirmDelete) {
      final confirmed = await showConfirmDialog(
        context: context,
        title: 'Delete this entry?',
        message:
            'It is removed from the encrypted vault. You can undo this right '
            'after, from the notification.',
        detail: entry.title,
        confirmLabel: 'Delete',
        destructive: true,
        icon: Icons.delete_outline_rounded,
      );
      if (!confirmed) return;
    }

    final ok = await vault.deleteEntry(entry.title);
    if (!ok) return;

    toasts.show(
      'Entry deleted',
      detail: entry.title,
      kind: ToastKind.warning,
      duration: const Duration(seconds: 8),
      action: ToastAction(
        label: 'Undo',
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
    return <AppMenuEntry?>[
      AppMenuEntry(
        label: 'Open',
        icon: Icons.open_in_new_rounded,
        onSelected: () => openEntry(context, entry.title),
      ),
      AppMenuEntry(
        label: 'Copy username',
        icon: Icons.person_outline_rounded,
        shortcut: 'Ctrl+Shift+U',
        enabled: entry.username.isNotEmpty,
        onSelected: () => copyUsername(context, entry),
      ),
      AppMenuEntry(
        label: 'Copy password',
        icon: Icons.key_rounded,
        shortcut: 'Ctrl+Shift+C',
        enabled: entry.password.isNotEmpty,
        onSelected: () => copyPassword(context, entry),
      ),
      if (entry.url.isNotEmpty)
        AppMenuEntry(
          label: 'Open website',
          icon: Icons.language_rounded,
          onSelected: () => openUrl(context, entry),
        ),
      null,
      AppMenuEntry(
        label: entry.favorite ? 'Remove from favorites' : 'Add to favorites',
        icon: entry.favorite ? Icons.star_rounded : Icons.star_outline_rounded,
        shortcut: 'Ctrl+D',
        onSelected: () => toggleFavorite(context, entry),
      ),
      AppMenuEntry(
        label: 'Duplicate',
        icon: Icons.copy_all_rounded,
        onSelected: () => duplicate(context, entry),
      ),
      null,
      AppMenuEntry(
        label: 'Delete',
        icon: Icons.delete_outline_rounded,
        shortcut: 'Del',
        destructive: true,
        onSelected: () => deleteEntry(context, entry),
      ),
    ];
  }
}
