import 'dart:io';

import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../app/theme/tokens.dart';
import '../../l10n/l10n.dart';
import '../../core/security/vault_file.dart';
import '../../core/security/vault_kdf.dart';
import '../../core/services/vault_health.dart';
import '../../core/utils/formatting.dart';
import '../../core/utils/rendering_info.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_dialog.dart';
import '../../shared/widgets/app_surface.dart';
import '../../shared/widgets/page_scaffold.dart';
import '../../state/settings_controller.dart';
import '../../state/toast_controller.dart';
import '../../state/vault_controller.dart';
import '../../shared/widgets/strength_meter.dart';
import '../../shared/widgets/tag_chip.dart';
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
    final l10n = context.l10n;
    final legacyFile =
        info?.formatVersion != null &&
        info!.formatVersion! < VaultFileCodec.currentVersion;
    final outdatedKdf = info?.kdf != null && !info!.kdf!.meetsCurrentPolicy;

    return PageScaffold(
      title: l10n.securityTitle,
      subtitle: l10n.securitySubtitle,
      icon: Icons.shield_outlined,
      actions: <Widget>[
        AppButton(
          label: l10n.securityLockNow,
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
                  title: l10n.securitySession,
                  subtitle: l10n.securitySessionSubtitle,
                  icon: Icons.lock_open_rounded,
                  trailing: _StatusPill(
                    label: switch (vault.saveState) {
                      SaveState.saving => l10n.securityStateSaving,
                      SaveState.failed => l10n.securityStateSaveFailed,
                      SaveState.saved => l10n.securityStateSaved,
                      SaveState.idle => l10n.securityStateUnlocked,
                    },
                    color: vault.saveState == SaveState.failed
                        ? palette.danger
                        : palette.success,
                  ),
                ),
                const SizedBox(height: Insets.sm),
                InfoRow(
                  label: l10n.securityEntries,
                  value: l10n.entriesCount(vault.entryCount),
                ),
                InfoRow(
                  label: l10n.securityLastWritten,
                  value: formatRelativeTime(l10n, vault.lastSavedAt),
                ),
                InfoRow(
                  label: l10n.securityMasterPasswordMemory,
                  value: l10n.securityMasterPasswordMemoryValue,
                ),
                Tooltip(
                  message: l10n.securityTotpDescription,
                  child: InfoRow(
                    label: l10n.securityTotpTitle,
                    value: l10n.securityTotpValue(vault.totpCount),
                  ),
                ),
                InfoRow(
                  label: l10n.securityRenderingEngine,
                  value: RenderingInfo.backendLabel,
                  monospace: true,
                ),
                if (vault.saveState == SaveState.failed &&
                    vault.lastSaveError != null) ...<Widget>[
                  const SizedBox(height: Insets.md),
                  _WarningBox(
                    title: vault.lastSaveError!.localizedTitle(l10n),
                    message: vault.lastSaveError!.localizedHint(l10n),
                    action: AppButton(
                      label: l10n.securityRetrySave,
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
          _PasswordHealthCard(report: vault.health),
          const SizedBox(height: Insets.md),
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                SectionHeader(
                  title: l10n.securityEncryption,
                  subtitle: l10n.securityEncryptionSubtitle,
                  icon: Icons.enhanced_encryption_outlined,
                ),
                const SizedBox(height: Insets.sm),
                InfoRow(
                  label: l10n.securityCipher,
                  value: l10n.securityCipherValue,
                  monospace: true,
                ),
                InfoRow(
                  label: l10n.securityKeyDerivation,
                  value: (info?.kdf ?? VaultKdfParams.current).describe(),
                  monospace: true,
                ),
                InfoRow(
                  label: l10n.securityVaultFormat,
                  value: info?.formatVersion == null
                      ? l10n.securityFormatUnknown
                      : 'v${info!.formatVersion}'
                            '${legacyFile ? l10n.securityFormatLegacySuffix : ''}',
                  monospace: true,
                ),
                InfoRow(
                  label: l10n.securityMemoryHard,
                  value: outdatedKdf
                      ? l10n.securityMemoryHardNo
                      : l10n.securityMemoryHardYes,
                ),
                InfoRow(
                  label: l10n.securityHeaderAuth,
                  value: l10n.securityHeaderAuthValue,
                ),
                InfoRow(
                  label: l10n.securityNonce,
                  value: l10n.securityNonceValue,
                ),
                if (outdatedKdf) ...<Widget>[
                  const SizedBox(height: Insets.md),
                  _WarningBox(
                    title: legacyFile
                        ? l10n.securityLegacyFormatTitle
                        : l10n.securityOldKdfTitle,
                    message: l10n.securityOldKdfMessage(
                      info.kdf!.describe(),
                      VaultKdfParams.current.describe(),
                    ),
                  ),
                ],
                const SizedBox(height: Insets.md),
                Text(l10n.securityArgonNote, style: tokens.text.caption),
              ],
            ),
          ),
          const SizedBox(height: Insets.md),
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                SectionHeader(
                  title: l10n.securityVaultFile,
                  subtitle: l10n.securityVaultFileSubtitle,
                  icon: Icons.folder_outlined,
                  trailing: AppButton(
                    label: l10n.securityRefresh,
                    variant: AppButtonVariant.ghost,
                    size: AppButtonSize.small,
                    onPressed: vault.refreshFileInfo,
                  ),
                ),
                const SizedBox(height: Insets.sm),
                InfoRow(
                  label: l10n.securityLocation,
                  value: info?.path ?? l10n.securityResolving,
                  monospace: true,
                  selectable: true,
                  trailing: info == null
                      ? null
                      : Row(
                          mainAxisSize: MainAxisSize.min,
                          children: <Widget>[
                            AppIconButton(
                              icon: Icons.copy_rounded,
                              tooltip: l10n.securityCopyPath,
                              dense: true,
                              size: 15,
                              onPressed: () => VaultActions.copyValue(
                                context,
                                value: info.path,
                                label: l10n.securityVaultPathLabel,
                              ),
                            ),
                            AppIconButton(
                              icon: Icons.folder_open_rounded,
                              tooltip: l10n.securityOpenFolder,
                              dense: true,
                              size: 15,
                              onPressed: () => _openFolder(context, info.path),
                            ),
                          ],
                        ),
                ),
                InfoRow(
                  label: l10n.securitySize,
                  value: info == null ? '—' : Formatting.bytes(info.sizeBytes),
                ),
                InfoRow(
                  label: l10n.securityModified,
                  value: Formatting.absoluteTime(info?.modifiedAt),
                ),
                InfoRow(
                  label: l10n.securityBackupCopy,
                  value: (info?.backupExists ?? false)
                      ? l10n.securityBackupPresent
                      : l10n.securityBackupOnNextSave,
                ),
                InfoRow(
                  label: l10n.securityWrites,
                  value: l10n.securityWritesValue,
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
                  title: l10n.securityProtection,
                  subtitle: l10n.securityProtectionSubtitle,
                  icon: Icons.policy_outlined,
                ),
                const SizedBox(height: Insets.sm),
                InfoRow(
                  label: l10n.infoAutoLock,
                  value: settings.autoLockEnabled
                      ? l10n.infoAutoLockAfter(settings.autoLockMinutes)
                      : l10n.settingsNever,
                ),
                InfoRow(
                  label: l10n.securityMinimizeLabel,
                  value: l10n.securityMinimizeValue,
                ),
                InfoRow(
                  label: l10n.infoClipboard,
                  value: settings.clipboardClearEnabled
                      ? l10n.securityClipboardCleared(
                          settings.clipboardClearSeconds,
                        )
                      : l10n.securityClipboardNever,
                ),
                InfoRow(
                  label: l10n.securitySecretsOnScreen,
                  value: settings.revealSecretsByDefault
                      ? l10n.securitySecretsRevealed
                      : l10n.securitySecretsHidden,
                ),
                InfoRow(
                  label: l10n.securityNetwork,
                  value: l10n.securityNetworkValue,
                ),
                const SizedBox(height: Insets.lg),
                Row(
                  children: <Widget>[
                    AppButton(
                      label: l10n.securityChangeMasterPassword,
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
    final l10n = context.l10n;
    final toasts = context.read<ToastController>();
    final separator = path.lastIndexOf(Platform.pathSeparator);
    final directory = separator <= 0 ? path : path.substring(0, separator);
    try {
      final opened = await launchUrl(Uri.file(directory));
      if (!opened) toasts.warning(l10n.securityFolderOpenFailed);
    } catch (_) {
      toasts.warning(l10n.securityFolderOpenFailed, detail: directory);
    }
  }
}

/// Weak and reused passwords across the vault, with a way to jump straight
/// to the entries that need attention.
class _PasswordHealthCard extends StatelessWidget {
  const _PasswordHealthCard({required this.report});

  final VaultHealthReport report;

  static const int _maxListed = 6;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final l10n = context.l10n;
    final accent = report.isClean
        ? palette.success
        : (report.score >= 70 ? palette.warning : palette.danger);

    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          SectionHeader(
            title: l10n.securityHealthTitle,
            subtitle: l10n.securityHealthSubtitle,
            icon: Icons.health_and_safety_outlined,
            trailing: _StatusPill(
              label: report.withPassword == 0
                  ? l10n.securityHealthNoPasswords
                  : l10n.securityHealthScore(report.score),
              color: accent,
            ),
          ),
          const SizedBox(height: Insets.md),
          Row(
            children: <Widget>[
              _HealthStat(
                label: l10n.securityHealthWithPassword,
                value: '${report.withPassword}',
                color: palette.textSecondary,
              ),
              _HealthStat(
                label: l10n.securityHealthReused,
                value: '${report.reusedEntryCount}',
                color: report.reusedEntryCount == 0
                    ? palette.success
                    : palette.danger,
              ),
              _HealthStat(
                label: l10n.securityHealthWeak,
                value: '${report.weak.length}',
                color: report.weak.isEmpty ? palette.success : palette.warning,
              ),
              _HealthStat(
                label: l10n.securityHealthNoPassword,
                value: '${report.withoutPassword.length}',
                color: palette.textSecondary,
              ),
            ],
          ),
          if (report.withPassword == 0) ...<Widget>[
            const SizedBox(height: Insets.md),
            Text(l10n.securityHealthEmptyMessage, style: tokens.text.secondary),
          ] else if (report.isClean) ...<Widget>[
            const SizedBox(height: Insets.md),
            Row(
              children: <Widget>[
                Icon(
                  Icons.check_circle_outline_rounded,
                  size: 16,
                  color: palette.success,
                ),
                const SizedBox(width: Insets.sm),
                Expanded(
                  child: Text(
                    l10n.securityHealthCleanMessage,
                    style: tokens.text.secondary,
                  ),
                ),
              ],
            ),
          ],
          if (report.reused.isNotEmpty) ...<Widget>[
            const SizedBox(height: Insets.lg),
            Text(l10n.securityHealthReusedTitle, style: tokens.text.label),
            const SizedBox(height: Insets.sm),
            for (final group in report.reused.take(_maxListed))
              _ReusedGroupRow(group: group),
            if (report.reused.length > _maxListed)
              Padding(
                padding: const EdgeInsets.only(top: Insets.xs),
                child: Text(
                  l10n.securityHealthMoreGroups(
                    report.reused.length - _maxListed,
                  ),
                  style: tokens.text.caption,
                ),
              ),
            const SizedBox(height: Insets.sm),
            Text(l10n.securityHealthReuseNote, style: tokens.text.caption),
          ],
          if (report.weak.isNotEmpty) ...<Widget>[
            const SizedBox(height: Insets.lg),
            Text(l10n.securityHealthWeakTitle, style: tokens.text.label),
            const SizedBox(height: Insets.sm),
            Wrap(
              spacing: Insets.sm,
              runSpacing: Insets.sm,
              children: <Widget>[
                for (final entry in report.weak.take(_maxListed))
                  TagChip(
                    label: entry.title,
                    icon: Icons.gpp_maybe_outlined,
                    onTap: () => VaultActions.openEntry(context, entry.title),
                  ),
                if (report.weak.length > _maxListed)
                  Text(
                    l10n.securityHealthMore(report.weak.length - _maxListed),
                    style: tokens.text.caption,
                  ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

class _HealthStat extends StatelessWidget {
  const _HealthStat({
    required this.label,
    required this.value,
    required this.color,
  });

  final String label;
  final String value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    return Expanded(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            value,
            style: tokens.text.display.copyWith(fontSize: 22, color: color),
          ),
          const SizedBox(height: 2),
          Text(label, style: tokens.text.caption),
        ],
      ),
    );
  }
}

class _ReusedGroupRow extends StatelessWidget {
  const _ReusedGroupRow({required this.group});

  final ReusedPasswordGroup group;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final l10n = context.l10n;

    return Container(
      margin: const EdgeInsets.only(bottom: Insets.sm),
      padding: const EdgeInsets.all(Insets.md),
      decoration: BoxDecoration(
        color: palette.surface,
        borderRadius: Corners.radiusSm,
        border: Border.all(color: palette.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Icon(Icons.copy_all_rounded, size: 14, color: palette.danger),
              const SizedBox(width: Insets.sm),
              Text(
                l10n.securityHealthSharedBy(group.count),
                style: tokens.text.bodyStrong,
              ),
              const Spacer(),
              Text(
                group.strength.level.localizedLabel(l10n),
                style: tokens.text.caption.copyWith(
                  color: StrengthMeter.colorFor(context, group.strength.level),
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ),
          const SizedBox(height: Insets.sm),
          Wrap(
            spacing: Insets.sm,
            runSpacing: Insets.sm,
            children: <Widget>[
              for (final entry in group.entries)
                TagChip(
                  label: entry.title,
                  onTap: () => VaultActions.openEntry(context, entry.title),
                ),
            ],
          ),
        ],
      ),
    );
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
