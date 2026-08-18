import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';
import '../../core/models/vault_entry.dart';
import '../../core/utils/formatting.dart';

/// Deterministic, offline avatar for an entry.
///
/// The colour is derived from the entry title, so an entry always looks the
/// same without ever asking a remote favicon service for it.
class EntryAvatar extends StatelessWidget {
  const EntryAvatar({
    super.key,
    required this.entry,
    this.size = 34,
    this.selected = false,
  });

  final VaultEntry entry;
  final double size;
  final bool selected;

  static Color accentFor(BuildContext context, String title) {
    final palette = context.colors;
    return palette.entryAccents[Formatting.accentIndex(
      title,
      palette.entryAccents.length,
    )];
  }

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final color = accentFor(context, entry.title);
    final label = Formatting.initials(entry.title);

    return AnimatedContainer(
      duration: context.motion.fast,
      curve: context.motion.standard,
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: color.withValues(alpha: selected ? 0.24 : 0.14),
        borderRadius: BorderRadius.circular(size * 0.32),
        border: Border.all(
          color: color.withValues(alpha: selected ? 0.7 : 0.28),
        ),
      ),
      alignment: Alignment.center,
      child: Text(
        label,
        style: tokens.text.bodyStrong.copyWith(
          color: color,
          fontSize: size * 0.36,
          letterSpacing: 0.2,
        ),
      ),
    );
  }
}
