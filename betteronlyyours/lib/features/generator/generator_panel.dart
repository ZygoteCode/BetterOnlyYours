import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/theme/tokens.dart';
import '../../l10n/l10n.dart';
import '../../core/models/generator_options.dart';
import '../../core/services/password_generator.dart';
import '../../core/services/password_strength.dart';
import '../../core/services/word_list.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_surface.dart';
import '../../shared/widgets/hover_builder.dart';
import '../../shared/widgets/strength_meter.dart';
import '../../state/settings_controller.dart';
import '../vault/vault_actions.dart';

/// Password generator surface, reused by the Generator page and by the
/// "generate" affordance inside entry editing.
class GeneratorPanel extends StatefulWidget {
  const GeneratorPanel({
    super.key,
    this.onUse,
    this.useLabel,
    this.showHistory = true,
  });

  /// When provided, an extra action hands the generated value back.
  final ValueChanged<String>? onUse;
  final String? useLabel;
  final bool showHistory;

  @override
  State<GeneratorPanel> createState() => _GeneratorPanelState();
}

class _GeneratorPanelState extends State<GeneratorPanel> {
  final PasswordGenerator _generator = PasswordGenerator();
  final List<String> _history = <String>[];

  GeneratedSecret? _current;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _regenerate());
  }

  GeneratorOptions get _options =>
      context.read<SettingsController>().settings.generator;

  void _regenerate() {
    if (!mounted) return;
    final generated = _generator.generate(_options);
    setState(() {
      if (_current != null) {
        _history.insert(0, _current!.value);
        if (_history.length > 5) _history.removeLast();
      }
      _current = generated;
    });
  }

  void _updateOptions(GeneratorOptions Function(GeneratorOptions) transform) {
    final settings = context.read<SettingsController>();
    settings.setGenerator(transform(settings.settings.generator));
    WidgetsBinding.instance.addPostFrameCallback((_) => _regenerate());
  }

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    final options = context.watch<SettingsController>().settings.generator;
    final l10n = context.l10n;
    final current = _current;
    final strength = current == null
        ? const PasswordStrength(level: StrengthLevel.empty, entropyBits: 0)
        : PasswordStrength.evaluate(current.value);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        _SegmentedToggle(
          value: options.mode,
          segments: <GeneratorMode, String>{
            GeneratorMode.characters: l10n.generatorModeCharacters,
            GeneratorMode.passphrase: l10n.generatorModePassphrase,
          },
          onChanged: (mode) => _updateOptions((o) => o.copyWith(mode: mode)),
        ),
        const SizedBox(height: Insets.lg),
        AppCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              AnimatedSwitcher(
                duration: context.motion.fast,
                child: SelectableText(
                  current?.value ?? '',
                  key: ValueKey<String>(current?.value ?? ''),
                  style: tokens.text.monoLarge.copyWith(height: 1.5),
                ),
              ),
              const SizedBox(height: Insets.lg),
              StrengthMeter(strength: strength),
              const SizedBox(height: Insets.lg),
              Row(
                children: <Widget>[
                  AppButton(
                    label: l10n.generatorCopy,
                    icon: Icons.copy_rounded,
                    onPressed: current == null
                        ? null
                        : () => VaultActions.copyValue(
                            context,
                            value: current.value,
                            label: l10n.labelPassword,
                          ),
                  ),
                  const SizedBox(width: Insets.sm),
                  AppButton(
                    label: l10n.generatorRegenerate,
                    icon: Icons.refresh_rounded,
                    variant: AppButtonVariant.secondary,
                    onPressed: _regenerate,
                  ),
                  if (widget.onUse != null) ...<Widget>[
                    const SizedBox(width: Insets.sm),
                    AppButton(
                      label: widget.useLabel ?? l10n.generatorUsePassword,
                      icon: Icons.check_rounded,
                      variant: AppButtonVariant.secondary,
                      onPressed: current == null
                          ? null
                          : () => widget.onUse!(current.value),
                    ),
                  ],
                  const Spacer(),
                  Text(l10n.generatorRngNote, style: tokens.text.caption),
                ],
              ),
            ],
          ),
        ),
        const SizedBox(height: Insets.lg),
        AppCard(
          child: options.mode == GeneratorMode.characters
              ? _buildCharacterOptions(context, options)
              : _buildPassphraseOptions(context, options),
        ),
        if (widget.showHistory && _history.isNotEmpty) ...<Widget>[
          const SizedBox(height: Insets.lg),
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                SectionHeader(
                  title: l10n.generatorSessionTitle,
                  subtitle: l10n.generatorSessionSubtitle,
                  icon: Icons.history_rounded,
                ),
                const SizedBox(height: Insets.sm),
                for (final value in _history)
                  HoverBuilder(
                    onTap: () => VaultActions.copyValue(
                      context,
                      value: value,
                      label: l10n.labelPassword,
                    ),
                    builder: (context, state) => AnimatedContainer(
                      duration: context.motion.fast,
                      margin: const EdgeInsets.only(top: Insets.xs),
                      padding: const EdgeInsets.symmetric(
                        horizontal: Insets.sm,
                        vertical: Insets.sm,
                      ),
                      decoration: BoxDecoration(
                        color: state.active
                            ? palette.surfaceHigh
                            : Colors.transparent,
                        borderRadius: Corners.radiusSm,
                      ),
                      child: Row(
                        children: <Widget>[
                          Expanded(
                            child: Text(
                              value,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: tokens.text.mono,
                            ),
                          ),
                          Icon(
                            Icons.copy_rounded,
                            size: 14,
                            color: state.active
                                ? palette.accent
                                : palette.textTertiary,
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
    );
  }

  Widget _buildCharacterOptions(
    BuildContext context,
    GeneratorOptions options,
  ) {
    final tokens = context.tokens;
    final l10n = context.l10n;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Row(
          children: <Widget>[
            Text(l10n.generatorLength, style: tokens.text.cardTitle),
            const Spacer(),
            Text('${options.length}', style: tokens.text.mono),
          ],
        ),
        Slider(
          value: options.length.toDouble(),
          min: GeneratorOptions.minLength.toDouble(),
          max: GeneratorOptions.maxLength.toDouble(),
          divisions: GeneratorOptions.maxLength - GeneratorOptions.minLength,
          label: '${options.length}',
          onChanged: (value) =>
              _updateOptions((o) => o.copyWith(length: value.round())),
        ),
        const SizedBox(height: Insets.sm),
        Wrap(
          spacing: Insets.md,
          runSpacing: Insets.sm,
          children: <Widget>[
            _OptionSwitch(
              label: l10n.generatorLowercase,
              value: options.lowercase,
              onChanged: (value) =>
                  _updateOptions((o) => o.copyWith(lowercase: value)),
            ),
            _OptionSwitch(
              label: l10n.generatorUppercase,
              value: options.uppercase,
              onChanged: (value) =>
                  _updateOptions((o) => o.copyWith(uppercase: value)),
            ),
            _OptionSwitch(
              label: l10n.generatorDigits,
              value: options.digits,
              onChanged: (value) =>
                  _updateOptions((o) => o.copyWith(digits: value)),
            ),
            _OptionSwitch(
              label: l10n.generatorSymbols,
              value: options.symbols,
              onChanged: (value) =>
                  _updateOptions((o) => o.copyWith(symbols: value)),
            ),
            _OptionSwitch(
              label: l10n.generatorAvoidAmbiguous,
              value: options.avoidAmbiguous,
              onChanged: (value) =>
                  _updateOptions((o) => o.copyWith(avoidAmbiguous: value)),
            ),
          ],
        ),
        if (!options.hasCharacterClass) ...<Widget>[
          const SizedBox(height: Insets.md),
          Text(
            l10n.generatorNoClassWarning,
            style: tokens.text.caption.copyWith(color: tokens.color.warning),
          ),
        ],
      ],
    );
  }

  Widget _buildPassphraseOptions(
    BuildContext context,
    GeneratorOptions options,
  ) {
    final tokens = context.tokens;
    final l10n = context.l10n;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Row(
          children: <Widget>[
            Text(l10n.generatorWords, style: tokens.text.cardTitle),
            const Spacer(),
            Text('${options.words}', style: tokens.text.mono),
          ],
        ),
        Slider(
          value: options.words.toDouble(),
          min: GeneratorOptions.minWords.toDouble(),
          max: GeneratorOptions.maxWords.toDouble(),
          divisions: GeneratorOptions.maxWords - GeneratorOptions.minWords,
          label: '${options.words}',
          onChanged: (value) =>
              _updateOptions((o) => o.copyWith(words: value.round())),
        ),
        const SizedBox(height: Insets.sm),
        Row(
          children: <Widget>[
            Text(l10n.generatorSeparator, style: tokens.text.secondary),
            const SizedBox(width: Insets.md),
            for (final separator in <String>['-', '.', '_', ' '])
              Padding(
                padding: const EdgeInsets.only(right: Insets.xs),
                child: _SeparatorChip(
                  value: separator,
                  selected: options.separator == separator,
                  onTap: () =>
                      _updateOptions((o) => o.copyWith(separator: separator)),
                ),
              ),
          ],
        ),
        const SizedBox(height: Insets.md),
        Wrap(
          spacing: Insets.md,
          runSpacing: Insets.sm,
          children: <Widget>[
            _OptionSwitch(
              label: l10n.generatorCapitalize,
              value: options.capitalizeWords,
              onChanged: (value) =>
                  _updateOptions((o) => o.copyWith(capitalizeWords: value)),
            ),
            _OptionSwitch(
              label: l10n.generatorAppendNumber,
              value: options.appendNumber,
              onChanged: (value) =>
                  _updateOptions((o) => o.copyWith(appendNumber: value)),
            ),
          ],
        ),
        const SizedBox(height: Insets.md),
        Text(
          l10n.generatorWordListNote(
            PassphraseWords.words.length,
            PassphraseWords.words.length.bitLength - 1,
          ),
          style: tokens.text.caption,
        ),
      ],
    );
  }
}

