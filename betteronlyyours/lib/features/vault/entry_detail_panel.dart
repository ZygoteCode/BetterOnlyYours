import 'package:flutter/foundation.dart';
import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/intents.dart';
import '../../app/theme/tokens.dart';
import '../../l10n/l10n.dart';
import '../../core/models/vault_entry.dart';
import '../../core/services/password_generator.dart';
import '../../core/utils/formatting.dart';
import '../../shared/animations/entrance.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_dialog.dart';
import '../../shared/widgets/app_menu.dart';
import '../../shared/widgets/app_surface.dart';
import '../../shared/widgets/app_text_field.dart';
import '../../shared/widgets/entry_avatar.dart';
import '../../shared/widgets/tag_chip.dart';
import '../../state/settings_controller.dart';
import '../../state/shell_controller.dart';
import '../../state/toast_controller.dart';
import '../../state/vault_controller.dart';
import 'password_history_section.dart';
import 'secret_field.dart';
import 'totp_section.dart';
import 'vault_actions.dart';

/// Full entry editor: identity, credentials, notes, tags and custom fields.
///
/// Editing is explicit — changes stay local until saved (Ctrl+S), and
/// switching entries with pending edits asks what to do with them.
class EntryDetailPanel extends StatefulWidget {
  const EntryDetailPanel({
    super.key,
    required this.entry,
    this.showBackButton = false,
    this.showMetadata = true,
  });

  final VaultEntry entry;
  final bool showBackButton;

  /// Hidden when the inspector column already shows the same information.
  final bool showMetadata;

  @override
  State<EntryDetailPanel> createState() => EntryDetailPanelState();
}

class EntryDetailPanelState extends State<EntryDetailPanel> {
  final TextEditingController _title = TextEditingController();
  final TextEditingController _username = TextEditingController();
  final TextEditingController _password = TextEditingController();
  final TextEditingController _url = TextEditingController();
  final TextEditingController _notes = TextEditingController();
  final TextEditingController _newTag = TextEditingController();
  final ScrollController _scroll = ScrollController();

  List<String> _tags = <String>[];
  List<_FieldEditor> _fields = <_FieldEditor>[];
  bool _saving = false;
  bool _addingTag = false;
  String? _titleError;

  @override
  void initState() {
    super.initState();
    _loadFrom(widget.entry);
  }

