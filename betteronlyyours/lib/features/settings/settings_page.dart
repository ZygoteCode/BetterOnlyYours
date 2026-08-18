import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../app/app_info.dart';
import '../../app/hotkey_service.dart';
import '../../app/theme/palette.dart';
import '../../app/theme/tokens.dart';
import '../../core/models/app_settings.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_dialog.dart';
import '../../shared/widgets/app_surface.dart';
import '../../shared/widgets/hover_builder.dart';
import '../../shared/widgets/page_scaffold.dart';
import '../../shared/widgets/tag_chip.dart';
import '../../state/settings_controller.dart';
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

    return PageScaffold(
      title: 'Settings',
      subtitle: 'Appearance, security behaviour, keyboard and vault storage.',
      icon: Icons.tune_rounded,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          _Section(
            title: 'Appearance',
            icon: Icons.palette_outlined,
            children: <Widget>[
              _SettingRow(
                title: 'Theme',
                description: 'All variants are dark; pick the one you prefer.',
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
                title: 'Density',
                description: 'Compact fits more rows on smaller windows.',
                control: _ChoiceChips<UiDensity>(
                  value: settings.density,
                  options: <UiDensity, String>{
                    for (final density in UiDensity.values)
                      density: density.label,
                  },
                  onChanged: controller.setDensity,
                ),
              ),
              _SettingRow(
                title: 'Animations',
                description:
                    'Turn off transitions and micro-interactions entirely.',
                control: Switch(
                  value: !settings.reduceMotion,
                  onChanged: (value) => controller.setReduceMotion(!value),
                ),
              ),
            ],
          ),
          const SizedBox(height: Insets.md),
          _Section(
            title: 'Security',
            icon: Icons.shield_outlined,
            children: <Widget>[
              _SettingRow(
                title: 'Auto-lock after inactivity',
                description:
                    'Minimizing, Alt-Tab or losing focus never lock the vault '
                    '— only this timer and the manual lock do.',
                control: _ChoiceChips<int>(
                  value: settings.autoLockMinutes,
                  options: <int, String>{
                    for (final minutes in AppSettings.autoLockChoices)
                      minutes: minutes == 0 ? 'Never' : '${minutes}m',
                  },
                  onChanged: controller.setAutoLockMinutes,
                ),
              ),
              _SettingRow(
                title: 'Clear clipboard after copying',
                description:
                    'Only clears if the copied secret is still on the '
                    'clipboard.',
                control: _ChoiceChips<int>(
                  value: settings.clipboardClearSeconds,
                  options: <int, String>{
                    for (final seconds in AppSettings.clipboardChoices)
                      seconds: seconds == 0 ? 'Never' : '${seconds}s',
                  },
                  onChanged: controller.setClipboardClearSeconds,
                ),
              ),
              _SettingRow(
                title: 'Reveal secrets by default',
                description:
                    'When off, passwords and secret fields start hidden.',
                control: Switch(
                  value: settings.revealSecretsByDefault,
                  onChanged: controller.setRevealSecretsByDefault,
                ),
              ),
              _SettingRow(
                title: 'Confirm before deleting',
                description:
                    'Deleting always offers an undo; this adds a dialog first.',
                control: Switch(
                  value: settings.confirmDelete,
                  onChanged: controller.setConfirmDelete,
                ),
              ),
              _SettingRow(
                title: 'Master password',
                description: 'Re-encrypts the vault with a new key.',
                control: AppButton(
                  label: 'Change',
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
            title: 'Keyboard',
            icon: Icons.keyboard_alt_outlined,
            children: <Widget>[
              _SettingRow(
                title: 'Global shortcut (Ctrl+Alt+P)',
                description:
                    shell.hotkeyError ??
                    'Brings BetterOnlyYours forward from any application and '
                        'opens the command palette.',
                control: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    if (settings.globalHotkeyEnabled && !shell.hotkeyRegistered)
                      Padding(
                        padding: const EdgeInsets.only(right: Insets.sm),
                        child: AppButton(
                          label: 'Retry',
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
                title: 'Shortcuts',
                description: 'Everything reachable without the mouse.',
                stacked: true,
                control: const SizedBox.shrink(),
                stackedChild: const _ShortcutTable(),
              ),
            ],
          ),
          const SizedBox(height: Insets.md),
          _Section(
            title: 'Vault & window',
            icon: Icons.folder_outlined,
            children: <Widget>[
              _SettingRow(
                title: 'Vault file',
                description:
                    vault.fileInfo?.path ?? 'Resolving the vault location…',
                control: AppButton(
                  label: 'Security center',
                  variant: AppButtonVariant.secondary,
                  size: AppButtonSize.small,
                  onPressed: () => context.read<ShellController>().goTo(
                    ShellDestination.security,
                  ),
                ),
              ),
              _SettingRow(
                title: 'Remember window size and position',
                description:
                    'Restores the window where you left it on the next start.',
                control: Switch(
                  value: settings.rememberWindowBounds,
                  onChanged: controller.setRememberWindowBounds,
                ),
              ),
            ],
          ),
          const SizedBox(height: Insets.md),
          _Section(
            title: 'About',
            icon: Icons.info_outline_rounded,
            children: <Widget>[
              InfoRow(label: 'Version', value: AppInfo.version),
              const InfoRow(
                label: 'Storage',
                value: 'A single encrypted file, written atomically.',
              ),
              const InfoRow(
                label: 'Network access',
                value: 'None — the app never connects to anything.',
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
                  child: Text(variant.label, style: tokens.text.bodyStrong),
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
              variant.description,
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

  static const List<(String, String)> shortcuts = <(String, String)>[
    ('Ctrl + K', 'Command palette'),
    ('Ctrl + Alt + P', 'Command palette from anywhere in Windows'),
    ('Ctrl + N', 'New entry'),
    ('Ctrl + F', 'Focus the vault search field'),
    ('Ctrl + S', 'Save the entry being edited'),
    ('Ctrl + D', 'Toggle favorite on the selected entry'),
    ('Ctrl + Shift + C', 'Copy the selected password'),
    ('Ctrl + Shift + U', 'Copy the selected username'),
    ('Ctrl + G', 'Password generator'),
    ('Ctrl + ,', 'Settings'),
    ('Ctrl + L', 'Lock the vault'),
    ('Delete', 'Delete the selected entry'),
    ('Esc', 'Close the palette, dialog or search'),
    ('↑ / ↓ / Enter', 'Move through results and open'),
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
          for (final shortcut in shortcuts)
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
