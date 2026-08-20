import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/app_info.dart';
import '../../app/hotkey_service.dart';
import '../../app/theme/palette.dart';
import '../../app/theme/tokens.dart';
import '../../l10n/l10n.dart';
import '../../core/models/app_settings.dart';
import '../../core/models/vault_entry.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_dialog.dart';
import '../../shared/widgets/app_surface.dart';
import '../../shared/widgets/hover_builder.dart';
import '../../shared/widgets/page_scaffold.dart';
import '../../shared/widgets/tag_chip.dart';
import '../../state/settings_controller.dart';
import '../../state/toast_controller.dart';
import '../../state/shell_controller.dart';
import '../../state/vault_controller.dart';
import '../security/change_master_password_dialog.dart';

/// Preferences. Every control here changes real behaviour — there are no
/// decorative toggles.
class SettingsPage extends StatelessWidget {
  const SettingsPage({super.key});

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final controller = context.watch<SettingsController>();
    final settings = controller.settings;
    final shell = context.watch<ShellController>();
    final vault = context.watch<VaultController>();
    final l10n = context.l10n;

    return PageScaffold(
      title: l10n.settingsTitle,
      subtitle: l10n.settingsSubtitle,
      icon: Icons.tune_rounded,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          _Section(
            title: l10n.settingsAppearance,
            icon: Icons.palette_outlined,
            children: <Widget>[
              _SettingRow(
                title: l10n.settingsTheme,
                description: l10n.settingsThemeDescription,
                control: const SizedBox.shrink(),
                stacked: true,
                stackedChild: Row(
                  children: <Widget>[
                    for (final variant in AppThemeVariant.values) ...<Widget>[
                      Expanded(
                        child: _ThemeCard(
                          variant: variant,
                          selected: settings.theme == variant,
                          onTap: () => controller.setTheme(variant),
                        ),
                      ),
                      if (variant != AppThemeVariant.values.last)
                        const SizedBox(width: Insets.sm),
                    ],
                  ],
                ),
              ),
              _SettingRow(
                title: l10n.settingsLanguage,
                description: l10n.settingsLanguageDescription,
                control: _ChoiceChips<AppLanguage>(
                  value: settings.language,
                  options: <AppLanguage, String>{
                    for (final language in AppLanguage.values)
                      language: language.localizedLabel(l10n),
                  },
                  onChanged: controller.setLanguage,
                ),
              ),
              _SettingRow(
                title: l10n.settingsDensity,
                description: l10n.settingsDensityDescription,
                control: _ChoiceChips<UiDensity>(
                  value: settings.density,
                  options: <UiDensity, String>{
                    for (final density in UiDensity.values)
                      density: density.localizedLabel(l10n),
                  },
                  onChanged: controller.setDensity,
                ),
              ),
              _SettingRow(
                title: l10n.settingsAnimations,
                description: l10n.settingsAnimationsDescription,
                control: Switch(
                  value: !settings.reduceMotion,
                  onChanged: (value) => controller.setReduceMotion(!value),
                ),
              ),
            ],
          ),
          const SizedBox(height: Insets.md),
          _Section(
            title: l10n.settingsSecurity,
            icon: Icons.shield_outlined,
            children: <Widget>[
              _SettingRow(
                title: l10n.settingsAutoLock,
                description: l10n.settingsAutoLockDescription,
                control: _ChoiceChips<int>(
                  value: settings.autoLockMinutes,
                  options: <int, String>{
                    for (final minutes in AppSettings.autoLockChoices)
                      minutes: minutes == 0
                          ? l10n.settingsNever
                          : l10n.settingsMinutesShort(minutes),
                  },
                  onChanged: controller.setAutoLockMinutes,
                ),
              ),
              _SettingRow(
                title: l10n.settingsClipboard,
                description: l10n.settingsClipboardDescription,
                control: _ChoiceChips<int>(
                  value: settings.clipboardClearSeconds,
                  options: <int, String>{
                    for (final seconds in AppSettings.clipboardChoices)
                      seconds: seconds == 0
                          ? l10n.settingsNever
                          : l10n.settingsSecondsShort(seconds),
                  },
                  onChanged: controller.setClipboardClearSeconds,
                ),
              ),
              _SettingRow(
                title: l10n.settingsRevealSecrets,
                description: l10n.settingsRevealSecretsDescription,
                control: Switch(
                  value: settings.revealSecretsByDefault,
                  onChanged: controller.setRevealSecretsByDefault,
                ),
              ),
              _SettingRow(
                title: l10n.settingsConfirmDelete,
                description: l10n.settingsConfirmDeleteDescription,
                control: Switch(
                  value: settings.confirmDelete,
                  onChanged: controller.setConfirmDelete,
                ),
              ),
              _SettingRow(
                title: l10n.settingsKeepHistory,
                description: l10n.settingsKeepHistoryDescription(
                  VaultEntry.maxPasswordHistory,
                ),
                control: Switch(
                  value: settings.keepPasswordHistory,
                  onChanged: controller.setKeepPasswordHistory,
                ),
              ),
              _SettingRow(
                title: l10n.settingsStoredHistory,
                description: vault.storedPasswordHistoryCount == 0
                    ? l10n.settingsStoredHistoryEmpty
                    : l10n.settingsStoredHistoryCount(
                        vault.storedPasswordHistoryCount,
                        vault.entryCount,
                      ),
                control: AppButton(
                  label: l10n.settingsClearAll,
                  icon: Icons.delete_sweep_outlined,
                  variant: AppButtonVariant.danger,
                  size: AppButtonSize.small,
                  onPressed:
                      vault.isUnlocked && vault.storedPasswordHistoryCount > 0
                      ? () => _clearAllHistory(context, vault)
                      : null,
                ),
              ),
              _SettingRow(
                title: l10n.settingsMasterPassword,
                description: l10n.settingsMasterPasswordDescription,
                control: AppButton(
                  label: l10n.settingsChange,
                  variant: AppButtonVariant.secondary,
                  size: AppButtonSize.small,
                  onPressed: vault.isUnlocked
                      ? () => showAppDialog<bool>(
                          context: context,
                          builder: (_) => const ChangeMasterPasswordDialog(),
                        )
                      : null,
                ),
              ),
            ],
          ),
          const SizedBox(height: Insets.md),
          _Section(
            title: l10n.settingsKeyboard,
            icon: Icons.keyboard_alt_outlined,
            children: <Widget>[
              _SettingRow(
                title: l10n.settingsGlobalShortcut,
                description:
                    shell.hotkeyError ?? l10n.settingsGlobalShortcutDescription,
                control: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    if (settings.globalHotkeyEnabled && !shell.hotkeyRegistered)
                      Padding(
                        padding: const EdgeInsets.only(right: Insets.sm),
                        child: AppButton(
                          label: l10n.commonRetry,
                          variant: AppButtonVariant.secondary,
                          size: AppButtonSize.small,
                          onPressed: () =>
                              context.read<HotkeyService>().register(),
                        ),
                      ),
                    Switch(
                      value: settings.globalHotkeyEnabled,
                      onChanged: (value) {
                        controller.setGlobalHotkeyEnabled(value);
                        context.read<HotkeyService>().apply(enabled: value);
                      },
                    ),
                  ],
                ),
              ),
              _SettingRow(
                title: l10n.settingsShortcuts,
                description: l10n.settingsShortcutsDescription,
                stacked: true,
                control: const SizedBox.shrink(),
                stackedChild: const _ShortcutTable(),
              ),
            ],
          ),
          const SizedBox(height: Insets.md),
          _Section(
            title: l10n.settingsVaultWindow,
            icon: Icons.folder_outlined,
            children: <Widget>[
              _SettingRow(
                title: l10n.settingsVaultFile,
                description:
                    vault.fileInfo?.path ?? l10n.settingsVaultFileResolving,
                control: AppButton(
                  label: l10n.dashboardSecurityCenter,
                  variant: AppButtonVariant.secondary,
                  size: AppButtonSize.small,
                  onPressed: () => context.read<ShellController>().goTo(
                    ShellDestination.security,
                  ),
                ),
              ),
              _SettingRow(
                title: l10n.settingsRememberWindow,
                description: l10n.settingsRememberWindowDescription,
                control: Switch(
                  value: settings.rememberWindowBounds,
                  onChanged: controller.setRememberWindowBounds,
                ),
              ),
            ],
          ),
          const SizedBox(height: Insets.md),
          _Section(
            title: l10n.settingsAbout,
            icon: Icons.info_outline_rounded,
            children: <Widget>[
              InfoRow(label: l10n.settingsVersion, value: AppInfo.version),
              InfoRow(
                label: l10n.settingsStorage,
                value: l10n.settingsStorageValue,
              ),
              InfoRow(
                label: l10n.settingsNetworkAccess,
                value: l10n.settingsNetworkAccessValue,
              ),
              Padding(
                padding: const EdgeInsets.only(top: Insets.sm),
                child: Text(AppInfo.description, style: tokens.text.secondary),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

Future<void> _clearAllHistory(
  BuildContext context,
  VaultController vault,
) async {
  final l10n = context.l10n;
  final toasts = context.read<ToastController>();
  final count = vault.storedPasswordHistoryCount;

  final confirmed = await showConfirmDialog(
    context: context,
    title: l10n.settingsClearAllTitle,
    message: l10n.settingsClearAllMessage,
    detail: l10n.settingsClearAllDetail(count),
    confirmLabel: l10n.settingsClearAllConfirm,
    destructive: true,
    icon: Icons.delete_sweep_outlined,
  );
  if (!confirmed) return;

  final cleared = await vault.clearAllPasswordHistory();
  if (cleared) {
    toasts.success(
      l10n.historyCleared,
      detail: l10n.settingsClearAllDone(count),
    );
  }
}

class _Section extends StatelessWidget {
  const _Section({
    required this.title,
    required this.icon,
    required this.children,
  });

  final String title;
  final IconData icon;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          SectionHeader(title: title, icon: icon),
          const SizedBox(height: Insets.md),
          for (var i = 0; i < children.length; i++) ...<Widget>[
            if (i > 0)
              const AppDivider(
                margin: EdgeInsets.symmetric(vertical: Insets.md),
              ),
            children[i],
          ],
        ],
      ),
    );
  }
}

