import 'package:material_ui/material_ui.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import '../../l10n/l10n.dart';
import '../../core/models/vault_entry.dart';
import '../../core/services/fuzzy_search.dart';
import '../../shared/animations/entrance.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_menu.dart';
import '../../shared/widgets/app_text_field.dart';
import '../../shared/widgets/empty_state.dart';
import '../../shared/widgets/entry_avatar.dart';
import '../../shared/widgets/highlighted_text.dart';
import '../../shared/widgets/hover_builder.dart';
import '../../shared/widgets/tag_chip.dart';
import '../../state/vault_controller.dart';
import 'vault_actions.dart';

enum VaultViewMode { all, favorites, recent }

/// Browsable list of entries with search, filters, sorting and keyboard
/// navigation.
class EntryListPanel extends StatefulWidget {
  const EntryListPanel({
    super.key,
    required this.mode,
    required this.searchFocusNode,
    this.showHeader = true,
  });

  final VaultViewMode mode;
  final FocusNode searchFocusNode;
  final bool showHeader;

  @override
  State<EntryListPanel> createState() => _EntryListPanelState();
}

class _EntryListPanelState extends State<EntryListPanel> {
  final TextEditingController _search = TextEditingController();
  final ScrollController _scroll = ScrollController();

  @override
  void initState() {
    super.initState();
    _search.text = context.read<VaultController>().query;
  }

  @override
  void dispose() {
    _search.dispose();
    _scroll.dispose();
    super.dispose();
  }

  List<VaultEntry> _entriesFor(VaultController vault) {
    switch (widget.mode) {
      case VaultViewMode.all:
        return vault.visibleEntries;
      case VaultViewMode.favorites:
        final favorites = vault.favorites;
        if (vault.query.trim().isEmpty) return favorites;
        return vault
            .searchEntries(vault.query, applyFilters: false)
            .where((e) => e.favorite)
            .toList();
      case VaultViewMode.recent:
        final recent = vault.recentEntries;
        if (vault.query.trim().isEmpty) return recent;
        final matches = vault
            .searchEntries(vault.query, applyFilters: false)
            .map((e) => e.title)
            .toSet();
        return recent.where((e) => matches.contains(e.title)).toList();
    }
  }

  void _moveSelection(int delta, List<VaultEntry> entries) {
    if (entries.isEmpty) return;
    final vault = context.read<VaultController>();
    final currentIndex = entries.indexWhere(
      (e) => e.title == vault.selectedTitle,
    );
    final nextIndex = currentIndex < 0
        ? (delta > 0 ? 0 : entries.length - 1)
        : (currentIndex + delta).clamp(0, entries.length - 1);
    vault.select(entries[nextIndex].title);
    _scrollTo(nextIndex);
  }

  void _scrollTo(int index) {
    if (!_scroll.hasClients) return;
    final extent = context.tokens.rowHeight + Insets.xs;
    final target = index * extent;
    final position = _scroll.position;
    final viewStart = position.pixels;
    final viewEnd = viewStart + position.viewportDimension - extent;

    if (target < viewStart || target > viewEnd) {
      final destination = target < viewStart
          ? target
          : target - position.viewportDimension + extent;
      _scroll.animateTo(
        destination.clamp(0.0, position.maxScrollExtent),
        duration: context.motion.fast,
        curve: context.motion.standard,
      );
    }
  }

  KeyEventResult _handleSearchKey(
    FocusNode node,
    KeyEvent event,
    List<VaultEntry> entries,
  ) {
    if (event is! KeyDownEvent && event is! KeyRepeatEvent) {
      return KeyEventResult.ignored;
    }
    final vault = context.read<VaultController>();
    switch (event.logicalKey) {
      case LogicalKeyboardKey.arrowDown:
        _moveSelection(1, entries);
        return KeyEventResult.handled;
      case LogicalKeyboardKey.arrowUp:
        _moveSelection(-1, entries);
        return KeyEventResult.handled;
      case LogicalKeyboardKey.enter:
      case LogicalKeyboardKey.numpadEnter:
        final selected =
            vault.selectedTitle ??
            (entries.isEmpty ? null : entries.first.title);
        if (selected != null) {
          VaultActions.openEntry(context, selected);
          return KeyEventResult.handled;
        }
        return KeyEventResult.ignored;
      case LogicalKeyboardKey.escape:
        if (_search.text.isNotEmpty) {
          _search.clear();
          vault.setQuery('');
          return KeyEventResult.handled;
        }
        return KeyEventResult.ignored;
      default:
        return KeyEventResult.ignored;
    }
  }

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final vault = context.watch<VaultController>();
    final entries = _entriesFor(vault);