class _OptionSwitch extends StatelessWidget {
  const _OptionSwitch({
    required this.label,
    required this.value,
    required this.onChanged,
  });

  final String label;
  final bool value;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    return HoverBuilder(
      onTap: () => onChanged(!value),
      builder: (context, state) => Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          SizedBox(
            height: 28,
            child: FittedBox(
              fit: BoxFit.contain,
              child: Switch(value: value, onChanged: onChanged),
            ),
          ),
          const SizedBox(width: Insets.sm),
          Text(
            label,
            style: tokens.text.secondary.copyWith(
              color: state.active
                  ? tokens.color.textPrimary
                  : tokens.color.textSecondary,
            ),
          ),
        ],
      ),
    );
  }
}

class _SeparatorChip extends StatelessWidget {
  const _SeparatorChip({
    required this.value,
    required this.selected,
    required this.onTap,
  });

  final String value;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;
    return HoverBuilder(
      onTap: onTap,
      builder: (context, state) => AnimatedContainer(
        duration: context.motion.fast,
        width: 34,
        height: 30,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: selected
              ? palette.accent.withValues(alpha: 0.16)
              : (state.active ? palette.surfaceHigh : palette.surface),
          borderRadius: Corners.radiusSm,
          border: Border.all(
            color: selected
                ? palette.accent.withValues(alpha: 0.6)
                : palette.border,
          ),
        ),
        child: Text(
          value == ' ' ? '␣' : value,
          style: tokens.text.mono.copyWith(
            color: selected ? palette.accent : palette.textSecondary,
          ),
        ),
      ),
    );
  }
}

