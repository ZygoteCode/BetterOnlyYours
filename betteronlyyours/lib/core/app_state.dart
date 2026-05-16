import 'package:flutter/material.dart';
import 'package:window_manager/window_manager.dart';
import 'security_core.dart';
import 'dart:math';

class AppState extends ChangeNotifier with WindowListener {
  Map<String, String> _credentials = {};
  bool _isLoadingInit = true;
  bool _isInitialized = false;
  bool _isLocked = true;
  bool _isSaving = false;
  String _currentPassword = "";
  String _searchQuery = "";
  bool _pendingSearchOverlay = false;

  bool get isLoadingInit => _isLoadingInit;
  bool get isInitialized => _isInitialized;
  bool get isLocked => _isLocked;
  bool get isSaving => _isSaving;
  String get searchQuery => _searchQuery;
  bool get pendingSearchOverlay => _pendingSearchOverlay;

  Map<String, String> get credentials => _credentials;
  List<String> get filteredKeys => _credentials.keys
      .where((k) => k.toLowerCase().contains(_searchQuery.toLowerCase()))
      .toList();

  AppState() {
    windowManager.addListener(this);
    checkVaultInitialization();
  }

  @override
  void dispose() {
    windowManager.removeListener(this);
    super.dispose();
  }

  @override
  void onWindowMinimize() {
    // Locks the app only when the user explicitly minimizes the window.
    lockApp();
  }

  @override
  void onWindowBlur() {
    // Intentionally left empty to improve UX.
    // The app will no longer lock when simply losing focus (e.g., ALT+TAB),
    // allowing the user to seamlessly copy-paste credentials between windows.
  }

  void requestShowSearchOverlay() {
    _pendingSearchOverlay = true;
    notifyListeners();
  }

  void clearPendingSearchOverlay() {
    _pendingSearchOverlay = false;
  }

  Future<void> checkVaultInitialization() async {
    _isLoadingInit = true;
    notifyListeners();

    _isInitialized = await SecurityCore.vaultExists();
    _isLoadingInit = false;
    notifyListeners();
  }

  void lockApp() {
    if (!_isLocked && _isInitialized) {
      _isLocked = true;
      _currentPassword = "";
      _credentials.clear();
      notifyListeners();
    }
  }

  Future<bool> initializeNewVault(String password) async {
    try {
      _credentials = {};
      _currentPassword = password;
      await SecurityCore.saveAllCredentials(_credentials, _currentPassword);
      _isInitialized = true;
      _isLocked = false;
      notifyListeners();
      return true;
    } catch (e) {
      debugPrint("Init Vault Error: $e");
      return false;
    }
  }

  Future<bool> unlockApp(String password) async {
    try {
      _credentials = await SecurityCore.loadAllCredentials(password);
      _currentPassword = password;
      _isLocked = false;
      notifyListeners();
      
      // If search overlay was requested while locked, show it after unlock
      if (_pendingSearchOverlay) {
        _pendingSearchOverlay = false;
        WidgetsBinding.instance.addPostFrameCallback((_) {
          _triggerSearchOverlay();
        });
      }
      
      return true;
    } catch (e) {
      debugPrint("Unlock Error: $e");
      return false;
    }
  }

  VoidCallback? onShowSearchOverlay;

  void _triggerSearchOverlay() {
    onShowSearchOverlay?.call();
  }

  Future<void> addOrUpdateCredential(String key, String value) async {
    _credentials[key] = value;
    await _saveData();
    notifyListeners();
  }

  Future<void> deleteCredential(String key) async {
    _credentials.remove(key);
    await _saveData();
    notifyListeners();
  }

  Future<void> _saveData() async {
    if (_currentPassword.isEmpty || _isSaving) return;

    _isSaving = true;
    notifyListeners();

    try {
      await SecurityCore.saveAllCredentials(_credentials, _currentPassword);
    } catch (e) {
      debugPrint("Error saving credentials: $e");
    } finally {
      _isSaving = false;
      notifyListeners();
    }
  }

  void setSearchQuery(String q) {
    _searchQuery = q;
    notifyListeners();
  }

  String generateStrongPassword() {
    const chars =
        'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!"|\\\$%&/()=?\'èé[{+*]+òç@à°#ù§,;.:-_<>';
    final random = Random.secure();
    return List.generate(
      64,
      (index) => chars[random.nextInt(chars.length)],
    ).join();
  }
}
