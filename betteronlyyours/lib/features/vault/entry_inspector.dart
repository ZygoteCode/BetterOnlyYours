import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import '../../l10n/l10n.dart';
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
    final reused = vault.health.isReused(entry.title);
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
            Text(context.l10n.inspectorTitle, style: tokens.text.label),
            const SizedBox(height: Insets.md),
            if (entry.password.isNotEmpty) ...<Widget>[
              AppCard(
                dense: true,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      context.l10n.inspectorPasswordStrength,
                      style: tokens.text.cardTitle,
                    ),
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
                    if (reused) ...<Widget>[
                      const SizedBox(height: Insets.sm),
                      Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: <Widget>[
                          Icon(
                            Icons.copy_all_rounded,
                            size: 13,
                            color: palette.warning,
                          ),
                          const SizedBox(width: Insets.xs + 2),
                          Expanded(
                            child: Text(
                              context.l10n.inspectorReused,
                              style: tokens.text.caption.copyWith(
                                color: palette.warning,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ],
                    const SizedBox(height: Insets.xs),
                    Text(
                      context.l10n.strengthEstimateNote,
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
                  Text(
                    context.l10n.inspectorQuickActions,
                    style: tokens.text.cardTitle,
                  ),
                  const SizedBox(height: Insets.sm),
                  _InspectorAction(
                    icon: Icons.person_outline_rounded,
                    label: context.l10n.menuCopyUsername,
                    enabled: entry.username.isNotEmpty,
                    onTap: () => VaultActions.copyUsername(context, entry),
                  ),
                  _InspectorAction(
                    icon: Icons.key_rounded,
                    label: context.l10n.menuCopyPassword,
                    enabled: entry.password.isNotEmpty,
                    onTap: () => VaultActions.copyPassword(context, entry),
                  ),
                  _InspectorAction(
                    icon: Icons.language_rounded,
                    label: context.l10n.menuOpenWebsite,
                    enabled: entry.url.isNotEmpty,
                    onTap: () => VaultActions.openUrl(context, entry),
                  ),
                  _InspectorAction(
                    icon: entry.favorite
                        ? Icons.star_rounded
                        : Icons.star_outline_rounded,
                    label: entry.favorite
                        ? context.l10n.menuRemoveFavorite
                        : context.l10n.menuAddFavorite,
                    onTap: () => VaultActions.toggleFavorite(context, entry),
                  ),
                  _InspectorAction(
                    icon: Icons.copy_all_rounded,
                    label: context.l10n.inspectorDuplicate,
                    onTap: () => VaultActions.duplicate(context, entry),
                  ),
                  _InspectorAction(
                    icon: Icons.delete_outline_rounded,
                    label: context.l10n.inspectorDelete,
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
                  Text(
                    context.l10n.inspectorTimeline,
                    style: tokens.text.cardTitle,
                  ),
                  const SizedBox(height: Insets.sm),
                  _TimelineRow(
                    label: context.l10n.inspectorCreated,
                    value: entry.createdAt == null
                        ? context.l10n.inspectorUnknown
                        : Formatting.absoluteTime(entry.createdAt),
                  ),
                  _TimelineRow(
                    label: context.l10n.inspectorModified,
                    value: formatRelativeTime(context.l10n, entry.updatedAt),
                  ),
                  _TimelineRow(
                    label: context.l10n.inspectorOpened,
                    value: formatRelativeTime(
                      context.l10n,
                      vault.lastUsedAt(entry.title),
                    ),
                  ),
                  _TimelineRow(
                    label: context.l10n.inspectorFormat,
                    value: entry.isLegacyFormat
                        ? context.l10n.inspectorFormatLegacy
                        : context.l10n.inspectorFormatStructured,
                  ),
                  _TimelineRow(
                    label: context.l10n.inspectorHistory,
                    value: entry.passwordHistory.isEmpty
                        ? context.l10n.inspectorNoHistory
                        : context.l10n.previousPasswordsCount(
                            entry.passwordHistory.length,
                          ),
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
                    Text(
                      context.l10n.inspectorTags,
                      style: tokens.text.cardTitle,
                    ),
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
                    Text(
                      context.l10n.inspectorRelated,
                      style: tokens.text.cardTitle,
                    ),
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
