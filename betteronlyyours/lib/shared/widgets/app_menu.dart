import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';

/// One entry of a desktop menu. `null` in a list renders a separator.
class AppMenuEntry {
  const AppMenuEntry({
    required this.label,
    required this.onSelected,
    this.icon,
    this.shortcut,
    this.destructive = false,
    this.enabled = true,
    this.checked = false,
  });

  final String label;
  final VoidCallback onSelected;
  final IconData? icon;
  final String? shortcut;
  final bool destructive;
  final bool enabled;
  final bool checked;
}

/// Opens a styled menu anchored at [globalPosition] (right-click) or below the
/// widget that owns [anchorKey].
Future<void> showAppMenu({
  required BuildContext context,
  required List<AppMenuEntry?> entries,
  Offset? globalPosition,
  GlobalKey? anchorKey,
  double width = 240,
}) async {
  final overlay = Overlay.of(context).context.findRenderObject() as RenderBox?;
  if (overlay == null) return;

  Offset position = globalPosition ?? Offset.zero;
  if (globalPosition == null && anchorKey?.currentContext != null) {
    final box = anchorKey!.currentContext!.findRenderObject() as RenderBox?;
    if (box != null) {
      position = box.localToGlobal(Offset(0, box.size.height + 4));
    }
  }

  final tokens = context.tokens;
  final palette = tokens.color;

  final selected = await showMenu<int>(
    context: context,
    color: palette.overlay,
    surfaceTintColor: Colors.transparent,
    elevation: 0,
    constraints: BoxConstraints(minWidth: width, maxWidth: width + 80),
    shape: RoundedRectangleBorder(
      borderRadius: Corners.radiusMd,
      side: BorderSide(color: palette.border),
    ),
    position: RelativeRect.fromLTRB(
      position.dx,
      position.dy,
      overlay.size.width - position.dx,
      overlay.size.height - position.dy,
    ),
    items: <PopupMenuEntry<int>>[
      for (var i = 0; i < entries.length; i++)
        if (entries[i] == null)
          PopupMenuDivider(height: 9, color: palette.border)
        else
          PopupMenuItem<int>(
            value: i,
            height: 38,
            enabled: entries[i]!.enabled,
            padding: const EdgeInsets.symmetric(horizontal: Insets.md),
            child: _MenuRow(entry: entries[i]!),
          ),
    ],
  );

  if (selected == null) return;
  final entry = entries[selected];
  entry?.onSelected();
}

class _MenuRow extends StatelessWidget {
  const _MenuRow({required this.entry});

  final AppMenuEntry entry;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final color = !entry.enabled
        ? palette.textTertiary
        : (entry.destructive ? palette.danger : palette.textPrimary);

    return Row(
      children: <Widget>[
        if (entry.icon != null) ...<Widget>[
          Icon(entry.icon, size: 15, color: color),
          const SizedBox(width: Insets.md),
        ] else if (entry.checked) ...<Widget>[
          Icon(Icons.check_rounded, size: 15, color: palette.accent),
          const SizedBox(width: Insets.md),
        ],
        Expanded(
          child: Text(
            entry.label,
            style: tokens.text.body.copyWith(color: color),
            overflow: TextOverflow.ellipsis,
          ),
        ),
        if (entry.shortcut != null) ...<Widget>[
          const SizedBox(width: Insets.md),
          Text(entry.shortcut!, style: tokens.text.caption),
        ],
      ],
    );
  }
}

/// Small keyboard-shortcut badge, e.g. `Ctrl` `K`.
class ShortcutHint extends StatelessWidget {
  const ShortcutHint({super.key, required this.keys, this.dim = false});

  final List<String> keys;
  final bool dim;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        for (var i = 0; i < keys.length; i++) ...<Widget>[
          if (i > 0) const SizedBox(width: Insets.xs),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
            decoration: BoxDecoration(
              color: dim ? Colors.transparent : palette.surface,
              borderRadius: Corners.radiusXs,
              border: Border.all(color: palette.border),
            ),
            child: Text(
              keys[i],
              style: tokens.text.caption.copyWith(
                fontSize: 10.5,
                color: palette.textTertiary,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ],
    );
  }
}
