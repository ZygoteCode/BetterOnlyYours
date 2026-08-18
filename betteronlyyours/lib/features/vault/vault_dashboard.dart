import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import '../../core/models/vault_entry.dart';
import '../../core/services/password_strength.dart';
import '../../core/utils/formatting.dart';
import '../../shared/animations/entrance.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_surface.dart';
import '../../shared/widgets/empty_state.dart';
import '../../shared/widgets/entry_avatar.dart';
import '../../shared/widgets/hover_builder.dart';
import '../../state/settings_controller.dart';
import '../../state/shell_controller.dart';
import '../../state/vault_controller.dart';
import 'vault_actions.dart';

/// Landing view for an unlocked vault: what is inside, what you touched last
/// and what to do next.
class VaultDashboard extends StatelessWidget {
  const VaultDashboard({super.key});

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final vault = context.watch<VaultController>();
    final settings = context.watch<SettingsController>().settings;

    if (vault.isEmptyVault) {
      return EmptyState(
        icon: Icons.shield_moon_outlined,
        title: 'Your vault is ready',
        message:
            'Nothing is stored yet. Create your first entry — it is encrypted '
            'locally the moment you save it.',
        actionLabel: 'New entry',
        onAction: () => VaultActions.createEntry(context),
        secondaryActionLabel: 'Open generator',
        onSecondaryAction: () =>
            context.read<ShellController>().goTo(ShellDestination.generator),
        hint: 'Ctrl+N new entry · Ctrl+K command palette · Ctrl+L lock',
      );
    }

    final weak = _weakEntries(vault.allEntries);
    final recents = vault.recentEntries.take(5).toList();
    final modified = vault.recentlyModified.take(5).toList();