    // The query can also be cleared from elsewhere (filters, palette). Sync
    // after the frame: mutating the controller during build would rebuild the
    // text field while it is already building.
    if (_search.text != vault.query) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted) return;
        final current = context.read<VaultController>().query;
        if (_search.text == current) return;
        _search.value = TextEditingValue(
          text: current,
          selection: TextSelection.collapsed(offset: current.length),
        );
      });
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        if (widget.showHeader) _buildHeader(context, vault, entries),
        Expanded(
          child: entries.isEmpty
              ? _buildEmptyState(context, vault)
              : Scrollbar(
                  controller: _scroll,
                  child: ListView.builder(
                    controller: _scroll,
                    padding: const EdgeInsets.fromLTRB(
                      Insets.sm,
                      Insets.xs,
                      Insets.sm,
                      Insets.md,
                    ),
                    itemExtent: tokens.rowHeight + Insets.xs,
                    itemCount: entries.length,
                    itemBuilder: (context, index) {
                      final entry = entries[index];
                      return EntranceFade(
                        key: ValueKey<String>(entry.title),
                        delay: Duration(
                          milliseconds: index < 10 ? 14 * index : 0,
                        ),
                        offset: const Offset(0, 0.06),
                        child: _EntryRow(
                          entry: entry,
                          selected: vault.selectedTitle == entry.title,
                          query: vault.query,
                          lastUsed: vault.lastUsedAt(entry.title),
                          onTap: () =>
                              VaultActions.openEntry(context, entry.title),
                        ),
                      );
                    },
                  ),
                ),
        ),
        _buildFooter(context, vault, entries.length),
      ],
    );
  }

  Widget _buildHeader(
    BuildContext context,
    VaultController vault,
    List<VaultEntry> entries,
  ) {
    final tokens = context.tokens;
    final l10n = context.l10n;
    final palette = tokens.color;
    final tags = vault.allTags;

    return Container(
      padding: const EdgeInsets.fromLTRB(
        Insets.md,
        Insets.md,
        Insets.md,
        Insets.sm,
      ),
      decoration: BoxDecoration(
        border: Border(bottom: BorderSide(color: palette.border)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Focus(
                  onKeyEvent: (node, event) =>
                      _handleSearchKey(node, event, entries),
                  child: AppTextField(
                    controller: _search,
                    focusNode: widget.searchFocusNode,
                    hint: l10n.listSearchHint,
                    prefixIcon: Icons.search_rounded,
                    semanticLabel: l10n.listSearchSemantics,
                    onChanged: vault.setQuery,
                    suffix: _search.text.isEmpty
                        ? null
                        : AppIconButton(
                            icon: Icons.close_rounded,
                            tooltip: l10n.listClearSearch,
                            dense: true,
                            size: 14,
                            onPressed: () {
                              _search.clear();
                              vault.setQuery('');
                            },
                          ),
                  ),
                ),
              ),
              const SizedBox(width: Insets.sm),
              Builder(
                builder: (buttonContext) => AppIconButton(
                  icon: Icons.sort_rounded,
                  tooltip: l10n.listSortTooltip,
                  onPressed: () => _openSortMenu(buttonContext, vault),
                ),
              ),
              AppIconButton(
                icon: Icons.add_rounded,
                tooltip: l10n.actionNewEntryTooltip,
                onPressed: () => VaultActions.createEntry(context),
              ),
            ],
          ),
          if (widget.mode == VaultViewMode.all &&
              (tags.isNotEmpty || vault.favoritesOnly)) ...<Widget>[
            const SizedBox(height: Insets.sm),
            SizedBox(
              height: 28,
              child: ListView(
                scrollDirection: Axis.horizontal,
                children: <Widget>[
                  TagChip(
                    label: l10n.navFavorites,
                    icon: Icons.star_rounded,
                    selected: vault.favoritesOnly,
                    onTap: () => vault.setFavoritesOnly(!vault.favoritesOnly),
                  ),
                  for (final tag in tags) ...<Widget>[
                    const SizedBox(width: Insets.xs + 2),
                    TagChip(
                      label: tag,
                      count: vault.tagCounts[tag],
                      selected: vault.tagFilter == tag,
                      onTap: () => vault.setTagFilter(
                        vault.tagFilter == tag ? null : tag,
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildFooter(
    BuildContext context,
    VaultController vault,
    int shownCount,
  ) {
    final tokens = context.tokens;
    final l10n = context.l10n;
    final palette = tokens.color;
    final total = switch (widget.mode) {
      VaultViewMode.all => vault.entryCount,
      VaultViewMode.favorites => vault.favorites.length,
      VaultViewMode.recent => vault.recentEntries.length,
    };

    return Container(
      height: 34,
      padding: const EdgeInsets.symmetric(horizontal: Insets.md),
      decoration: BoxDecoration(
        border: Border(top: BorderSide(color: palette.border)),
      ),
      child: Row(
        children: <Widget>[
          Text(
            shownCount == total
                ? l10n.entriesCount(total)
                : l10n.listShownOfTotal(shownCount, total),
            style: tokens.text.caption,
          ),
          const Spacer(),
          if (vault.query.isNotEmpty ||
              vault.tagFilter != null ||
              vault.favoritesOnly)
            HoverBuilder(
              onTap: () {
                _search.clear();
                vault.clearFilters();
              },
              builder: (context, state) => Text(
                l10n.listClearFilters,
                style: tokens.text.caption.copyWith(
                  color: state.hovered ? palette.accent : palette.textTertiary,
                ),
              ),
            ),
        ],
      ),
    );
  }

  Widget _buildEmptyState(BuildContext context, VaultController vault) {
    final l10n = context.l10n;
    final searching =
        vault.query.trim().isNotEmpty ||
        vault.tagFilter != null ||
        vault.favoritesOnly;

    if (searching) {
      return EmptyState(
        compact: true,
        icon: Icons.search_off_rounded,
        title: l10n.emptySearchTitle,
        message: l10n.emptySearchMessage,
        actionLabel: l10n.emptySearchCreate(vault.query.trim()),
        onAction: vault.query.trim().isEmpty
            ? null
            : () => VaultActions.createEntry(
                context,
                initialTitle: vault.query.trim(),
              ),
        secondaryActionLabel: l10n.listClearFilters,
        onSecondaryAction: () {
          _search.clear();
          vault.clearFilters();
        },
      );
    }

    return switch (widget.mode) {
      VaultViewMode.favorites => EmptyState(
        compact: true,
        icon: Icons.star_outline_rounded,
        title: l10n.emptyFavoritesTitle,
        message: l10n.emptyFavoritesMessage,
        hint: l10n.emptyFavoritesHint,
      ),
      VaultViewMode.recent => EmptyState(
        compact: true,
        icon: Icons.history_rounded,
        title: l10n.emptyRecentTitle,
        message: l10n.emptyRecentMessage,
      ),
      VaultViewMode.all => EmptyState(
        compact: true,
        icon: Icons.lock_open_rounded,
        title: l10n.emptyVaultTitle,
        message: l10n.emptyVaultMessage,
        actionLabel: l10n.newEntry,
        onAction: () => VaultActions.createEntry(context),
        hint: l10n.emptyVaultHint,
      ),
    };
  }

  Future<void> _openSortMenu(
    BuildContext buttonContext,
    VaultController vault,
  ) async {
    final l10n = buttonContext.l10n;
    final box = buttonContext.findRenderObject() as RenderBox?;
    final position = box == null
        ? Offset.zero
        : box.localToGlobal(Offset(0, box.size.height + 4));

    await showAppMenu(
      context: buttonContext,
      globalPosition: position,
      entries: <AppMenuEntry?>[
        for (final sort in EntrySort.values)
          AppMenuEntry(
            label: sort.localizedLabel(l10n),
            checked: vault.sort == sort,
            onSelected: () => vault.setSort(sort),
          ),
        null,
        AppMenuEntry(
          label: l10n.listFavoritesOnly,
          checked: vault.favoritesOnly,
          onSelected: () => vault.setFavoritesOnly(!vault.favoritesOnly),
        ),
        if (vault.tagFilter != null)
          AppMenuEntry(
            label: l10n.listClearTagFilter(vault.tagFilter!),
            icon: Icons.filter_alt_off_rounded,
            onSelected: () => vault.setTagFilter(null),
          ),
      ],
    );
  }
}

class _EntryRow extends StatelessWidget {
  const _EntryRow({
    required this.entry,
    required this.selected,
    required this.query,
    required this.onTap,
    this.lastUsed,
  });

  final VaultEntry entry;
  final bool selected;
  final String query;
  final VoidCallback onTap;
  final DateTime? lastUsed;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final l10n = context.l10n;
    final match = query.trim().isEmpty
        ? null
        : FuzzySearch.match(query, entry.title);
    final accent = EntryAvatar.accentFor(context, entry.title);

    return Padding(
      padding: const EdgeInsets.only(bottom: Insets.xs),
      child: HoverBuilder(
        onTap: onTap,
        onSecondaryTapDown: (details) => showAppMenu(
          context: context,
          globalPosition: details.globalPosition,
          entries: VaultActions.contextMenu(context, entry),
        ),
        builder: (context, state) {
          return AnimatedContainer(
            duration: context.motion.fast,
            curve: context.motion.standard,
            height: tokens.rowHeight,
            padding: const EdgeInsets.symmetric(horizontal: Insets.sm),
            decoration: BoxDecoration(
              color: selected
                  ? palette.accent.withValues(alpha: 0.12)
                  : (state.active ? palette.surfaceHigh : Colors.transparent),
              borderRadius: Corners.radiusSm,
              border: Border.all(
                color: selected
                    ? palette.accent.withValues(alpha: 0.42)
                    : (state.focused
                          ? palette.borderStrong
                          : Colors.transparent),
              ),
            ),
            child: Row(
              children: <Widget>[
                if (selected)
                  Container(
                    width: 2,
                    height: 20,
                    margin: const EdgeInsets.only(right: Insets.sm),
                    decoration: BoxDecoration(
                      color: accent,
                      borderRadius: Corners.radiusXs,
                    ),
                  ),
                EntryAvatar(
                  entry: entry,
                  size: tokens.isCompact ? 28 : 32,
                  selected: selected,
                ),
                const SizedBox(width: Insets.md),
                Expanded(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Row(
                        children: <Widget>[
                          Flexible(
                            child: match == null
                                ? Text(
                                    entry.title,
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis,
                                    style: tokens.text.body.copyWith(
                                      fontWeight: selected
                                          ? FontWeight.w600
                                          : FontWeight.w500,
                                      color: palette.textPrimary,
                                    ),
                                  )
                                : HighlightedText(
                                    text: entry.title,
                                    positions: match.positions,
                                    highlightColor: palette.accent,
                                    style: tokens.text.body.copyWith(
                                      fontWeight: selected
                                          ? FontWeight.w600
                                          : FontWeight.w500,
                                    ),
                                  ),
                          ),
                          if (entry.favorite) ...<Widget>[
                            const SizedBox(width: Insets.xs + 2),
                            Icon(
                              Icons.star_rounded,
                              size: 12,
                              color: palette.warning,
                            ),
                          ],
                          if (entry.hasTotp) ...<Widget>[
                            const SizedBox(width: Insets.xs + 2),
                            Tooltip(
                              message: context.l10n.totpBadgeTooltip,
                              child: Icon(
                                Icons.shield_rounded,
                                size: 12,
                                color: palette.accent,
                              ),
                            ),
                          ],
                        ],
                      ),
                      if (!tokens.isCompact) ...<Widget>[
                        const SizedBox(height: 1),
                        Text(
                          entry.subtitle,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: tokens.text.caption,
                        ),
                      ],
                    ],
                  ),
                ),
                AnimatedOpacity(
                  duration: context.motion.fast,
                  opacity: state.active ? 1 : 0,
                  child: state.active
                      ? Row(
                          mainAxisSize: MainAxisSize.min,
                          children: <Widget>[
                            if (entry.username.isNotEmpty)
                              AppIconButton(
                                icon: Icons.person_outline_rounded,
                                tooltip: l10n.menuCopyUsername,
                                dense: true,
                                size: 15,
                                onPressed: () =>
                                    VaultActions.copyUsername(context, entry),
                              ),
                            if (entry.password.isNotEmpty)
                              AppIconButton(
                                icon: Icons.key_rounded,
                                tooltip: l10n.menuCopyPassword,
                                dense: true,
                                size: 15,
                                onPressed: () =>
                                    VaultActions.copyPassword(context, entry),
                              ),
                            Builder(
                              builder: (menuContext) => AppIconButton(
                                icon: Icons.more_horiz_rounded,
                                tooltip: l10n.listEntryActions,
                                dense: true,
                                size: 16,
                                onPressed: () {
                                  final box =
                                      menuContext.findRenderObject()
                                          as RenderBox?;
                                  Offset? anchor;
                                  if (box != null) {
                                    anchor = box.localToGlobal(
                                      Offset(0, box.size.height),
                                    );
                                  }
                                  showAppMenu(
                                    context: menuContext,
                                    globalPosition: anchor,
                                    entries: VaultActions.contextMenu(
                                      context,
                                      entry,
                                    ),
                                  );
                                },
                              ),
                            ),
                          ],
                        )
                      : const SizedBox.shrink(),
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}
