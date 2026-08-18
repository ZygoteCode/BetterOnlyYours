import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import '../../core/models/vault_entry.dart';
import '../../core/services/password_strength.dart';
import '../../core/utils/formatting.dart';
import '../../shared/animations/entrance.dart';
import '../../shared/widgets/app_surface.dart';
import '../../shared/widgets/entry_avatar.dart';
import '../../shared/widgets/hover_builder.dart';
import '../../shared/widgets/strength_meter.dart';
import '../../shared/widgets/tag_chip.dart';
import '../../state/vault_controller.dart';
import 'vault_actions.dart';

/// Contextual panel shown on wide windows: strength analysis, timeline and
/// related entries for the current selection.
class EntryInspector extends StatelessWidget {
  const EntryInspector({super.key, required this.entry});

  final VaultEntry entry;

  static const double width = 304;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final vault = context.watch<VaultController>();
    final strength = PasswordStrength.evaluate(entry.password);
    final related = vault.allEntries
        .where(
          (other) =>
              other.title != entry.title && other.tags.any(entry.tags.contains),
        )
        .take(4)
        .toList();

    return Container(
      width: width,
      decoration: BoxDecoration(
        color: palette.backgroundElevated,
        border: Border(left: BorderSide(color: palette.border)),
      ),
      child: EntranceFade(
        key: ValueKey<String>('inspector-${entry.title}'),
        offset: const Offset(0.04, 0),
        child: ListView(
          padding: const EdgeInsets.all(Insets.lg),
          children: <Widget>[
            Text('INSPECTOR', style: tokens.text.label),
            const SizedBox(height: Insets.md),
            if (entry.password.isNotEmpty) ...<Widget>[
              AppCard(
                dense: true,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text('Password strength', style: tokens.text.cardTitle),
                    const SizedBox(height: Insets.md),
                    StrengthMeter(strength: strength),
                    if (strength.suggestions.isNotEmpty) ...<Widget>[
                      const SizedBox(height: Insets.sm),
                      for (final suggestion in strength.suggestions.take(2))
                        Padding(
                          padding: const EdgeInsets.only(bottom: 2),
                          child: Text(
                            '· $suggestion',
                            style: tokens.text.caption,
                          ),
                        ),
                    ],
                    const SizedBox(height: Insets.xs),
                    Text(
                      'Estimate only — based on length, character mix and '
                      'obvious patterns.',
                      style: tokens.text.caption.copyWith(
                        color: palette.textTertiary,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: Insets.md),
            ],
            AppCard(
              dense: true,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text('Quick actions', style: tokens.text.cardTitle),
                  const SizedBox(height: Insets.sm),
                  _InspectorAction(
                    icon: Icons.person_outline_rounded,
                    label: 'Copy username',
                    enabled: entry.username.isNotEmpty,
                    onTap: () => VaultActions.copyUsername(context, entry),
                  ),
                  _InspectorAction(
                    icon: Icons.key_rounded,
                    label: 'Copy password',
                    enabled: entry.password.isNotEmpty,
                    onTap: () => VaultActions.copyPassword(context, entry),
                  ),
                  _InspectorAction(
                    icon: Icons.language_rounded,
                    label: 'Open website',
                    enabled: entry.url.isNotEmpty,
                    onTap: () => VaultActions.openUrl(context, entry),
                  ),
                  _InspectorAction(
                    icon: entry.favorite
                        ? Icons.star_rounded
                        : Icons.star_outline_rounded,
                    label: entry.favorite
                        ? 'Remove from favorites'
                        : 'Add to favorites',
                    onTap: () => VaultActions.toggleFavorite(context, entry),
                  ),
                  _InspectorAction(
                    icon: Icons.copy_all_rounded,
                    label: 'Duplicate entry',
                    onTap: () => VaultActions.duplicate(context, entry),
                  ),
                  _InspectorAction(
                    icon: Icons.delete_outline_rounded,
                    label: 'Delete entry',
                    destructive: true,
                    onTap: () => VaultActions.deleteEntry(context, entry),
                  ),
                ],
              ),
            ),
            const SizedBox(height: Insets.md),
            AppCard(
              dense: true,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text('Timeline', style: tokens.text.cardTitle),
                  const SizedBox(height: Insets.sm),
                  _TimelineRow(
                    label: 'Created',
                    value: entry.createdAt == null
                        ? 'Unknown'
                        : Formatting.absoluteTime(entry.createdAt),
                  ),
                  _TimelineRow(
                    label: 'Modified',
                    value: Formatting.relativeTime(entry.updatedAt),
                  ),
                  _TimelineRow(
                    label: 'Opened',
                    value: Formatting.relativeTime(
                      vault.lastUsedAt(entry.title),
                    ),
                  ),
                  _TimelineRow(
                    label: 'Format',
                    value: entry.isLegacyFormat ? 'Legacy text' : 'Structured',
                  ),
                ],
              ),
            ),
            if (entry.tags.isNotEmpty) ...<Widget>[
              const SizedBox(height: Insets.md),
              AppCard(
                dense: true,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text('Tags', style: tokens.text.cardTitle),
                    const SizedBox(height: Insets.sm),
                    Wrap(
                      spacing: Insets.xs + 2,
                      runSpacing: Insets.xs + 2,
                      children: <Widget>[
                        for (final tag in entry.tags)
                          TagChip(
                            label: tag,
                            onTap: () {
                              vault.setTagFilter(tag);
                            },
                          ),
                      ],
                    ),
                  ],
                ),
              ),
            ],
            if (related.isNotEmpty) ...<Widget>[
              const SizedBox(height: Insets.md),
              AppCard(
                dense: true,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text('Related', style: tokens.text.cardTitle),
                    const SizedBox(height: Insets.sm),
                    for (final other in related)
                      HoverBuilder(
                        onTap: () =>
                            VaultActions.openEntry(context, other.title),
                        builder: (context, state) => Padding(
                          padding: const EdgeInsets.symmetric(vertical: 4),
                          child: Row(
                            children: <Widget>[
                              EntryAvatar(entry: other, size: 22),
                              const SizedBox(width: Insets.sm),
                              Expanded(
                                child: Text(
                                  other.title,
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                  style: tokens.text.secondary.copyWith(
                                    color: state.active
                                        ? palette.textPrimary
                                        : palette.textSecondary,
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                  ],
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _InspectorAction extends StatelessWidget {
  const _InspectorAction({
    required this.icon,
    required this.label,
    required this.onTap,
    this.enabled = true,
    this.destructive = false,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final bool enabled;
  final bool destructive;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final color = !enabled
        ? palette.textTertiary
        : (destructive ? palette.danger : palette.textSecondary);

    return HoverBuilder(
      onTap: enabled ? onTap : null,
      enabled: enabled,
      builder: (context, state) => AnimatedContainer(
        duration: context.motion.fast,
        padding: const EdgeInsets.symmetric(
          horizontal: Insets.sm,
          vertical: Insets.sm,
        ),
        decoration: BoxDecoration(
          color: state.active ? palette.surfaceHigh : Colors.transparent,
          borderRadius: Corners.radiusSm,
        ),
        child: Row(
          children: <Widget>[
            Icon(icon, size: 15, color: color),
            const SizedBox(width: Insets.md),
            Expanded(
              child: Text(
                label,
                style: tokens.text.secondary.copyWith(
                  color: state.active && enabled
                      ? (destructive ? palette.danger : palette.textPrimary)
                      : color,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _TimelineRow extends StatelessWidget {
  const _TimelineRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          SizedBox(width: 78, child: Text(label, style: tokens.text.caption)),
          Expanded(
            child: Text(
              value,
              style: tokens.text.caption.copyWith(
                color: tokens.color.textSecondary,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
