import 'package:material_ui/material_ui.dart';
import 'package:flutter/services.dart';

class OpenPaletteIntent extends Intent {
  const OpenPaletteIntent();
}

class NewEntryIntent extends Intent {
  const NewEntryIntent();
}

class LockVaultIntent extends Intent {
  const LockVaultIntent();
}

class FocusSearchIntent extends Intent {
  const FocusSearchIntent();
}

class SaveEntryIntent extends Intent {
  const SaveEntryIntent();
}

class ToggleFavoriteIntent extends Intent {
  const ToggleFavoriteIntent();
}

class CopyPasswordIntent extends Intent {
  const CopyPasswordIntent();
}

class CopyUsernameIntent extends Intent {
  const CopyUsernameIntent();
}

class CopyTotpIntent extends Intent {
  const CopyTotpIntent();
}

class OpenGeneratorIntent extends Intent {
  const OpenGeneratorIntent();
}

class OpenSettingsIntent extends Intent {
  const OpenSettingsIntent();
}

class DeleteEntryIntent extends Intent {
  const DeleteEntryIntent();
}

class DismissOverlayIntent extends Intent {
  const DismissOverlayIntent();
}

/// Application-wide key bindings. Deliberately avoids text-editing shortcuts
/// (Ctrl+C/V/X/A/Z) so typing keeps working as expected.
final Map<ShortcutActivator, Intent> appShortcuts = <ShortcutActivator, Intent>{
  const SingleActivator(LogicalKeyboardKey.keyK, control: true):
      const OpenPaletteIntent(),
  const SingleActivator(LogicalKeyboardKey.keyN, control: true):
      const NewEntryIntent(),
  const SingleActivator(LogicalKeyboardKey.keyL, control: true):
      const LockVaultIntent(),
  const SingleActivator(LogicalKeyboardKey.keyF, control: true):
      const FocusSearchIntent(),
  const SingleActivator(LogicalKeyboardKey.keyS, control: true):
      const SaveEntryIntent(),
  const SingleActivator(LogicalKeyboardKey.keyD, control: true):
      const ToggleFavoriteIntent(),
  const SingleActivator(LogicalKeyboardKey.keyG, control: true):
      const OpenGeneratorIntent(),
  const SingleActivator(LogicalKeyboardKey.comma, control: true):
      const OpenSettingsIntent(),
  const SingleActivator(LogicalKeyboardKey.keyC, control: true, shift: true):
      const CopyPasswordIntent(),
  const SingleActivator(LogicalKeyboardKey.keyU, control: true, shift: true):
      const CopyUsernameIntent(),
  const SingleActivator(LogicalKeyboardKey.keyT, control: true, shift: true):
      const CopyTotpIntent(),
  const SingleActivator(LogicalKeyboardKey.delete): const DeleteEntryIntent(),
  const SingleActivator(LogicalKeyboardKey.escape):
      const DismissOverlayIntent(),
};
