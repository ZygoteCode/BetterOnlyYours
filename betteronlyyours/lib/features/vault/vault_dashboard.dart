import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import '../../l10n/l10n.dart';
import '../../core/models/vault_entry.dart';
import '../../core/security/vault_kdf.dart';
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
    final l10n = context.l10n;

    if (vault.isEmptyVault) {
      return EmptyState(
        icon: Icons.shield_moon_outlined,
        title: l10n.dashboardReadyTitle,
        message: l10n.dashboardReadyMessage,
        actionLabel: l10n.newEntry,
        onAction: () => VaultActions.createEntry(context),
        secondaryActionLabel: l10n.dashboardOpenGenerator,
        onSecondaryAction: () =>
            context.read<ShellController>().goTo(ShellDestination.generator),
        hint: l10n.dashboardShortcutHint,
      );
    }

    final health = vault.health;
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
                        label: l10n.statEntries,
                        value: '${vault.entryCount}',
                        icon: Icons.inventory_2_outlined,
                        caption: vault.legacyEntries.isEmpty
                            ? l10n.statEntriesAllStructured
                            : l10n.statEntriesLegacy(
                                vault.legacyEntries.length,
                              ),
                        onTap: () => context.read<ShellController>().goTo(
                          ShellDestination.vault,
                        ),
                      ),
                      StatTile(
                        label: l10n.statFavorites,
                        value: '${vault.favorites.length}',
                        icon: Icons.star_rounded,
                        accent: tokens.color.warning,
                        caption: l10n.statFavoritesCaption,
                        onTap: () => context.read<ShellController>().goTo(
                          ShellDestination.favorites,
                        ),
                      ),
                      StatTile(
                        label: l10n.statTags,
                        value: '${vault.allTags.length}',
                        icon: Icons.sell_outlined,
                        accent: tokens.color.secondary,
                        caption: vault.allTags.isEmpty
                            ? l10n.statTagsEmpty
                            : vault.allTags.take(3).join(' · '),
                      ),
                      StatTile(
                        label: l10n.statHealth,
                        value: '${health.score}',
                        icon: health.isClean
                            ? Icons.verified_user_outlined
                            : Icons.gpp_maybe_outlined,
                        accent: health.isClean
                            ? tokens.color.success
                            : (health.score >= 70
                                  ? tokens.color.warning
                                  : tokens.color.danger),
                        caption: health.isClean
                            ? l10n.statHealthClean
                            : l10n.statHealthIssues(
                                health.weak.length,
                                health.reusedEntryCount,
                              ),
                        onTap: () => context.read<ShellController>().goTo(
                          ShellDestination.security,
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
                      title: l10n.dashboardRecentlyOpened,
                      icon: Icons.history_rounded,
                      entries: recents,
                      emptyMessage: l10n.dashboardRecentlyOpenedEmpty,
                      subtitleBuilder: (entry) => formatRelativeTime(
                        l10n,
                        vault.lastUsedAt(entry.title),
                      ),
                    );
                    final right = _RecentList(
                      title: l10n.dashboardRecentlyModified,
                      icon: Icons.edit_calendar_outlined,
                      entries: modified,
                      emptyMessage: l10n.dashboardRecentlyModifiedEmpty,
                      subtitleBuilder: (entry) =>
                          formatRelativeTime(l10n, entry.updatedAt),
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
    final l10n = context.l10n;
    final shell = context.read<ShellController>();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(l10n.navVault, style: tokens.text.display),
        const SizedBox(height: Insets.xs),
        Text(
          l10n.dashboardSubtitle(l10n.entriesCount(vault.entryCount)),
          style: tokens.text.secondary,
        ),
        const SizedBox(height: Insets.lg),
        Wrap(
          spacing: Insets.sm,
          runSpacing: Insets.sm,
          children: <Widget>[
            AppButton(
              label: l10n.newEntry,
              icon: Icons.add_rounded,
              tooltip: 'Ctrl+N',
              onPressed: () => VaultActions.createEntry(context),
            ),
            AppButton(
              label: l10n.dashboardSearch,
              icon: Icons.search_rounded,
              variant: AppButtonVariant.secondary,
              tooltip: 'Ctrl+K',
              onPressed: shell.openCommandPalette,
            ),
            AppButton(
              label: l10n.dashboardGeneratePassword,
              icon: Icons.casino_rounded,
              variant: AppButtonVariant.secondary,
              tooltip: 'Ctrl+G',
              onPressed: () => shell.goTo(ShellDestination.generator),
            ),
            AppButton(
              label: l10n.navLockVault,
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
    final l10n = context.l10n;
    final info = vault.fileInfo;

    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          SectionHeader(
            title: l10n.dashboardSecuritySnapshot,
            subtitle: l10n.dashboardSecuritySnapshotSubtitle,
            icon: Icons.shield_outlined,
            trailing: AppButton(
              label: l10n.dashboardSecurityCenter,
              variant: AppButtonVariant.ghost,
              size: AppButtonSize.small,
              onPressed: () => context.read<ShellController>().goTo(
                ShellDestination.security,
              ),
            ),
          ),
          const SizedBox(height: Insets.sm),
          InfoRow(
            label: l10n.infoEncryption,
            value:
                'AES-256-GCM · ${(info?.kdf ?? VaultKdfParams.current).describe()}',
          ),
          InfoRow(
            label: l10n.infoAutoLock,
            value: autoLockMinutes == 0
                ? l10n.infoAutoLockOff
                : l10n.infoAutoLockAfter(autoLockMinutes),
          ),
          InfoRow(
            label: l10n.infoClipboard,
            value: clipboardSeconds == 0
                ? l10n.infoClipboardKeep
                : l10n.infoClipboardClear(clipboardSeconds),
          ),
          InfoRow(
            label: l10n.infoLastSaved,
            value: formatRelativeTime(l10n, vault.lastSavedAt),
          ),
          if (vault.legacyEntries.isNotEmpty) ...<Widget>[
            const SizedBox(height: Insets.sm),
            Text(
              l10n.dashboardLegacyNote(vault.legacyEntries.length),
              style: tokens.text.caption,
            ),
          ],
        ],
      ),
    );
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
