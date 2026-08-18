import 'dart:io';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../app/theme/tokens.dart';
import '../../core/security/vault_file.dart';
import '../../core/utils/formatting.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_dialog.dart';
import '../../shared/widgets/app_surface.dart';
import '../../shared/widgets/page_scaffold.dart';
import '../../state/settings_controller.dart';
import '../../state/toast_controller.dart';
import '../../state/vault_controller.dart';
import '../vault/vault_actions.dart';
import 'change_master_password_dialog.dart';

/// Facts about how this vault is protected — no marketing claims, only what
/// the implementation actually does.
class SecurityCenterPage extends StatelessWidget {
  const SecurityCenterPage({super.key});

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final vault = context.watch<VaultController>();
    final settings = context.watch<SettingsController>().settings;
    final info = vault.fileInfo;
    final legacyFile = info?.formatVersion == VaultFileCodec.legacyVersion;

    return PageScaffold(
      title: 'Security',
      subtitle: 'How this vault is encrypted, stored and protected.',
      icon: Icons.shield_outlined,
      actions: <Widget>[
        AppButton(
          label: 'Lock now',
          icon: Icons.lock_outline_rounded,
          variant: AppButtonVariant.secondary,
          size: AppButtonSize.small,
          onPressed: () => VaultActions.lockVault(context),
        ),
      ],
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                SectionHeader(
                  title: 'Session',
                  subtitle: 'Current state of the unlocked vault.',
                  icon: Icons.lock_open_rounded,
                  trailing: _StatusPill(
                    label: switch (vault.saveState) {
                      SaveState.saving => 'Saving…',
                      SaveState.failed => 'Save failed',
                      SaveState.saved => 'Saved',
                      SaveState.idle => 'Unlocked',
                    },
                    color: vault.saveState == SaveState.failed
                        ? palette.danger
                        : palette.success,
                  ),
                ),
                const SizedBox(height: Insets.sm),
                InfoRow(
                  label: 'Entries',
                  value: Formatting.plural(
                    vault.entryCount,
                    'entry',
                    'entries',
                  ),
                ),
                InfoRow(
                  label: 'Last written',
                  value: Formatting.relativeTime(vault.lastSavedAt),
                ),
                InfoRow(
                  label: 'Master password in memory',
                  value:
                      'No — only the derived key is kept while unlocked, and '
                      'it is wiped on lock.',
                ),
                if (vault.saveState == SaveState.failed &&
                    vault.lastSaveError != null) ...<Widget>[
                  const SizedBox(height: Insets.md),
                  _WarningBox(
                    title: vault.lastSaveError!.title,
                    message: vault.lastSaveError!.hint,
                    action: AppButton(
                      label: 'Retry save',
                      size: AppButtonSize.small,
                      variant: AppButtonVariant.secondary,
                      onPressed: vault.retrySave,
                    ),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(height: Insets.md),
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                const SectionHeader(
                  title: 'Encryption',
                  subtitle: 'Primitives used by this build.',
                  icon: Icons.enhanced_encryption_outlined,
                ),
                const SizedBox(height: Insets.sm),
                const InfoRow(
                  label: 'Cipher',
                  value: 'AES-256-GCM (128-bit authentication tag)',
                  monospace: true,
                ),
                InfoRow(
                  label: 'Key derivation',
                  value:
                      'PBKDF2-HMAC-SHA256, '
                      '${info?.iterations ?? VaultFileCodec.currentIterations} '
                      'iterations, 16-byte salt',
                  monospace: true,
                ),
                InfoRow(
                  label: 'Vault format',
                  value: info?.formatVersion == null
                      ? 'Unknown'
                      : 'v${info!.formatVersion}'
                            '${legacyFile ? ' (legacy)' : ''}',
                  monospace: true,
                ),
                const InfoRow(
                  label: 'Header authentication',
                  value:
                      'The full header (version, KDF, iterations, salt, nonce) '
                      'is authenticated as GCM associated data.',
                ),
                const InfoRow(
                  label: 'Nonce',
                  value: 'Fresh 96-bit random nonce for every save',
                ),
                if (legacyFile) ...<Widget>[
                  const SizedBox(height: Insets.md),
                  _WarningBox(
                    title: 'Legacy vault format detected',
                    message:
                        'This file still uses the original v1 layout with '
                        '3,000 PBKDF2 iterations. Unlocking it re-encrypts it '
                        'automatically with '
                        '${VaultFileCodec.currentIterations} iterations.',
                  ),
                ],
                const SizedBox(height: Insets.md),
                Text(
                  'PBKDF2 with a high iteration count slows down guessing, but '
                  'it is not memory-hard: a long, unique master password is '
                  'still what protects the vault.',
                  style: tokens.text.caption,
                ),
              ],
            ),
          ),
          const SizedBox(height: Insets.md),
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                SectionHeader(
                  title: 'Vault file',
                  subtitle: 'Everything lives in a single encrypted file.',
                  icon: Icons.folder_outlined,
                  trailing: AppButton(
                    label: 'Refresh',
                    variant: AppButtonVariant.ghost,
                    size: AppButtonSize.small,
                    onPressed: vault.refreshFileInfo,
                  ),
                ),
                const SizedBox(height: Insets.sm),
                InfoRow(
                  label: 'Location',
                  value: info?.path ?? 'Resolving…',
                  monospace: true,
                  selectable: true,
                  trailing: info == null
                      ? null
                      : Row(
                          mainAxisSize: MainAxisSize.min,
                          children: <Widget>[
                            AppIconButton(
                              icon: Icons.copy_rounded,
                              tooltip: 'Copy path',
                              dense: true,
                              size: 15,
                              onPressed: () => VaultActions.copyValue(
                                context,
                                value: info.path,
                                label: 'Vault path',
                              ),
                            ),
                            AppIconButton(
                              icon: Icons.folder_open_rounded,
                              tooltip: 'Open containing folder',
                              dense: true,
                              size: 15,
                              onPressed: () => _openFolder(context, info.path),
                            ),
                          ],
                        ),
                ),
                InfoRow(
                  label: 'Size',
                  value: info == null ? '—' : Formatting.bytes(info.sizeBytes),
                ),
                InfoRow(
                  label: 'Modified',
                  value: Formatting.absoluteTime(info?.modifiedAt),
                ),
                InfoRow(
                  label: 'Backup copy',
                  value: (info?.backupExists ?? false)
                      ? 'credentials.plf.bak kept next to the vault'
                      : 'Created automatically on the next save',
                ),
                const InfoRow(
                  label: 'Writes',
                  value:
                      'Atomic: written to a temp file, then renamed over the '
                      'vault, so an interrupted save cannot corrupt it.',
                ),
              ],
            ),
          ),
          const SizedBox(height: Insets.md),
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                const SectionHeader(
                  title: 'Protection',
                  subtitle: 'Session behaviour you control in Settings.',
                  icon: Icons.policy_outlined,
                ),
                const SizedBox(height: Insets.sm),
                InfoRow(
                  label: 'Auto-lock',
                  value: settings.autoLockEnabled
                      ? 'After ${settings.autoLockMinutes} minutes without '
                            'keyboard or mouse activity'
                      : 'Off',
                ),
                const InfoRow(
                  label: 'Minimize / Alt-Tab',
                  value:
                      'Never locks the vault — copy a secret, switch apps and '
                      'come back to an open vault.',
                ),
                InfoRow(
                  label: 'Clipboard',
                  value: settings.clipboardClearEnabled
                      ? 'Cleared after ${settings.clipboardClearSeconds}s, and '
                            'only if the copied value is still there'
                      : 'Never cleared automatically',
                ),
                InfoRow(
                  label: 'Secrets on screen',
                  value: settings.revealSecretsByDefault
                      ? 'Revealed by default in the editor'
                      : 'Hidden until you reveal them',
                ),
                const InfoRow(
                  label: 'Network',
                  value:
                      'None. No telemetry, no sync, no favicon lookups, no '
                      'update checks.',
                ),
                const SizedBox(height: Insets.lg),
                Row(
                  children: <Widget>[
                    AppButton(
                      label: 'Change master password',
                      icon: Icons.password_rounded,
                      variant: AppButtonVariant.secondary,
                      onPressed: () => showAppDialog<bool>(
                        context: context,
                        builder: (_) => const ChangeMasterPasswordDialog(),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _openFolder(BuildContext context, String path) async {
    final toasts = context.read<ToastController>();
    final separator = path.lastIndexOf(Platform.pathSeparator);
    final directory = separator <= 0 ? path : path.substring(0, separator);
    try {
      final opened = await launchUrl(Uri.file(directory));
      if (!opened) toasts.warning('The folder could not be opened');
    } catch (_) {
      toasts.warning('The folder could not be opened', detail: directory);
    }
  }
}

class _StatusPill extends StatelessWidget {
  const _StatusPill({required this.label, required this.color});

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: Insets.md,
        vertical: Insets.xs,
      ),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: Corners.radiusSm,
        border: Border.all(color: color.withValues(alpha: 0.4)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Container(
            width: 6,
            height: 6,
            decoration: BoxDecoration(color: color, shape: BoxShape.circle),
          ),
          const SizedBox(width: Insets.sm),
          Text(
            label,
            style: tokens.text.caption.copyWith(
              color: color,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}

class _WarningBox extends StatelessWidget {
  const _WarningBox({required this.title, required this.message, this.action});

  final String title;
  final String message;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;

    return Container(
      padding: const EdgeInsets.all(Insets.md),
      decoration: BoxDecoration(
        color: palette.warning.withValues(alpha: 0.08),
        borderRadius: Corners.radiusSm,
        border: Border.all(color: palette.warning.withValues(alpha: 0.32)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(Icons.warning_amber_rounded, size: 16, color: palette.warning),
          const SizedBox(width: Insets.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(title, style: tokens.text.bodyStrong),
                const SizedBox(height: 2),
                Text(message, style: tokens.text.secondary),
              ],
            ),
          ),
          if (action != null) ...<Widget>[
            const SizedBox(width: Insets.md),
            action!,
          ],
        ],
      ),
    );
  }
}
