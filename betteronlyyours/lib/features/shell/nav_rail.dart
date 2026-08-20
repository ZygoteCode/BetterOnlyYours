import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import '../../l10n/l10n.dart';
import '../../shared/widgets/hover_builder.dart';
import '../../state/shell_controller.dart';
import '../../state/vault_controller.dart';
import '../vault/vault_actions.dart';

/// Primary navigation. Collapses to icons on narrow windows; every icon keeps
/// its tooltip so the collapsed state stays usable.
class AppNavRail extends StatelessWidget {
  const AppNavRail({super.key, required this.expanded});

  final bool expanded;

  static const double collapsedWidth = 64;
  static const double expandedWidth = 206;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final shell = context.watch<ShellController>();
    final vault = context.watch<VaultController>();
    final l10n = context.l10n;

    final destinations = <_NavItemData>[
      _NavItemData(
        destination: ShellDestination.vault,
        icon: Icons.grid_view_rounded,
        badge: vault.entryCount,
      ),
      _NavItemData(
        destination: ShellDestination.favorites,
        icon: Icons.star_rounded,
        badge: vault.favorites.length,
      ),
      _NavItemData(
        destination: ShellDestination.recent,
        icon: Icons.history_rounded,
        badge: vault.recentEntries.length,
      ),
      _NavItemData(
        destination: ShellDestination.generator,
        icon: Icons.casino_rounded,
      ),
      _NavItemData(
        destination: ShellDestination.security,
        icon: Icons.shield_outlined,
      ),
      _NavItemData(
        destination: ShellDestination.settings,
        icon: Icons.tune_rounded,
      ),
    ];

    return AnimatedContainer(
      duration: context.motion.normal,
      curve: context.motion.standard,
      width: expanded ? expandedWidth : collapsedWidth,
      decoration: BoxDecoration(
        color: palette.backgroundElevated,
        border: Border(right: BorderSide(color: palette.border)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          const SizedBox(height: Insets.md),
          for (final item in destinations)
            Padding(
              padding: const EdgeInsets.symmetric(
                horizontal: Insets.sm,
                vertical: 2,
              ),
              child: _NavItem(
                data: item,
                expanded: expanded,
                selected: shell.destination == item.destination,
                onTap: () => shell.goTo(item.destination!),
              ),
            ),
          const Spacer(),
          Padding(
            padding: const EdgeInsets.symmetric(
              horizontal: Insets.sm,
              vertical: 2,
            ),
            child: _NavItem(
              data: _NavItemData(
                destination: null,
                icon: Icons.lock_outline_rounded,
                label: l10n.navLockVault,
                shortcut: 'Ctrl+L',
              ),
              expanded: expanded,
              selected: false,
              accent: palette.warning,
              onTap: () => VaultActions.lockVault(context),
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(
              Insets.sm,
              2,
              Insets.sm,
              Insets.md,
            ),
            child: _NavItem(
              data: _NavItemData(
                destination: null,
                icon: expanded
                    ? Icons.keyboard_double_arrow_left_rounded
                    : Icons.keyboard_double_arrow_right_rounded,
                label: l10n.navCollapse,
              ),
              expanded: expanded,
              selected: false,
              onTap: shell.toggleSidebar,
            ),
          ),
        ],
      ),
    );
  }
}

class _NavItemData {
  const _NavItemData({
    required this.destination,
    required this.icon,
    this.label,
    this.badge,
    this.shortcut,
  });

  final ShellDestination? destination;
  final IconData icon;
  final String? label;
  final int? badge;
  final String? shortcut;

  String title(AppLocalizations l10n) =>
      label ?? destination?.localizedLabel(l10n) ?? '';
}

class _NavItem extends StatelessWidget {
  const _NavItem({
    required this.data,
    required this.expanded,
    required this.selected,
    required this.onTap,
    this.accent,
  });

  final _NavItemData data;
  final bool expanded;
  final bool selected;
  final VoidCallback onTap;
  final Color? accent;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final color = accent ?? palette.accent;

    final content = HoverBuilder(
      onTap: onTap,
      builder: (context, state) {
        final active = selected || state.active;
        return AnimatedContainer(
          duration: context.motion.fast,
          curve: context.motion.standard,
          height: 40,
          padding: EdgeInsets.symmetric(horizontal: expanded ? Insets.md : 0),
          decoration: BoxDecoration(
            color: selected
                ? color.withValues(alpha: 0.14)
                : (state.active ? palette.surfaceHigh : Colors.transparent),
            borderRadius: Corners.radiusSm,
            border: Border.all(
              color: state.focused
                  ? color.withValues(alpha: 0.7)
                  : Colors.transparent,
            ),
          ),
          child: Row(
            mainAxisAlignment: expanded
                ? MainAxisAlignment.start
                : MainAxisAlignment.center,
            children: <Widget>[
              Icon(
                data.icon,
                size: 18,
                color: selected
                    ? color
                    : (active ? palette.textPrimary : palette.textSecondary),
              ),
              if (expanded) ...<Widget>[
                const SizedBox(width: Insets.md),
                Expanded(
                  child: Text(
                    data.title(context.l10n),
                    overflow: TextOverflow.ellipsis,
                    style: tokens.text.body.copyWith(
                      color: selected ? color : palette.textSecondary,
                      fontWeight: selected ? FontWeight.w600 : FontWeight.w500,
                    ),
                  ),
                ),
                if (data.badge != null && data.badge! > 0)
                  Text('${data.badge}', style: tokens.text.caption),
              ],
            ],
          ),
        );
      },
    );

    if (expanded) {
      return Semantics(button: true, selected: selected, child: content);
    }

    final title = data.title(context.l10n);
    return Tooltip(
      message: data.shortcut == null ? title : '$title  ·  ${data.shortcut}',
      child: Semantics(button: true, selected: selected, child: content),
    );
  }
}