class _SettingRow extends StatelessWidget {
  const _SettingRow({
    required this.title,
    required this.control,
    this.description,
    this.stacked = false,
    this.stackedChild,
  });

  final String title;
  final String? description;
  final Widget control;
  final bool stacked;
  final Widget? stackedChild;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;

    final label = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Text(title, style: tokens.text.bodyStrong),
        if (description != null) ...<Widget>[
          const SizedBox(height: 2),
          Text(description!, style: tokens.text.secondary),
        ],
      ],
    );

    if (stacked) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          label,
          const SizedBox(height: Insets.md),
          ?stackedChild,
        ],
      );
    }

    return LayoutBuilder(
      builder: (context, constraints) {
        if (constraints.maxWidth < 520) {
          return Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              label,
              const SizedBox(height: Insets.md),
              Align(alignment: Alignment.centerLeft, child: control),
            ],
          );
        }
        return Row(
          crossAxisAlignment: CrossAxisAlignment.center,
          children: <Widget>[
            Expanded(child: label),
            const SizedBox(width: Insets.lg),
            control,
          ],
        );
      },
    );
  }
}

class _ChoiceChips<T> extends StatelessWidget {
  const _ChoiceChips({
    required this.value,
    required this.options,
    required this.onChanged,
  });

  final T value;
  final Map<T, String> options;
  final ValueChanged<T> onChanged;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: Insets.xs + 2,
      runSpacing: Insets.xs + 2,
      children: <Widget>[
        for (final option in options.entries)
          TagChip(
            label: option.value,
            selected: option.key == value,
            onTap: () => onChanged(option.key),
          ),
      ],
    );
  }
}

