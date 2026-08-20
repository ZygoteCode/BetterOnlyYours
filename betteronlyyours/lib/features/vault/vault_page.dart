import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import '../../l10n/l10n.dart';
import '../../core/models/app_settings.dart';
import '../../shared/widgets/empty_state.dart';
import '../../state/settings_controller.dart';
import '../../state/shell_controller.dart';
import '../../state/vault_controller.dart';
import 'entry_detail_panel.dart';
import 'entry_inspector.dart';
import 'entry_list_panel.dart';
import 'vault_actions.dart';
import 'vault_dashboard.dart';

/// Two- or three-pane vault browser that folds into a single pane on narrow
/// windows.
class VaultPage extends StatelessWidget {
  const VaultPage({
    super.key,
    required this.mode,
    required this.searchFocusNode,
  });

  final VaultViewMode mode;
  final FocusNode searchFocusNode;

  @override
  Widget build(BuildContext context) {
    final vault = context.watch<VaultController>();
    final shell = context.watch<ShellController>();
    final settings = context.watch<SettingsController>();
    final palette = context.colors;
    final selected = vault.selectedEntry;

    return LayoutBuilder(
      builder: (context, constraints) {
        final singlePane = constraints.maxWidth < Breakpoints.singlePane;
        final showInspector =
            constraints.maxWidth >= Breakpoints.inspector && selected != null;

        if (singlePane) {
          final showDetail = shell.detailExpandedOnCompact && selected != null;
          return AnimatedSwitcher(
            duration: context.motion.normal,
            switchInCurve: context.motion.standard,
            switchOutCurve: context.motion.exit,
            transitionBuilder: (child, animation) => FadeTransition(
              opacity: animation,
              child: SlideTransition(
                position: Tween<Offset>(
                  begin: Offset(showDetail ? 0.05 : -0.05, 0),
                  end: Offset.zero,
                ).animate(animation),
                child: child,
              ),
            ),
            child: showDetail
                ? EntryDetailPanel(
                    key: const ValueKey<String>('detail'),
                    entry: selected,
                    showBackButton: true,
                    showMetadata: true,
                  )
                : KeyedSubtree(
                    key: const ValueKey<String>('list'),
                    child: EntryListPanel(
                      mode: mode,
                      searchFocusNode: searchFocusNode,
                    ),
                  ),
          );
        }

        final sidebarWidth = settings.settings.sidebarWidth.clamp(
          AppSettings.minSidebarWidth,
          constraints.maxWidth * 0.45,
        );

        return Row(
          children: <Widget>[
            SizedBox(
              width: sidebarWidth,
              child: DecoratedBox(
                decoration: BoxDecoration(color: palette.backgroundElevated),
                child: EntryListPanel(
                  mode: mode,
                  searchFocusNode: searchFocusNode,
                ),
              ),
            ),
            _ResizeHandle(
              onDelta: (delta) => settings.setSidebarWidth(
                (settings.settings.sidebarWidth + delta).clamp(
                  AppSettings.minSidebarWidth,
                  AppSettings.maxSidebarWidth,
                ),
              ),
            ),
            Expanded(
              child: AnimatedSwitcher(
                duration: context.motion.normal,
                switchInCurve: context.motion.standard,
                switchOutCurve: context.motion.exit,
                child: selected == null
                    ? KeyedSubtree(
                        key: ValueKey<String>('empty-${mode.name}'),
                        child: _buildNoSelection(context, mode),
                      )
                    : EntryDetailPanel(
                        key: const ValueKey<String>('detail'),
                        entry: selected,
                        showMetadata: !showInspector,
                      ),
              ),
            ),
            if (showInspector) EntryInspector(entry: selected),
          ],
        );
      },
    );
  }

  Widget _buildNoSelection(BuildContext context, VaultViewMode mode) {
    final l10n = context.l10n;
    switch (mode) {
      case VaultViewMode.all:
        return const VaultDashboard();
      case VaultViewMode.favorites:
        return EmptyState(
          icon: Icons.star_outline_rounded,
          title: l10n.vaultPageFavoritesTitle,
          message: l10n.vaultPageFavoritesMessage,
          actionLabel: l10n.newEntry,
          onAction: () => VaultActions.createEntry(context),
        );
      case VaultViewMode.recent:
        return EmptyState(
          icon: Icons.history_rounded,
          title: l10n.vaultPageRecentTitle,
          message: l10n.vaultPageRecentMessage,
          hint: l10n.vaultPageRecentHint,
        );
    }
  }
}

class _ResizeHandle extends StatefulWidget {
  const _ResizeHandle({required this.onDelta});

  final ValueChanged<double> onDelta;

  @override
  State<_ResizeHandle> createState() => _ResizeHandleState();
}

class _ResizeHandleState extends State<_ResizeHandle> {
  bool _hovered = false;
  bool _dragging = false;

  @override
  Widget build(BuildContext context) {
    final palette = context.colors;
    final active = _hovered || _dragging;

    return MouseRegion(
      cursor: SystemMouseCursors.resizeLeftRight,
      onEnter: (_) => setState(() => _hovered = true),
      onExit: (_) => setState(() => _hovered = false),
      child: GestureDetector(
        behavior: HitTestBehavior.translucent,
        onHorizontalDragStart: (_) => setState(() => _dragging = true),
        onHorizontalDragEnd: (_) => setState(() => _dragging = false),
        onHorizontalDragCancel: () => setState(() => _dragging = false),
        onHorizontalDragUpdate: (details) => widget.onDelta(details.delta.dx),
        child: SizedBox(
          width: 6,
          child: Center(
            child: AnimatedContainer(
              duration: context.motion.fast,
              width: active ? 2 : 1,
              color: active ? palette.accent : palette.border,
            ),
          ),
        ),
      ),
    );
  }
}