    return SingleChildScrollView(
      padding: EdgeInsets.all(tokens.panePadding),
      child: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 1080),
          child: EntranceFade(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: <Widget>[
                _buildHeader(context, vault),
                const SizedBox(height: Insets.xl),
                LayoutBuilder(
                  builder: (context, constraints) {
                    final columns = constraints.maxWidth > 900
                        ? 4
                        : (constraints.maxWidth > 620 ? 2 : 1);
                    final spacing = Insets.md;
                    final width =
                        (constraints.maxWidth - spacing * (columns - 1)) /
                        columns;
                    final tiles = <Widget>[
                      StatTile(
                        label: 'Entries',
                        value: '${vault.entryCount}',
                        icon: Icons.inventory_2_outlined,
                        caption: vault.legacyEntries.isEmpty
                            ? 'All structured'
                            : '${vault.legacyEntries.length} legacy',
                        onTap: () => context.read<ShellController>().goTo(
                          ShellDestination.vault,
                        ),
                      ),
                      StatTile(
                        label: 'Favorites',
                        value: '${vault.favorites.length}',
                        icon: Icons.star_rounded,
                        accent: tokens.color.warning,
                        caption: 'Starred for quick access',
                        onTap: () => context.read<ShellController>().goTo(
                          ShellDestination.favorites,
                        ),
                      ),
                      StatTile(
                        label: 'Tags',
                        value: '${vault.allTags.length}',
                        icon: Icons.sell_outlined,
                        accent: tokens.color.secondary,
                        caption: vault.allTags.isEmpty
                            ? 'Add tags to group entries'
                            : vault.allTags.take(3).join(' · '),
                      ),
                      StatTile(
                        label: 'Weak passwords',
                        value: '${weak.length}',
                        icon: Icons.gpp_maybe_outlined,
                        accent: weak.isEmpty
                            ? tokens.color.success
                            : tokens.color.danger,
                        caption: weak.isEmpty
                            ? 'Nothing obviously weak'
                            : 'Review and regenerate',
                        onTap: weak.isEmpty
                            ? null
                            : () => VaultActions.openEntry(
                                context,
                                weak.first.title,
                              ),
                      ),
                    ];
                    return Wrap(
                      spacing: spacing,
                      runSpacing: spacing,
                      children: <Widget>[
                        for (final tile in tiles)
                          SizedBox(width: width, child: tile),
                      ],
                    );
                  },
                ),
                const SizedBox(height: Insets.xl),
                LayoutBuilder(
                  builder: (context, constraints) {
                    final twoColumns = constraints.maxWidth > 780;
                    final left = _RecentList(
                      title: 'Recently opened',
                      icon: Icons.history_rounded,
                      entries: recents,
                      emptyMessage: 'Open an entry and it shows up here.',
                      subtitleBuilder: (entry) => Formatting.relativeTime(
                        vault.lastUsedAt(entry.title),
                      ),
                    );
                    final right = _RecentList(
                      title: 'Recently modified',
                      icon: Icons.edit_calendar_outlined,
                      entries: modified,
                      emptyMessage:
                          'Entries you edit appear here with their timestamp.',
                      subtitleBuilder: (entry) =>
                          Formatting.relativeTime(entry.updatedAt),
                    );
                    if (!twoColumns) {
                      return Column(
                        children: <Widget>[
                          left,
                          const SizedBox(height: Insets.md),
                          right,
                        ],
                      );
                    }
                    return Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Expanded(child: left),
                        const SizedBox(width: Insets.md),
                        Expanded(child: right),
                      ],
                    );
                  },
                ),
                const SizedBox(height: Insets.xl),
                _buildSecuritySnapshot(
                  context,
                  vault,
                  settings.autoLockMinutes,
                  settings.clipboardClearSeconds,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildHeader(BuildContext context, VaultController vault) {
    final tokens = context.tokens;
    final shell = context.read<ShellController>();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text('Vault', style: tokens.text.display),
        const SizedBox(height: Insets.xs),
        Text(
          '${Formatting.plural(vault.entryCount, 'entry', 'entries')} '
          'encrypted on this machine. Select an entry to view it, or start '
          'something new.',
          style: tokens.text.secondary,
        ),
        const SizedBox(height: Insets.lg),
        Wrap(
          spacing: Insets.sm,
          runSpacing: Insets.sm,
          children: <Widget>[
            AppButton(
              label: 'New entry',
              icon: Icons.add_rounded,
              tooltip: 'Ctrl+N',
              onPressed: () => VaultActions.createEntry(context),
            ),
            AppButton(
              label: 'Search',
              icon: Icons.search_rounded,
              variant: AppButtonVariant.secondary,
              tooltip: 'Ctrl+K',
              onPressed: shell.openCommandPalette,
            ),
            AppButton(
              label: 'Generate password',
              icon: Icons.casino_rounded,
              variant: AppButtonVariant.secondary,
              tooltip: 'Ctrl+G',
              onPressed: () => shell.goTo(ShellDestination.generator),
            ),
            AppButton(
              label: 'Lock vault',
              icon: Icons.lock_outline_rounded,
              variant: AppButtonVariant.ghost,
              tooltip: 'Ctrl+L',
              onPressed: () => VaultActions.lockVault(context),
            ),
          ],
        ),
      ],
    );
  }

  Widget _buildSecuritySnapshot(
    BuildContext context,
    VaultController vault,
    int autoLockMinutes,
    int clipboardSeconds,
  ) {
    final tokens = context.tokens;
    final info = vault.fileInfo;

    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          SectionHeader(
            title: 'Security snapshot',
            subtitle: 'Current protection settings for this vault.',
            icon: Icons.shield_outlined,
            trailing: AppButton(
              label: 'Security center',
              variant: AppButtonVariant.ghost,
              size: AppButtonSize.small,
              onPressed: () => context.read<ShellController>().goTo(
                ShellDestination.security,
              ),
            ),
          ),
          const SizedBox(height: Insets.sm),
          InfoRow(
            label: 'Encryption',
            value:
                'AES-256-GCM · PBKDF2-HMAC-SHA256'
                '${info?.iterations != null ? ' · ${info!.iterations} iterations' : ''}',
          ),
          InfoRow(
            label: 'Auto-lock',
            value: autoLockMinutes == 0
                ? 'Off — minimizing never locks the vault'
                : 'After $autoLockMinutes minutes of inactivity',
          ),
          InfoRow(
            label: 'Clipboard',
            value: clipboardSeconds == 0
                ? 'Kept until you replace it'
                : 'Cleared $clipboardSeconds seconds after copying',
          ),
          InfoRow(
            label: 'Last saved',
            value: Formatting.relativeTime(vault.lastSavedAt),
          ),
          if (vault.legacyEntries.isNotEmpty) ...<Widget>[
            const SizedBox(height: Insets.sm),
            Text(
              '${vault.legacyEntries.length} entries still use the legacy '
              'plain-text format. Open one and fill in a field to upgrade it.',
              style: tokens.text.caption,
            ),
          ],
        ],
      ),
    );
  }

  static List<VaultEntry> _weakEntries(List<VaultEntry> entries) {
    return entries.where((entry) {
      if (entry.password.isEmpty) return false;
      final strength = PasswordStrength.evaluate(entry.password);
      return strength.level == StrengthLevel.veryWeak ||
          strength.level == StrengthLevel.weak;
    }).toList();
  }
}

class _RecentList extends StatelessWidget {
  const _RecentList({
    required this.title,
    required this.icon,
    required this.entries,
    required this.emptyMessage,
    required this.subtitleBuilder,
  });

  final String title;
  final IconData icon;
  final List<VaultEntry> entries;
  final String emptyMessage;
  final String Function(VaultEntry entry) subtitleBuilder;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;

    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          SectionHeader(title: title, icon: icon),
          const SizedBox(height: Insets.md),
          if (entries.isEmpty)
            Text(emptyMessage, style: tokens.text.caption)
          else
            for (final entry in entries)
              HoverBuilder(
                onTap: () => VaultActions.openEntry(context, entry.title),
                builder: (context, state) => AnimatedContainer(
                  duration: context.motion.fast,
                  padding: const EdgeInsets.symmetric(
                    horizontal: Insets.sm,
                    vertical: Insets.sm,
                  ),
                  margin: const EdgeInsets.only(bottom: Insets.xs),
                  decoration: BoxDecoration(
                    color: state.active
                        ? tokens.color.surfaceHigh
                        : Colors.transparent,
                    borderRadius: Corners.radiusSm,
                  ),
                  child: Row(
                    children: <Widget>[
                      EntryAvatar(entry: entry, size: 26),
                      const SizedBox(width: Insets.md),
                      Expanded(
                        child: Text(
                          entry.title,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: tokens.text.body,
                        ),
                      ),
                      Text(subtitleBuilder(entry), style: tokens.text.caption),
                    ],
                  ),
                ),
              ),
        ],
      ),
    );
  }
}