class _SegmentedToggle<T> extends StatelessWidget {
  const _SegmentedToggle({
    required this.value,
    required this.segments,
    required this.onChanged,
  });

  final T value;
  final Map<T, String> segments;
  final ValueChanged<T> onChanged;

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    final palette = tokens.color;

    return Container(
      padding: const EdgeInsets.all(3),
      decoration: BoxDecoration(
        color: palette.surface,
        borderRadius: Corners.radiusSm,
        border: Border.all(color: palette.border),
      ),
      child: Row(
        children: <Widget>[
          for (final segment in segments.entries)
            Expanded(
              child: HoverBuilder(
                onTap: () => onChanged(segment.key),
                builder: (context, state) {
                  final selected = segment.key == value;
                  return AnimatedContainer(
                    duration: context.motion.fast,
                    curve: context.motion.standard,
                    height: 34,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      color: selected
                          ? palette.accent.withValues(alpha: 0.16)
                          : (state.active
                                ? palette.surfaceHigh
                                : Colors.transparent),
                      borderRadius: Corners.radiusXs,
                      border: Border.all(
                        color: selected
                            ? palette.accent.withValues(alpha: 0.5)
                            : Colors.transparent,
                      ),
                    ),
                    child: Text(
                      segment.value,
                      style: tokens.text.body.copyWith(
                        color: selected
                            ? palette.accent
                            : palette.textSecondary,
                        fontWeight: selected
                            ? FontWeight.w600
                            : FontWeight.w500,
                      ),
                    ),
                  );
                },
              ),
            ),
        ],
      ),
    );
  }
}