  @override
  void didUpdateWidget(covariant EntryDetailPanel oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.entry.title == widget.entry.title) {
      // Same entry: adopt external changes only when the user has none pending.
      if (!_isDirtyAgainst(oldWidget.entry)) _loadFrom(widget.entry);
      return;
    }
    if (_isDirtyAgainst(oldWidget.entry)) {
      final previous = oldWidget.entry;
      final pending = _snapshot(previous);
      _loadFrom(widget.entry);
      WidgetsBinding.instance.addPostFrameCallback((_) {
        _askAboutPendingChanges(previous, pending);
      });
      return;
    }
    _loadFrom(widget.entry);
  }

  @override
  void dispose() {
    _title.dispose();
    _username.dispose();
    _password.dispose();
    _url.dispose();
    _notes.dispose();
    _newTag.dispose();
    _scroll.dispose();
    for (final field in _fields) {
      field.dispose();
    }
    super.dispose();
  }

  void _loadFrom(VaultEntry entry) {
    _title.text = entry.title;
    _username.text = entry.username;
    _password.text = entry.password;
    _url.text = entry.url;
    _notes.text = entry.notes;
    _tags = List<String>.from(entry.tags);
    for (final field in _fields) {
      field.dispose();
    }
    _fields = entry.customFields
        .map(
          (f) => _FieldEditor(
            label: TextEditingController(text: f.label),
            value: TextEditingController(text: f.value),
            secret: f.secret,
          ),
        )
        .toList();
    _titleError = null;
    _addingTag = false;
    _newTag.clear();
  }

  VaultEntry _snapshot(VaultEntry base) {
    return base.copyWith(
      title: _title.text.trim().isEmpty ? base.title : _title.text.trim(),
      username: _username.text.trim(),
      password: _password.text,
      url: _url.text.trim(),
      notes: _notes.text,
      tags: _tags,
      customFields: _fields
          .where((f) => f.label.text.trim().isNotEmpty)
          .map(
            (f) => VaultCustomField(
              label: f.label.text.trim(),
              value: f.value.text,
              secret: f.secret,
            ),
          )
          .toList(),
    );
  }

  bool _isDirtyAgainst(VaultEntry entry) {
    final snapshot = _snapshot(entry);
    if (snapshot.title != entry.title) return true;
    if (snapshot.username != entry.username) return true;
    if (snapshot.password != entry.password) return true;
    if (snapshot.url != entry.url) return true;
    if (snapshot.notes != entry.notes) return true;
    if (!listEquals(snapshot.tags, entry.tags)) return true;
    if (snapshot.customFields.length != entry.customFields.length) return true;
    for (var i = 0; i < snapshot.customFields.length; i++) {
      if (snapshot.customFields[i] != entry.customFields[i]) return true;
    }
    return false;
  }

  bool get isDirty => _isDirtyAgainst(widget.entry);

  Future<void> _askAboutPendingChanges(
    VaultEntry previous,
    VaultEntry pending,
  ) async {
    if (!mounted) return;
    final l10n = context.l10n;
    final vault = context.read<VaultController>();
    final toasts = context.read<ToastController>();

    final save = await showConfirmDialog(
      context: context,
      title: l10n.detailUnsavedChanges,
      message: l10n.detailUnsavedPrompt,
      detail: previous.title,
      confirmLabel: l10n.detailSaveChanges,
      cancelLabel: l10n.detailDiscard,
      icon: Icons.edit_note_rounded,
    );
    if (!save) {
      toasts.show(l10n.detailChangesDiscarded, detail: previous.title);
      return;
    }

    final ok = await vault.upsertEntry(pending, previousTitle: previous.title);
    if (ok) toasts.success(l10n.detailSaved, detail: pending.title);
  }

  Future<void> save() async {
    if (_saving) return;
    final l10n = context.l10n;
    final vault = context.read<VaultController>();
    final toasts = context.read<ToastController>();

    final title = _title.text.trim();
    if (title.isEmpty) {
      setState(() => _titleError = l10n.detailNameRequired);
      return;
    }
    if (title != widget.entry.title && vault.hasEntry(title)) {
      setState(() => _titleError = l10n.detailNameTaken);
      return;
    }

    setState(() {
      _titleError = null;
      _saving = true;
    });

    final ok = await vault.upsertEntry(
      _snapshot(widget.entry),
      previousTitle: widget.entry.title,
    );

    if (!mounted) return;
    setState(() => _saving = false);
    if (ok) {
      toasts.success(l10n.detailSaved, detail: title);
    }
  }

  void _revert() {
    setState(() => _loadFrom(widget.entry));
  }

  void _generatePassword() {
    final l10n = context.l10n;
    final options = context.read<SettingsController>().settings.generator;
    final generated = PasswordGenerator().generate(options);
    setState(() => _password.text = generated.value);
    context.read<ToastController>().show(
      l10n.detailPasswordGenerated,
      detail: l10n.detailPasswordGeneratedDetail,
      duration: const Duration(seconds: 3),
    );
  }

  void _addTag(String value) {
    final tag = value.trim();
    if (tag.isEmpty) return;
    if (_tags.any((t) => t.toLowerCase() == tag.toLowerCase())) return;
    setState(() {
      _tags = <String>[..._tags, tag];
      _newTag.clear();
    });
  }

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final entry = widget.entry;
    final l10n = context.l10n;
    final dirty = isDirty;
    final revealDefault = context
        .watch<SettingsController>()
        .settings
        .revealSecretsByDefault;

    // Ctrl+S resolves here whenever focus is inside the editor.
    return Actions(
      actions: <Type, Action<Intent>>{
        SaveEntryIntent: CallbackAction<SaveEntryIntent>(
          onInvoke: (_) {
            save();
            return null;
          },
        ),
      },
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          _buildHeader(context, entry, dirty),
          Expanded(
            child: Scrollbar(
              controller: _scroll,
              child: SingleChildScrollView(
                controller: _scroll,
                padding: EdgeInsets.fromLTRB(
                  tokens.panePadding,
                  Insets.lg,
                  tokens.panePadding,
                  tokens.panePadding,
                ),
                child: Center(
                  child: ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: 720),
                    child: EntranceFade(
                      key: ValueKey<String>('detail-${entry.title}'),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: <Widget>[
                          if (entry.isLegacyFormat) _buildLegacyBanner(context),
                          AppTextField(
                            controller: _title,
                            label: l10n.fieldName,
                            hint: l10n.fieldNameHint,
                            errorText: _titleError,
                            onChanged: (_) => setState(() {}),
                            onSubmitted: (_) => save(),
                          ),
                          const SizedBox(height: Insets.lg),
                          AppTextField(
                            controller: _username,
                            label: l10n.fieldUsername,
                            hint: l10n.fieldNotSet,
                            prefixIcon: Icons.person_outline_rounded,
                            onChanged: (_) => setState(() {}),
                            suffix: _username.text.isEmpty
                                ? null
                                : AppIconButton(
                                    icon: Icons.copy_rounded,
                                    tooltip: l10n.detailCopyUsernameShortcut,
                                    dense: true,
                                    size: 15,
                                    onPressed: () => VaultActions.copyValue(
                                      context,
                                      value: _username.text,
                                      label: l10n.labelUsername,
                                    ),
                                  ),
                          ),
                          const SizedBox(height: Insets.lg),
                          SecretField(
                            controller: _password,
                            label: l10n.fieldPassword,
                            hint: l10n.fieldNotSet,
                            revealByDefault: revealDefault,
                            onGenerate: _generatePassword,
                            onCopy: () => VaultActions.copyValue(
                              context,
                              value: _password.text,
                              label: l10n.fieldPassword,
                            ),
                            onChanged: (_) => setState(() {}),
                          ),
                          const SizedBox(height: Insets.xl),
                          TotpSection(entry: entry),
                          const SizedBox(height: Insets.xl),
                          AppTextField(
                            controller: _url,
                            label: l10n.fieldWebsite,
                            hint: 'https://…',
                            prefixIcon: Icons.link_rounded,
                            onChanged: (_) => setState(() {}),
                            suffix: _url.text.isEmpty
                                ? null
                                : AppIconButton(
                                    icon: Icons.open_in_new_rounded,
                                    tooltip: l10n.detailOpenInBrowser,
                                    dense: true,
                                    size: 15,
                                    onPressed: () => VaultActions.openUrl(
                                      context,
                                      _snapshot(entry),
                                    ),
                                  ),
                          ),
                          const SizedBox(height: Insets.xl),
                          _buildTags(context),
                          const SizedBox(height: Insets.xl),
                          AppTextField(
                            controller: _notes,
                            label: l10n.fieldNotes,
                            hint: l10n.fieldNotesHint,
                            maxLines: 8,
                            minLines: 4,
                            monospace: true,
                            textAlignVertical: TextAlignVertical.top,
                            onChanged: (_) => setState(() {}),
                          ),
                          const SizedBox(height: Insets.xl),
                          _buildCustomFields(context, revealDefault),
                          const SizedBox(height: Insets.xl),
                          PasswordHistorySection(entry: entry),
                          if (widget.showMetadata) ...<Widget>[
                            const SizedBox(height: Insets.xl),
                            _buildMetadata(context, entry),
                          ],
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            ),
          ),
          AnimatedSize(
            duration: context.motion.normal,
            curve: context.motion.standard,
            child: dirty
                ? Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: Insets.xl,
                      vertical: Insets.md,
                    ),
                    decoration: BoxDecoration(
                      color: palette.surface,
                      border: Border(top: BorderSide(color: palette.border)),
                    ),
                    child: Row(
                      children: <Widget>[
                        Container(
                          width: 7,
                          height: 7,
                          decoration: BoxDecoration(
                            color: palette.warning,
                            shape: BoxShape.circle,
                          ),
                        ),
                        const SizedBox(width: Insets.sm),
                        Expanded(
                          child: Text(
                            l10n.detailUnsavedChanges,
                            style: tokens.text.secondary,
                          ),
                        ),
                        AppButton(
                          label: l10n.detailRevert,
                          variant: AppButtonVariant.ghost,
                          size: AppButtonSize.small,
                          onPressed: _revert,
                        ),
                        const SizedBox(width: Insets.sm),
                        AppButton(
                          label: l10n.detailSave,
                          icon: Icons.save_rounded,
                          size: AppButtonSize.small,
                          loading: _saving,
                          tooltip: 'Ctrl+S',
                          onPressed: save,
                        ),
                      ],
                    ),
                  )
                : const SizedBox(width: double.infinity),
          ),
        ],
      ),
    );
  }

  Widget _buildHeader(BuildContext context, VaultEntry entry, bool dirty) {
    final tokens = context.tokens;
    final l10n = context.l10n;
    final palette = tokens.color;

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: Insets.xl,
        vertical: Insets.md,
      ),
      decoration: BoxDecoration(
        color: palette.backgroundElevated,
        border: Border(bottom: BorderSide(color: palette.border)),
      ),
      child: Row(
        children: <Widget>[
          if (widget.showBackButton) ...<Widget>[
            AppIconButton(
              icon: Icons.arrow_back_rounded,
              tooltip: l10n.detailBackToList,
              onPressed: context.read<ShellController>().showList,
            ),
            const SizedBox(width: Insets.sm),
          ],
          EntryAvatar(entry: entry, size: 34, selected: true),
          const SizedBox(width: Insets.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                Text(
                  _title.text.trim().isEmpty ? entry.title : _title.text.trim(),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: tokens.text.sectionTitle,
                ),
                Text(
                  entry.host ?? entry.subtitle,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: tokens.text.caption,
                ),
              ],
            ),
          ),
          AppIconButton(
            icon: entry.favorite
                ? Icons.star_rounded
                : Icons.star_outline_rounded,
            tooltip: entry.favorite
                ? l10n.detailRemoveFavorite
                : l10n.detailAddFavorite,
            color: entry.favorite ? palette.warning : null,
            onPressed: () => VaultActions.toggleFavorite(context, entry),
          ),
          if (entry.url.isNotEmpty)
            AppIconButton(
              icon: Icons.language_rounded,
              tooltip: l10n.detailOpenWebsite,
              onPressed: () => VaultActions.openUrl(context, entry),
            ),
          AppIconButton(
            icon: Icons.key_rounded,
            tooltip: l10n.detailCopyPasswordShortcut,
            onPressed: entry.password.isEmpty
                ? null
                : () => VaultActions.copyPassword(context, entry),
          ),
          Builder(
            builder: (menuContext) => AppIconButton(
              icon: Icons.more_horiz_rounded,
              tooltip: l10n.listEntryActions,
              onPressed: () {
                final box = menuContext.findRenderObject() as RenderBox?;
                Offset? anchor;
                if (box != null) {
                  anchor = box.localToGlobal(Offset(0, box.size.height));
                }
                showAppMenu(
                  context: menuContext,
                  globalPosition: anchor,
                  entries: VaultActions.contextMenu(context, entry),
                );
              },
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildLegacyBanner(BuildContext context) {
    final tokens = context.tokens;
    final l10n = context.l10n;
    final palette = tokens.color;
    return Container(
      margin: const EdgeInsets.only(bottom: Insets.lg),
      padding: const EdgeInsets.all(Insets.md),
      decoration: BoxDecoration(
        color: palette.warning.withValues(alpha: 0.08),
        borderRadius: Corners.radiusSm,
        border: Border.all(color: palette.warning.withValues(alpha: 0.32)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(Icons.history_edu_rounded, size: 16, color: palette.warning),
          const SizedBox(width: Insets.md),
          Expanded(
            child: Text(l10n.detailLegacyBanner, style: tokens.text.secondary),
          ),
        ],
      ),
    );
  }

  Widget _buildTags(BuildContext context) {
    final tokens = context.tokens;
    final l10n = context.l10n;
    final vault = context.read<VaultController>();
    final suggestions = vault.allTags
        .where(
          (t) => !_tags.any(
            (existing) => existing.toLowerCase() == t.toLowerCase(),
          ),
        )
        .take(6)
        .toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(l10n.fieldTags, style: tokens.text.label),
        const SizedBox(height: Insets.sm),
        Wrap(
          spacing: Insets.sm,
          runSpacing: Insets.sm,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: <Widget>[
            for (final tag in _tags)
              TagChip(
                label: tag,
                selected: true,
                onRemove: () => setState(() {
                  _tags = _tags.where((t) => t != tag).toList();
                }),
              ),
            if (_addingTag)
              SizedBox(
                width: 180,
                child: AppTextField(
                  controller: _newTag,
                  hint: l10n.fieldTagName,
                  autofocus: true,
                  onSubmitted: (value) {
                    _addTag(value);
                    setState(() => _addingTag = false);
                  },
                ),
              )
            else
              TagChip(
                label: l10n.fieldAddTag,
                icon: Icons.add_rounded,
                onTap: () => setState(() => _addingTag = true),
              ),
          ],
        ),
        if (suggestions.isNotEmpty) ...<Widget>[
          const SizedBox(height: Insets.sm),
          Wrap(
            spacing: Insets.sm,
            runSpacing: Insets.sm,
            children: <Widget>[
              Text(l10n.fieldSuggestions, style: tokens.text.caption),
              for (final tag in suggestions)
                TagChip(label: tag, onTap: () => _addTag(tag)),
            ],
          ),
        ],
      ],
    );
  }

  Widget _buildCustomFields(BuildContext context, bool revealDefault) {
    final tokens = context.tokens;
    final l10n = context.l10n;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          children: <Widget>[
            Text(l10n.fieldCustomFields, style: tokens.text.label),
            const Spacer(),
            AppButton(
              label: l10n.fieldAddField,
              icon: Icons.add_rounded,
              variant: AppButtonVariant.ghost,
              size: AppButtonSize.small,
              onPressed: () => setState(() {
                _fields = <_FieldEditor>[
                  ..._fields,
                  _FieldEditor(
                    label: TextEditingController(),
                    value: TextEditingController(),
                    secret: false,
                  ),
                ];
              }),
            ),
          ],
        ),
        const SizedBox(height: Insets.sm),
        if (_fields.isEmpty)
          Text(l10n.fieldCustomFieldsEmpty, style: tokens.text.caption)
        else
          for (var i = 0; i < _fields.length; i++)
            Padding(
              padding: const EdgeInsets.only(bottom: Insets.md),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  SizedBox(
                    width: 180,
                    child: AppTextField(
                      controller: _fields[i].label,
                      hint: l10n.fieldLabel,
                      onChanged: (_) => setState(() {}),
                    ),
                  ),
                  const SizedBox(width: Insets.sm),
                  Expanded(
                    child: _fields[i].secret
                        ? SecretField(
                            controller: _fields[i].value,
                            hint: l10n.fieldValue,
                            showStrength: false,
                            revealByDefault: revealDefault,
                            onCopy: () => VaultActions.copyValue(
                              context,
                              value: _fields[i].value.text,
                              label: _fields[i].label.text.trim().isEmpty
                                  ? l10n.fieldGenericField
                                  : _fields[i].label.text.trim(),
                            ),
                            onChanged: (_) => setState(() {}),
                          )
                        : AppTextField(
                            controller: _fields[i].value,
                            hint: l10n.fieldValue,
                            onChanged: (_) => setState(() {}),
                            suffix: AppIconButton(
                              icon: Icons.copy_rounded,
                              tooltip: l10n.fieldCopyValue,
                              dense: true,
                              size: 15,
                              onPressed: _fields[i].value.text.isEmpty
                                  ? null
                                  : () => VaultActions.copyValue(
                                      context,
                                      value: _fields[i].value.text,
                                      label:
                                          _fields[i].label.text.trim().isEmpty
                                          ? l10n.fieldGenericField
                                          : _fields[i].label.text.trim(),
                                    ),
                            ),
                          ),
                  ),
                  const SizedBox(width: Insets.xs),
                  AppIconButton(
                    icon: _fields[i].secret
                        ? Icons.lock_rounded
                        : Icons.lock_open_rounded,
                    tooltip: _fields[i].secret
                        ? l10n.fieldShowValue
                        : l10n.fieldHideValue,
                    dense: true,
                    size: 15,
                    active: _fields[i].secret,
                    onPressed: () =>
                        setState(() => _fields[i].secret = !_fields[i].secret),
                  ),
                  AppIconButton(
                    icon: Icons.delete_outline_rounded,
                    tooltip: l10n.fieldRemoveField,
                    dense: true,
                    size: 15,
                    danger: true,
                    onPressed: () => setState(() {
                      final removed = _fields[i];
                      _fields = <_FieldEditor>[..._fields]..removeAt(i);
                      removed.dispose();
                    }),
                  ),
                ],
              ),
            ),
      ],
    );
  }

  Widget _buildMetadata(BuildContext context, VaultEntry entry) {
    final tokens = context.tokens;
    final l10n = context.l10n;
    final vault = context.watch<VaultController>();
    final lastUsed = vault.lastUsedAt(entry.title);

    return AppCard(
      dense: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(l10n.fieldDetails, style: tokens.text.label),
          const SizedBox(height: Insets.xs),
          InfoRow(
            label: l10n.detailCreated,
            value: entry.createdAt == null
                ? l10n.detailUnknownLegacy
                : Formatting.absoluteTime(entry.createdAt),
          ),
          InfoRow(
            label: l10n.detailLastModified,
            value: entry.updatedAt == null
                ? l10n.detailUnknownLegacy
                : '${formatRelativeTime(l10n, entry.updatedAt)} · '
                      '${Formatting.absoluteTime(entry.updatedAt)}',
          ),
          InfoRow(
            label: l10n.detailLastOpened,
            value: formatRelativeTime(l10n, lastUsed),
          ),
          InfoRow(
            label: l10n.detailStorageFormat,
            value: entry.isLegacyFormat
                ? l10n.detailFormatLegacy
                : l10n.detailFormatStructured,
          ),
        ],
      ),
    );
  }
}

class _FieldEditor {
  _FieldEditor({
    required this.label,
    required this.value,
    required this.secret,
  });

  final TextEditingController label;
  final TextEditingController value;
  bool secret;

  void dispose() {
    label.dispose();
    value.dispose();
  }
}