class _ThemeCard extends StatelessWidget {
  const _ThemeCard({
    required this.variant,
    required this.selected,
    required this.onTap,
  });

  final AppThemeVariant variant;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final preview = AppPalette.of(variant);

    return HoverBuilder(
      onTap: onTap,
      builder: (context, state) => AnimatedContainer(
        duration: context.motion.fast,
        curve: context.motion.standard,
        padding: const EdgeInsets.all(Insets.md),
        decoration: BoxDecoration(
          color: state.active ? tokens.color.surfaceHigh : tokens.color.surface,
          borderRadius: Corners.radiusMd,
          border: Border.all(
            color: selected
                ? tokens.color.accent.withValues(alpha: 0.7)
                : tokens.color.border,
            width: selected ? 1.4 : 1,
          ),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Container(
              height: 46,
              decoration: BoxDecoration(
                color: preview.background,
                borderRadius: Corners.radiusSm,
                border: Border.all(color: preview.border),
              ),
              padding: const EdgeInsets.all(Insets.sm),
              child: Row(
                children: <Widget>[
                  Container(
                    width: 14,
                    decoration: BoxDecoration(
                      color: preview.backgroundElevated,
                      borderRadius: Corners.radiusXs,
                    ),
                  ),
                  const SizedBox(width: Insets.xs),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Container(
                          height: 6,
                          width: 42,
                          decoration: BoxDecoration(
                            color: preview.accent,
                            borderRadius: Corners.radiusXs,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Container(
                          height: 6,
                          width: 26,
                          decoration: BoxDecoration(
                            color: preview.secondary.withValues(alpha: 0.7),
                            borderRadius: Corners.radiusXs,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: Insets.sm),
            Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    variant.localizedLabel(context.l10n),
                    style: tokens.text.bodyStrong,
                  ),
                ),
                if (selected)
                  Icon(
                    Icons.check_circle_rounded,
                    size: 15,
                    color: tokens.color.accent,
                  ),
              ],
            ),
            const SizedBox(height: 2),
            Text(
              variant.localizedDescription(context.l10n),
              style: tokens.text.caption,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
            ),
          ],
        ),
      ),
    );
  }
}

