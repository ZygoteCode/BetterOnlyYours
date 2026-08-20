import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../l10n/l10n.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_dialog.dart';
import '../../shared/widgets/page_scaffold.dart';
import '../../state/shell_controller.dart';
import 'generator_panel.dart';

class GeneratorPage extends StatelessWidget {
  const GeneratorPage({super.key});

  @override
  Widget build(BuildContext context) {
    return PageScaffold(
      title: context.l10n.generatorTitle,
      subtitle: context.l10n.generatorSubtitle,
      icon: Icons.casino_rounded,
      actions: <Widget>[
        AppButton(
          label: context.l10n.newEntry,
          icon: Icons.add_rounded,
          variant: AppButtonVariant.secondary,
          size: AppButtonSize.small,
          onPressed: () {
            context.read<ShellController>().goTo(ShellDestination.vault);
          },
        ),
      ],
      child: const GeneratorPanel(),
    );
  }
}

/// Generator in a dialog, used from password fields.
Future<String?> showGeneratorDialog(BuildContext context) {
  return showAppDialog<String>(
    context: context,
    builder: (dialogContext) => AppDialog(
      title: dialogContext.l10n.generatorDialogTitle,
      subtitle: dialogContext.l10n.generatorDialogSubtitle,
      icon: Icons.casino_rounded,
      width: 620,
      scrollable: true,
      content: GeneratorPanel(
        showHistory: false,
        onUse: (value) => Navigator.of(dialogContext).pop(value),
      ),
      actions: <Widget>[
        AppButton(
          label: dialogContext.l10n.commonClose,
          variant: AppButtonVariant.ghost,
          onPressed: () => Navigator.of(dialogContext).pop(),
        ),
      ],
    ),
  );
}
