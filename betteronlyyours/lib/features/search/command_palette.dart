import 'dart:ui';

import 'package:material_ui/material_ui.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import '../../l10n/l10n.dart';
import '../../core/models/vault_entry.dart';
import '../../core/services/fuzzy_search.dart';
import '../../shared/widgets/app_menu.dart';
import '../../shared/widgets/entry_avatar.dart';
import '../../shared/widgets/highlighted_text.dart';
import '../../shared/widgets/hover_builder.dart';
import '../../state/shell_controller.dart';
import '../../state/vault_controller.dart';
import '../vault/vault_actions.dart';

/// Command palette: fuzzy search across entries plus the app's commands.
///
/// Rendered as an overlay inside the shell, so it never depends on a global
/// navigator or a static widget-state reference.
class CommandPalette extends StatefulWidget {
  const CommandPalette({super.key});

  @override
  State<CommandPalette> createState() => _CommandPaletteState();
}

class _CommandPaletteState extends State<CommandPalette> {
  final TextEditingController _query = TextEditingController();
  final FocusNode _focus = FocusNode();
  final ScrollController _scroll = ScrollController();

  int _selectedIndex = 0;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) _focus.requestFocus();
    });
  }

  @override
  void dispose() {
    _query.dispose();
    _focus.dispose();
    _scroll.dispose();
    super.dispose();
  }

  void _close() => context.read<ShellController>().closeCommandPalette();

  List<_PaletteItem> _buildItems(BuildContext context, VaultController vault) {
    final l10n = context.l10n;
    final query = _query.text.trim();
    final items = <_PaletteItem>[];

    final entries = query.isEmpty
        ? <VaultEntry>[
            ...vault.recentEntries.take(5),
            ...vault.favorites.where(
              (e) => !vault.recentEntries.take(5).contains(e),
            ),
          ].take(7).toList()
        : vault.searchEntries(query, applyFilters: false, limit: 8);

    for (final entry in entries) {
      items.add(_PaletteItem.entry(entry));
    }

    final commands = <_PaletteItem>[
      _PaletteItem.command(
        title: l10n.newEntry,
        subtitle: l10n.paletteCommandNewEntry,
        icon: Icons.add_rounded,
        shortcut: const <String>['Ctrl', 'N'],
        run: (context) => VaultActions.createEntry(context),
      ),
      _PaletteItem.command(
        title: l10n.generatorTitle,
        subtitle: l10n.paletteCommandGenerator,
        icon: Icons.casino_rounded,
        shortcut: const <String>['Ctrl', 'G'],
        run: (context) =>
            context.read<ShellController>().goTo(ShellDestination.generator),
      ),
      if (vault.selectedEntry?.hasTotp ?? false)
        _PaletteItem.command(
          title: l10n.menuCopyTotp,
          subtitle: l10n.paletteCommandCopyTotp,
          icon: Icons.shield_outlined,
          shortcut: const <String>['Ctrl', 'Shift', 'T'],
          run: (context) =>
              VaultActions.copyTotp(context, vault.selectedEntry!),
        ),
      _PaletteItem.command(
        title: l10n.navLockVault,
        subtitle: l10n.paletteCommandLock,
        icon: Icons.lock_outline_rounded,
        shortcut: const <String>['Ctrl', 'L'],
        run: (context) => VaultActions.lockVault(context),
      ),
      _PaletteItem.command(
        title: l10n.navFavorites,
        subtitle: l10n.paletteCommandFavorites,
        icon: Icons.star_rounded,
        run: (context) =>
            context.read<ShellController>().goTo(ShellDestination.favorites),
      ),
      _PaletteItem.command(
        title: l10n.navRecent,
        subtitle: l10n.paletteCommandRecent,
        icon: Icons.history_rounded,
        run: (context) =>
            context.read<ShellController>().goTo(ShellDestination.recent),
      ),
      _PaletteItem.command(
        title: l10n.dashboardSecurityCenter,
        subtitle: l10n.paletteCommandSecurity,
        icon: Icons.shield_outlined,
        run: (context) =>
            context.read<ShellController>().goTo(ShellDestination.security),
      ),
      _PaletteItem.command(
        title: l10n.navSettings,
        subtitle: l10n.paletteCommandSettings,
        icon: Icons.tune_rounded,
        shortcut: const <String>['Ctrl', ','],
        run: (context) =>
            context.read<ShellController>().goTo(ShellDestination.settings),
      ),
    ];

    if (query.isEmpty) {
      items.addAll(commands.take(4));
    } else {
      final scored = <({_PaletteItem item, int score})>[];
      for (final command in commands) {
        final match = FuzzySearch.match(query, command.title);
        if (match != null) scored.add((item: command, score: match.score));
      }
      scored.sort((a, b) => b.score.compareTo(a.score));
      items.addAll(scored.map((s) => s.item));
    }

    return items;
  }

  void _move(int delta, int length) {
    if (length == 0) return;
    setState(() {
      _selectedIndex = (_selectedIndex + delta) % length;
      if (_selectedIndex < 0) _selectedIndex += length;
    });
    _scrollToSelection();
  }

  void _scrollToSelection() {
    if (!_scroll.hasClients) return;
    const rowHeight = 52.0;
    final target = _selectedIndex * rowHeight;
    final position = _scroll.position;
    if (target < position.pixels ||
        target > position.pixels + position.viewportDimension - rowHeight) {
      _scroll.animateTo(
        (target - position.viewportDimension / 2 + rowHeight).clamp(
          0.0,
          position.maxScrollExtent,
        ),
        duration: context.motion.fast,
        curve: context.motion.standard,
      );
    }
  }

  KeyEventResult _onKey(KeyEvent event, List<_PaletteItem> items) {
    if (event is! KeyDownEvent && event is! KeyRepeatEvent) {
      return KeyEventResult.ignored;
    }
    final pressed = HardwareKeyboard.instance;

    switch (event.logicalKey) {
      case LogicalKeyboardKey.arrowDown:
        _move(1, items.length);
        return KeyEventResult.handled;
      case LogicalKeyboardKey.arrowUp:
        _move(-1, items.length);
        return KeyEventResult.handled;
      case LogicalKeyboardKey.escape:
        _close();
        return KeyEventResult.handled;
      case LogicalKeyboardKey.enter:
      case LogicalKeyboardKey.numpadEnter:
        if (items.isEmpty) return KeyEventResult.handled;
        final item = items[_selectedIndex.clamp(0, items.length - 1)];
        if (pressed.isControlPressed) {
          _runSecondary(item, copyPassword: true);
        } else if (pressed.isAltPressed) {
          _runSecondary(item, copyPassword: false);
        } else {
          _run(item);
        }
        return KeyEventResult.handled;
      default:
        return KeyEventResult.ignored;
    }
  }

  void _run(_PaletteItem item) {
    _close();
    final entry = item.entry;
    if (entry != null) {
      VaultActions.openEntry(context, entry.title);
    } else {
      item.run?.call(context);
    }
  }

  void _runSecondary(_PaletteItem item, {required bool copyPassword}) {
    final entry = item.entry;
    if (entry == null) {
      _run(item);
      return;
    }
    _close();
    if (copyPassword) {
      VaultActions.copyPassword(context, entry);
    } else {
      VaultActions.copyUsername(context, entry);
    }
  }

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final vault = context.watch<VaultController>();
    final items = _buildItems(context, vault);
    if (_selectedIndex >= items.length) {
      _selectedIndex = items.isEmpty ? 0 : items.length - 1;
    }

    return Positioned.fill(
      child: Stack(
        children: <Widget>[
          Positioned.fill(
            child: GestureDetector(
              behavior: HitTestBehavior.opaque,
              onTap: _close,
              child: BackdropFilter(
                filter: ImageFilter.blur(sigmaX: 8, sigmaY: 8),
                child: ColoredBox(
                  color: Colors.black.withValues(alpha: 0.45),
                  child: const SizedBox.expand(),
                ),
              ),
            ),
          ),
          Align(
            alignment: const Alignment(0, -0.55),
            child: TweenAnimationBuilder<double>(
              tween: Tween<double>(begin: 0, end: 1),
              duration: context.motion.normal,
              curve: context.motion.emphasized,
              builder: (context, value, child) => Opacity(
                opacity: value,
                child: Transform.translate(
                  offset: Offset(0, (1 - value) * -12),
                  child: Transform.scale(
                    scale: 0.98 + 0.02 * value,
                    child: child,
                  ),
                ),
              ),
              child: Material(
                color: Colors.transparent,
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 660),
                  child: Container(
                    decoration: BoxDecoration(
                      color: palette.overlay,
                      borderRadius: Corners.radiusLg,
                      border: Border.all(color: palette.borderStrong),
                      boxShadow: tokens.overlayShadow,
                    ),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: <Widget>[
                        _buildInput(context, items),
                        if (items.isNotEmpty)
                          Container(height: 1, color: palette.border),
                        Flexible(
                          child: items.isEmpty
                              ? _buildNoResults(context)
                              : ConstrainedBox(
                                  constraints: const BoxConstraints(
                                    maxHeight: 380,
                                  ),
                                  child: ListView.builder(
                                    controller: _scroll,
                                    shrinkWrap: true,
                                    padding: const EdgeInsets.symmetric(
                                      vertical: Insets.sm,
                                    ),
                                    itemCount: items.length,
                                    itemBuilder: (context, index) =>
                                        _PaletteRow(
                                          item: items[index],
                                          query: _query.text,
                                          selected: index == _selectedIndex,
                                          onHover: () => setState(
                                            () => _selectedIndex = index,
                                          ),
                                          onTap: () => _run(items[index]),
                                        ),
                                  ),
                                ),
                        ),
                        _buildFooter(context),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildInput(BuildContext context, List<_PaletteItem> items) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final l10n = context.l10n;

    return Focus(
      onKeyEvent: (node, event) => _onKey(event, items),
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: Insets.lg,
          vertical: Insets.xs,
        ),
        child: Row(
          children: <Widget>[
            Icon(Icons.search_rounded, size: 20, color: palette.accent),
            const SizedBox(width: Insets.md),
            Expanded(
              child: TextField(
                controller: _query,
                focusNode: _focus,
                style: tokens.text.body.copyWith(fontSize: 16),
                cursorColor: palette.accent,
                cursorWidth: 1.6,
                onChanged: (_) => setState(() => _selectedIndex = 0),
                decoration: InputDecoration(
                  border: InputBorder.none,
                  hintText: l10n.paletteHint,
                  hintStyle: tokens.text.body.copyWith(
                    fontSize: 16,
                    color: palette.textTertiary,
                  ),
                  contentPadding: const EdgeInsets.symmetric(
                    vertical: Insets.lg,
                  ),
                ),
              ),
            ),
            const ShortcutHint(keys: <String>['Esc']),
          ],
        ),
      ),
    );
  }

  Widget _buildNoResults(BuildContext context) {
    final tokens = context.tokens;
    final l10n = context.l10n;
    final query = _query.text.trim();
    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: Insets.lg,
        vertical: Insets.xxl,
      ),
      child: Column(
        children: <Widget>[
          Text(l10n.paletteNoMatches(query), style: tokens.text.body),
          const SizedBox(height: Insets.sm),
          HoverBuilder(
            onTap: () {
              _close();
              VaultActions.createEntry(context, initialTitle: query);
            },
            builder: (context, state) => Text(
              l10n.paletteCreateNamed(query),
              style: tokens.text.secondary.copyWith(
                color: state.hovered
                    ? tokens.color.accent
                    : tokens.color.textSecondary,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildFooter(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final l10n = context.l10n;

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: Insets.lg,
        vertical: Insets.sm,
      ),
      decoration: BoxDecoration(
        color: palette.surface.withValues(alpha: 0.6),
        border: Border(top: BorderSide(color: palette.border)),
        borderRadius: const BorderRadius.vertical(
          bottom: Radius.circular(Corners.lg),
        ),
      ),
      child: Wrap(
        spacing: Insets.lg,
        runSpacing: Insets.xs,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: <Widget>[
          _FooterHint(
            keys: const <String>['↑', '↓'],
            label: l10n.paletteNavigate,
          ),
          _FooterHint(keys: const <String>['Enter'], label: l10n.paletteOpen),
          _FooterHint(
            keys: const <String>['Ctrl', 'Enter'],
            label: l10n.paletteCopyPassword,
          ),
          _FooterHint(
            keys: const <String>['Alt', 'Enter'],
            label: l10n.paletteCopyUsername,
          ),
        ],
      ),
    );
  }
}

class _FooterHint extends StatelessWidget {
  const _FooterHint({required this.keys, required this.label});

  final List<String> keys;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        ShortcutHint(keys: keys),
        const SizedBox(width: Insets.sm),
        Text(label, style: context.tokens.text.caption),
      ],
    );
  }
}

class _PaletteItem {
  const _PaletteItem._({
    required this.title,
    required this.icon,
    this.subtitle,
    this.entry,
    this.run,
    this.shortcut,
  });

  factory _PaletteItem.entry(VaultEntry entry) => _PaletteItem._(
    title: entry.title,
    subtitle: entry.subtitle,
    icon: Icons.vpn_key_outlined,
    entry: entry,
  );

  factory _PaletteItem.command({
    required String title,
    required IconData icon,
    String? subtitle,
    List<String>? shortcut,
    void Function(BuildContext context)? run,
  }) => _PaletteItem._(
    title: title,
    subtitle: subtitle,
    icon: icon,
    shortcut: shortcut,
    run: run,
  );

  final String title;
  final String? subtitle;
  final IconData icon;
  final VaultEntry? entry;
  final void Function(BuildContext context)? run;
  final List<String>? shortcut;
}

class _PaletteRow extends StatelessWidget {
  const _PaletteRow({
    required this.item,
    required this.query,
    required this.selected,
    required this.onTap,
    required this.onHover,
  });

  final _PaletteItem item;
  final String query;
  final bool selected;
  final VoidCallback onTap;
  final VoidCallback onHover;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final l10n = context.l10n;
    final match = query.trim().isEmpty
        ? null
        : FuzzySearch.match(query, item.title);

    return MouseRegion(
      onEnter: (_) => onHover(),
      child: HoverBuilder(
        onTap: onTap,
        canRequestFocus: false,
        builder: (context, state) => AnimatedContainer(
          duration: context.motion.instant,
          height: 52,
          margin: const EdgeInsets.symmetric(
            horizontal: Insets.sm,
            vertical: 1,
          ),
          padding: const EdgeInsets.symmetric(horizontal: Insets.md),
          decoration: BoxDecoration(
            color: selected
                ? palette.accent.withValues(alpha: 0.14)
                : Colors.transparent,
            borderRadius: Corners.radiusSm,
            border: Border.all(
              color: selected
                  ? palette.accent.withValues(alpha: 0.35)
                  : Colors.transparent,
            ),
          ),
          child: Row(
            children: <Widget>[
              if (item.entry != null)
                EntryAvatar(entry: item.entry!, size: 28, selected: selected)
              else
                Container(
                  width: 28,
                  height: 28,
                  decoration: BoxDecoration(
                    color: palette.surfaceHigh,
                    borderRadius: Corners.radiusSm,
                  ),
                  child: Icon(
                    item.icon,
                    size: 15,
                    color: selected ? palette.accent : palette.textSecondary,
                  ),
                ),
              const SizedBox(width: Insets.md),
              Expanded(
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    match == null
                        ? Text(
                            item.title,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: tokens.text.body,
                          )
                        : HighlightedText(
                            text: item.title,
                            positions: match.positions,
                            highlightColor: palette.accent,
                            style: tokens.text.body,
                          ),
                    if (item.subtitle != null)
                      Text(
                        item.subtitle!,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: tokens.text.caption,
                      ),
                  ],
                ),
              ),
              if (item.shortcut != null)
                ShortcutHint(keys: item.shortcut!)
              else if (selected && item.entry != null)
                Text(l10n.paletteEnterToOpen, style: tokens.text.caption),
            ],
          ),
        ),
      ),
    );
  }
}
