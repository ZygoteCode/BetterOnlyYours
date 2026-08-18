import 'package:flutter/foundation.dart';

enum ShellDestination {
  vault('Vault'),
  favorites('Favorites'),
  recent('Recent'),
  generator('Generator'),
  security('Security'),
  settings('Settings');

  const ShellDestination(this.label);

  final String label;
}

/// Navigation and transient interface state. Deliberately free of platform
/// plugins so it can be exercised in tests.
class ShellController extends ChangeNotifier {
  ShellDestination _destination = ShellDestination.vault;
  bool _sidebarCollapsed = false;
  bool _commandPaletteOpen = false;
  bool _detailExpandedOnCompact = false;
  bool _pendingPaletteAfterUnlock = false;
  String? _hotkeyError;
  bool _hotkeyRegistered = false;

  ShellDestination get destination => _destination;
  bool get sidebarCollapsed => _sidebarCollapsed;
  bool get commandPaletteOpen => _commandPaletteOpen;
  bool get detailExpandedOnCompact => _detailExpandedOnCompact;
  bool get pendingPaletteAfterUnlock => _pendingPaletteAfterUnlock;
  String? get hotkeyError => _hotkeyError;
  bool get hotkeyRegistered => _hotkeyRegistered;

  void goTo(ShellDestination destination) {
    if (_destination == destination) return;
    _destination = destination;
    if (destination != ShellDestination.vault) {
      _detailExpandedOnCompact = false;
    }
    notifyListeners();
  }

  void toggleSidebar() {
    _sidebarCollapsed = !_sidebarCollapsed;
    notifyListeners();
  }

  void setSidebarCollapsed(bool value) {
    if (_sidebarCollapsed == value) return;
    _sidebarCollapsed = value;
    notifyListeners();
  }

  void openCommandPalette() {
    if (_commandPaletteOpen) return;
    _commandPaletteOpen = true;
    notifyListeners();
  }

  void closeCommandPalette() {
    if (!_commandPaletteOpen) return;
    _commandPaletteOpen = false;
    notifyListeners();
  }

  void toggleCommandPalette() =>
      _commandPaletteOpen ? closeCommandPalette() : openCommandPalette();

  /// In single-pane layouts the detail view covers the list.
  void showDetail() {
    if (_detailExpandedOnCompact) return;
    _detailExpandedOnCompact = true;
    notifyListeners();
  }

  void showList() {
    if (!_detailExpandedOnCompact) return;
    _detailExpandedOnCompact = false;
    notifyListeners();
  }

  /// The global hotkey fired while the vault was locked; open the palette as
  /// soon as the user unlocks.
  void requestPaletteAfterUnlock() {
    _pendingPaletteAfterUnlock = true;
    notifyListeners();
  }

  bool consumePendingPalette() {
    if (!_pendingPaletteAfterUnlock) return false;
    _pendingPaletteAfterUnlock = false;
    return true;
  }

  void setHotkeyStatus({required bool registered, String? error}) {
    _hotkeyRegistered = registered;
    _hotkeyError = error;
    notifyListeners();
  }

  void reset() {
    _destination = ShellDestination.vault;
    _commandPaletteOpen = false;
    _detailExpandedOnCompact = false;
    notifyListeners();
  }
}
