import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

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
      title: 'Password generator',
      subtitle:
          'Everything is generated locally with the operating system\'s '
          'cryptographic random source.',
      icon: Icons.casino_rounded,
      actions: <Widget>[
        AppButton(
          label: 'New entry',
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
      title: 'Generate a password',
      subtitle: 'Pick the shape you need, then use it in the entry.',
      icon: Icons.casino_rounded,
      width: 620,
      scrollable: true,
      content: GeneratorPanel(
        showHistory: false,
        onUse: (value) => Navigator.of(dialogContext).pop(value),
      ),
      actions: <Widget>[
        AppButton(
          label: 'Close',
          variant: AppButtonVariant.ghost,
          onPressed: () => Navigator.of(dialogContext).pop(),
        ),
      ],
    ),
  );
}
