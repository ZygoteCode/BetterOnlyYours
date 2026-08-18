import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import '../../shared/widgets/app_button.dart';
import '../../state/shell_controller.dart';
import '../../state/vault_controller.dart';
import '../generator/generator_page.dart';
import '../security/security_center_page.dart';
import '../settings/settings_page.dart';
import '../vault/entry_list_panel.dart';
import '../vault/vault_actions.dart';
import '../vault/vault_page.dart';
import 'nav_rail.dart';
import 'status_bar.dart';
import 'title_bar.dart';

/// The unlocked application: title bar, navigation rail, destination content
/// and status footer.
class AppShell extends StatelessWidget {
  const AppShell({super.key, required this.searchFocusNode});

  final FocusNode searchFocusNode;

  @override
  Widget build(BuildContext context) {
    final shell = context.watch<ShellController>();
    final vault = context.watch<VaultController>();
    final palette = context.colors;

    return LayoutBuilder(
      builder: (context, constraints) {
        final railExpanded =
            !shell.sidebarCollapsed &&
            constraints.maxWidth >= Breakpoints.railLabels;

        return Column(
          children: <Widget>[
            AppTitleBar(
              contextLabel: _contextLabel(shell, vault),
              actions: <Widget>[
                AppIconButton(
                  icon: Icons.search_rounded,
                  tooltip: 'Command palette  ·  Ctrl+K',
                  onPressed: shell.openCommandPalette,
                ),
                AppIconButton(
                  icon: Icons.add_rounded,
                  tooltip: 'New entry  ·  Ctrl+N',
                  onPressed: () => VaultActions.createEntry(context),
                ),
                AppIconButton(
                  icon: Icons.lock_outline_rounded,
                  tooltip: 'Lock vault  ·  Ctrl+L',
                  onPressed: () => VaultActions.lockVault(context),
                ),
              ],
            ),
            Expanded(
              child: Row(
                children: <Widget>[
                  AppNavRail(expanded: railExpanded),
                  Expanded(
                    child: ColoredBox(
                      color: palette.background,
                      child: AnimatedSwitcher(
                        duration: context.motion.normal,
                        switchInCurve: context.motion.standard,
                        switchOutCurve: context.motion.exit,
                        transitionBuilder: (child, animation) => FadeTransition(
                          opacity: animation,
                          child: SlideTransition(
                            position: Tween<Offset>(
                              begin: const Offset(0, 0.02),
                              end: Offset.zero,
                            ).animate(animation),
                            child: child,
                          ),
                        ),
                        child: KeyedSubtree(
                          key: ValueKey<ShellDestination>(shell.destination),
                          child: _buildDestination(shell.destination),
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
            const AppStatusBar(),
          ],
        );
      },
    );
  }

  Widget _buildDestination(ShellDestination destination) {
    switch (destination) {
      case ShellDestination.vault:
        return VaultPage(
          mode: VaultViewMode.all,
          searchFocusNode: searchFocusNode,
        );
      case ShellDestination.favorites:
        return VaultPage(
          mode: VaultViewMode.favorites,
          searchFocusNode: searchFocusNode,
        );
      case ShellDestination.recent:
        return VaultPage(
          mode: VaultViewMode.recent,
          searchFocusNode: searchFocusNode,
        );
      case ShellDestination.generator:
        return const GeneratorPage();
      case ShellDestination.security:
        return const SecurityCenterPage();
      case ShellDestination.settings:
        return const SettingsPage();
    }
  }

  String _contextLabel(ShellController shell, VaultController vault) {
    final selected = vault.selectedEntry;
    final destination = shell.destination.label;
    if (selected != null &&
        (shell.destination == ShellDestination.vault ||
            shell.destination == ShellDestination.favorites ||
            shell.destination == ShellDestination.recent)) {
      return '$destination  ›  ${selected.title}';
    }
    return destination;
  }
}