class _ShortcutTable extends StatelessWidget {
  const _ShortcutTable();

  static List<(String, String)> shortcuts(AppLocalizations l10n) =>
      <(String, String)>[
        ('Ctrl + K', l10n.shortcutPalette),
        ('Ctrl + Alt + P', l10n.shortcutPaletteGlobal),
        ('Ctrl + N', l10n.shortcutNewEntry),
        ('Ctrl + F', l10n.shortcutFocusSearch),
        ('Ctrl + S', l10n.shortcutSave),
        ('Ctrl + D', l10n.shortcutFavorite),
        ('Ctrl + Shift + C', l10n.shortcutCopyPassword),
        ('Ctrl + Shift + U', l10n.shortcutCopyUsername),
        ('Ctrl + G', l10n.shortcutGenerator),
        ('Ctrl + ,', l10n.shortcutSettings),
        ('Ctrl + L', l10n.shortcutLock),
        ('Delete', l10n.shortcutDelete),
        ('Esc', l10n.shortcutEscape),
        ('↑ / ↓ / Enter', l10n.shortcutNavigate),
      ];

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;

    return Container(
      decoration: BoxDecoration(
        color: palette.background.withValues(alpha: 0.4),
        borderRadius: Corners.radiusSm,
        border: Border.all(color: palette.border),
      ),
      padding: const EdgeInsets.symmetric(
        horizontal: Insets.md,
        vertical: Insets.sm,
      ),
      child: Column(
        children: <Widget>[
          for (final shortcut in shortcuts(context.l10n))
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 5),
              child: Row(
                children: <Widget>[
                  SizedBox(
                    width: 150,
                    child: Text(shortcut.$1, style: tokens.text.mono),
                  ),
                  Expanded(
                    child: Text(shortcut.$2, style: tokens.text.secondary),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}
