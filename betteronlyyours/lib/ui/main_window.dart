import 'dart:ui';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';
import '../core/app_state.dart';

class GlowButton extends StatelessWidget {
  final String label;
  final VoidCallback? onPressed;
  final bool isLoading;
  final Color glowColor;

  const GlowButton({
    Key? key,
    required this.label,
    required this.onPressed,
    this.isLoading = false,
    this.glowColor = const Color(0xFF7C5CFF),
  }) : super(key: key);

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        boxShadow: [
          if (onPressed != null)
            BoxShadow(color: glowColor.withOpacity(0.45), blurRadius: 20, spreadRadius: -2, offset: const Offset(0, 4))
        ],
      ),
      child: ElevatedButton(
        style: ElevatedButton.styleFrom(
          backgroundColor: glowColor,
          disabledBackgroundColor: const Color(0xFF171D38),
          minimumSize: const Size(double.infinity, 50),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
          elevation: 0,
        ),
        onPressed: isLoading ? null : onPressed,
        child: isLoading
            ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2))
            : Text(label, style: const TextStyle(fontWeight: FontWeight.w700, color: Colors.white, letterSpacing: 1.2)),
      ),
    );
  }
}

class CyberTextField extends StatelessWidget {
  final TextEditingController controller;
  final String hintText;
  final bool obscureText;
  final ValueChanged<String>? onSubmitted;
  final ValueChanged<String>? onChanged;
  final Widget? prefixIcon;

  const CyberTextField({
    Key? key,
    required this.controller,
    required this.hintText,
    this.obscureText = false,
    this.onSubmitted,
    this.onChanged,
    this.prefixIcon,
  }) : super(key: key);

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: controller,
      obscureText: obscureText,
      style: const TextStyle(color: Color(0xFFF5F7FF), fontSize: 14, letterSpacing: 1.2),
      onSubmitted: onSubmitted,
      onChanged: onChanged,
      cursorColor: const Color(0xFF00D4FF),
      decoration: InputDecoration(
        hintText: hintText,
        hintStyle: const TextStyle(color: Color(0xFF4B5675)),
        prefixIcon: prefixIcon,
        filled: true,
        fillColor: const Color(0xFF0B1023),
        enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(8), borderSide: const BorderSide(color: Color(0xFF171D38))),
        focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(8), borderSide: const BorderSide(color: Color(0xFF00D4FF), width: 1.5)),
      ),
    );
  }
}

class CreateVaultScreen extends StatefulWidget {
  const CreateVaultScreen({Key? key}) : super(key: key);

  @override
  State<CreateVaultScreen> createState() => _CreateVaultScreenState();
}

class _CreateVaultScreenState extends State<CreateVaultScreen> {
  final TextEditingController _passCtrl = TextEditingController();
  final TextEditingController _confirmPassCtrl = TextEditingController();
  String _errorMessage = "";
  bool _isCreating = false;

  @override
  void dispose() {
    _passCtrl.dispose();
    _confirmPassCtrl.dispose();
    super.dispose();
  }

  void _handleCreateVault() async {
    final password = _passCtrl.text;
    final confirmPassword = _confirmPassCtrl.text;

    if (password.isEmpty || confirmPassword.isEmpty) {
      setState(() => _errorMessage = "All fields are required.");
      return;
    }
    if (password.length < 8) {
      setState(() => _errorMessage = "Password must be at least 8 characters long.");
      return;
    }
    if (password != confirmPassword) {
      setState(() => _errorMessage = "Passwords do not match.");
      return;
    }

    setState(() {
      _errorMessage = "";
      _isCreating = true;
    });

    bool success = await context.read<AppState>().initializeNewVault(password);
    if (!success) {
      setState(() {
        _errorMessage = "Failed to initialize secure vault database.";
        _isCreating = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Container(
        width: 440,
        padding: const EdgeInsets.all(40),
        decoration: BoxDecoration(
          color: const Color(0xFF0B1023),
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: const Color(0xFF171D38)),
          boxShadow: const [BoxShadow(color: Color(0x2600D4FF), blurRadius: 60, spreadRadius: -10)],
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(color: const Color(0xFF12172D), borderRadius: BorderRadius.circular(12)),
                  child: const Icon(Icons.shield_outlined, size: 28, color: Color(0xFF00D4FF)),
                ),
                const SizedBox(width: 16),
                const Text("Vault Initialization", style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold, color: Color(0xFFF5F7FF))),
              ],
            ),
            const SizedBox(height: 16),
            const Text(
              "No vault file detected. Set a strong master password to encrypt your credentials locally.",
              style: TextStyle(fontSize: 13, color: Color(0xFFA8B2D1), height: 1.5),
            ),
            const SizedBox(height: 32),
            CyberTextField(controller: _passCtrl, hintText: "Master Password", obscureText: true),
            const SizedBox(height: 16),
            CyberTextField(
              controller: _confirmPassCtrl,
              hintText: "Confirm Master Password",
              obscureText: true,
              onSubmitted: (_) => _handleCreateVault(),
            ),
            if (_errorMessage.isNotEmpty) ...[
              const SizedBox(height: 16),
              Text(_errorMessage, style: const TextStyle(color: Color(0xFFFF5470), fontSize: 13, fontWeight: FontWeight.w600)),
            ],
            const SizedBox(height: 32),
            GlowButton(label: "CREATE VAULT", onPressed: _isCreating ? null : _handleCreateVault, isLoading: _isCreating),
          ],
        ),
      ),
    );
  }
}

class LockScreen extends StatefulWidget {
  const LockScreen({Key? key}) : super(key: key);

  @override
  State<LockScreen> createState() => _LockScreenState();
}

class _LockScreenState extends State<LockScreen> {
  final TextEditingController _passCtrl = TextEditingController();
  bool _isLoading = false;
  String _error = "";

  @override
  void dispose() {
    _passCtrl.dispose();
    super.dispose();
  }

  void _submit() async {
    if (_passCtrl.text.isEmpty) return;
    setState(() {
      _isLoading = true;
      _error = "";
    });

    bool success = await context.read<AppState>().unlockApp(_passCtrl.text);
    if (!success) {
      setState(() {
        _error = "Invalid Master Password or corrupted signature.";
        _isLoading = false;
        _passCtrl.clear();
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Container(
        width: 380,
        padding: const EdgeInsets.all(40),
        decoration: BoxDecoration(
          color: const Color(0xFF0B1023),
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: const Color(0xFF171D38)),
          boxShadow: const [BoxShadow(color: Color(0x337C5CFF), blurRadius: 60, spreadRadius: -10)],
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: const Color(0xFF12172D),
                border: Border.all(color: const Color(0xFF7C5CFF).withOpacity(0.3)),
              ),
              child: const Icon(Icons.lock_outline, size: 36, color: Color(0xFF7C5CFF)),
            ),
            const SizedBox(height: 24),
            const Text("Encrypted Vault", style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold, color: Color(0xFFF5F7FF))),
            const SizedBox(height: 8),
            const Text("Enter your master password to unlock.", style: TextStyle(fontSize: 13, color: Color(0xFFA8B2D1))),
            const SizedBox(height: 32),
            CyberTextField(
              controller: _passCtrl,
              hintText: "Master Password",
              obscureText: true,
              prefixIcon: const Icon(Icons.key, color: Color(0xFF4B5675), size: 18),
              onSubmitted: (_) => _submit(),
            ),
            if (_error.isNotEmpty) ...[
              const SizedBox(height: 16),
              Text(_error, style: const TextStyle(color: Color(0xFFFF5470), fontSize: 13, fontWeight: FontWeight.w600)),
            ],
            const SizedBox(height: 32),
            GlowButton(label: "UNLOCK VAULT", onPressed: _isLoading ? null : _submit, isLoading: _isLoading),
          ],
        ),
      ),
    );
  }
}

class MainWindow extends StatefulWidget {
  const MainWindow({Key? key}) : super(key: key);

  @override
  State<MainWindow> createState() => _MainWindowState();
}

class _MainWindowState extends State<MainWindow> {
  static _MainWindowState? _currentInstance;
  String? _selectedKey;
  final TextEditingController _contentCtrl = TextEditingController();
  final TextEditingController _searchSideCtrl = TextEditingController();

  @override
  void initState() {
    super.initState();
    _currentInstance = this;
    _searchSideCtrl.addListener(() {
      context.read<AppState>().setSearchQuery(_searchSideCtrl.text);
    });
  }

  @override
  void dispose() {
    if (_currentInstance == this) {
      _currentInstance = null;
    }
    _contentCtrl.dispose();
    _searchSideCtrl.dispose();
    super.dispose();
  }

  void _selectCredential(String key, String content) {
    setState(() {
      _selectedKey = key;
      _contentCtrl.text = content;
    });
  }

  void _saveContent() {
    if (_selectedKey != null) {
      context.read<AppState>().addOrUpdateCredential(_selectedKey!, _contentCtrl.text);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text("Changes encrypted and saved to disk.", style: TextStyle(color: Color(0xFFF5F7FF))),
          backgroundColor: Color(0xFF171D38),
          behavior: SnackBarBehavior.floating,
        ),
      );
    }
  }

  void _addNew() {
    showDialog(
      context: context,
      barrierColor: Colors.black.withOpacity(0.6),
      builder: (c) {
        final ctrl = TextEditingController();

        void submitNewProfile() {
          if (ctrl.text.isNotEmpty) {
            context.read<AppState>().addOrUpdateCredential(ctrl.text, "");
            _selectCredential(ctrl.text, "");
          }
          Navigator.pop(c);
        }

        return AlertDialog(
          backgroundColor: const Color(0xFF0B1023),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12), side: const BorderSide(color: Color(0xFF171D38))),
          title: const Text("New Credential Profile", style: TextStyle(fontSize: 18, color: Color(0xFFF5F7FF), fontWeight: FontWeight.w600)),
          content: TextField(
            controller: ctrl,
            autofocus: true,
            style: const TextStyle(color: Colors.white),
            cursorColor: const Color(0xFF00D4FF),
            decoration: const InputDecoration(
              hintText: "Enter profile identifier...",
              hintStyle: TextStyle(color: Color(0xFF4B5675)),
              focusedBorder: UnderlineInputBorder(borderSide: BorderSide(color: Color(0xFF00D4FF))),
            ),
            onSubmitted: (_) => submitNewProfile(),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(c),
              child: const Text("CANCEL", style: TextStyle(color: Color(0xFFA8B2D1))),
            ),
            ElevatedButton(
              style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFF7C5CFF)),
              onPressed: submitNewProfile,
              child: const Text("ADD PROFILE", style: TextStyle(color: Colors.white)),
            ),
          ],
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();
    return Row(
      children: [
        Container(
          width: 320,
          decoration: const BoxDecoration(
            color: Color(0xFF0B1023),
            border: Border(right: BorderSide(color: Color(0xFF12172D))),
          ),
          child: Column(
            children: [
              Padding(
                padding: const EdgeInsets.all(20),
                child: CyberTextField(
                  controller: _searchSideCtrl,
                  hintText: "Search vault...",
                  prefixIcon: const Icon(Icons.search, color: Color(0xFF6E7A9C), size: 18),
                ),
              ),
              Expanded(
                child: ListView.builder(
                  itemCount: state.filteredKeys.length,
                  itemBuilder: (context, index) {
                    final key = state.filteredKeys[index];
                    final isSelected = key == _selectedKey;
                    return Container(
                      margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 2),
                      decoration: BoxDecoration(
                        color: isSelected ? const Color(0xFF7C5CFF).withOpacity(0.1) : Colors.transparent,
                        borderRadius: BorderRadius.circular(6),
                        border: Border.all(color: isSelected ? const Color(0xFF7C5CFF).withOpacity(0.3) : Colors.transparent),
                      ),
                      child: ListTile(
                        dense: true,
                        title: Text(key, style: TextStyle(color: isSelected ? const Color(0xFFF5F7FF) : const Color(0xFFA8B2D1), fontSize: 13, fontWeight: isSelected ? FontWeight.w600 : FontWeight.normal)),
                        onTap: () => _selectCredential(key, state.credentials[key]!),
                        trailing: IconButton(
                          icon: const Icon(Icons.delete_outline, size: 16, color: Color(0xFFFF5470)),
                          splashRadius: 16,
                          onPressed: () {
                            state.deleteCredential(key);
                            if (_selectedKey == key) {
                              setState(() { _selectedKey = null; _contentCtrl.clear(); });
                            }
                          },
                        ),
                      ),
                    );
                  },
                ),
              ),
              Padding(
                padding: const EdgeInsets.all(20),
                child: ElevatedButton.icon(
                  onPressed: _addNew,
                  icon: const Icon(Icons.add, size: 18, color: Color(0xFF00D4FF)),
                  label: const Text("ADD CREDENTIAL", style: TextStyle(color: Color(0xFF00D4FF), fontSize: 12, fontWeight: FontWeight.bold, letterSpacing: 1.0)),
                  style: ElevatedButton.styleFrom(
                    minimumSize: const Size(double.infinity, 48),
                    backgroundColor: const Color(0xFF12172D),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8), side: const BorderSide(color: Color(0xFF171D38))),
                  ),
                ),
              )
            ],
          ),
        ),
        Expanded(
          child: Container(
            color: const Color(0xFF050816),
            padding: const EdgeInsets.all(32),
            child: _selectedKey == null
                ? const Center(child: Text("Select a profile to decrypt contents.", style: TextStyle(color: Color(0xFF4B5675), fontSize: 14, letterSpacing: 1.0)))
                : Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(_selectedKey!, style: const TextStyle(fontSize: 24, fontWeight: FontWeight.w700, color: Color(0xFFF5F7FF), letterSpacing: 0.5)),
                    Row(
                      children: [
                        Tooltip(
                          message: "Generate 64-char Strong Password",
                          child: IconButton(
                            icon: const Icon(Icons.vpn_key_outlined, color: Color(0xFF00D4FF)),
                            onPressed: () {
                              final psw = state.generateStrongPassword();
                              Clipboard.setData(ClipboardData(text: psw));
                              ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
                                content: Text("Strong password copied to clipboard.", style: TextStyle(color: Colors.white)),
                                backgroundColor: Color(0xFF171D38),
                                behavior: SnackBarBehavior.floating,
                              ));
                            },
                          ),
                        ),
                        const SizedBox(width: 16),
                        SizedBox(
                          width: 160,
                          child: GlowButton(
                            label: "SAVE DATA",
                            onPressed: _saveContent,
                            isLoading: state.isSaving,
                          ),
                        ),
                      ],
                    )
                  ],
                ),
                const SizedBox(height: 32),
                Expanded(
                  child: TextField(
                    controller: _contentCtrl,
                    maxLines: null,
                    expands: true,
                    textAlignVertical: TextAlignVertical.top,
                    style: const TextStyle(color: Color(0xFFF5F7FF), height: 1.6, fontSize: 14),
                    cursorColor: const Color(0xFF00D4FF),
                    decoration: InputDecoration(
                      filled: true,
                      fillColor: const Color(0xFF0B1023),
                      enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: Color(0xFF12172D))),
                      focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: Color(0xFF171D38))),
                    ),
                  ),
                )
              ],
            ),
          ),
        )
      ],
    );
  }
}

class SearchOverlay extends StatefulWidget {
  const SearchOverlay({Key? key}) : super(key: key);

  @override
  State<SearchOverlay> createState() => _SearchOverlayState();
}

class _SearchOverlayState extends State<SearchOverlay> {
  final TextEditingController _searchCtrl = TextEditingController();

  @override
  void dispose() {
    _searchCtrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();

    if (state.isLocked) {
      WidgetsBinding.instance.addPostFrameCallback((_) => Navigator.pop(context));
    }

    final query = _searchCtrl.text.toLowerCase();
    final results = state.credentials.keys.where((k) => k.toLowerCase().contains(query)).take(6).toList();

    return GestureDetector(
      onTap: () => Navigator.pop(context),
      child: Scaffold(
        backgroundColor: Colors.transparent,
        body: BackdropFilter(
          filter: ImageFilter.blur(sigmaX: 12, sigmaY: 12),
          child: Center(
            child: GestureDetector(
              onTap: () {},
              child: Container(
                width: 600,
                decoration: BoxDecoration(
                  color: const Color(0xFF0B1023).withOpacity(0.9),
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(color: const Color(0xFF7C5CFF).withOpacity(0.2), width: 1.5),
                  boxShadow: const [BoxShadow(color: Color(0x267C5CFF), blurRadius: 80, spreadRadius: -10)],
                ),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    TextField(
                      controller: _searchCtrl,
                      autofocus: true,
                      style: const TextStyle(fontSize: 20, color: Color(0xFFF5F7FF), letterSpacing: 0.5),
                      cursorColor: const Color(0xFF00D4FF),
                      decoration: const InputDecoration(
                        hintText: "Global vault search...",
                        hintStyle: TextStyle(color: Color(0xFF4B5675), fontSize: 18),
                        prefixIcon: Padding(
                          padding: EdgeInsets.only(left: 18, right: 10),
                          child: Icon(
                            Icons.search,
                            size: 28,
                            color: Color(0xFF7C5CFF),
                          ),
                        ),
                        prefixIconConstraints: BoxConstraints(
                          minWidth: 72,
                          minHeight: 72,
                        ),
                        border: InputBorder.none,
                        contentPadding: EdgeInsets.all(24),
                      ),
                      onChanged: (val) => setState(() {}),
                      onSubmitted: (val) {
                        if (results.isNotEmpty) {
                          final firstKey = results.first;
                          _MainWindowState._currentInstance?._selectCredential(firstKey, state.credentials[firstKey]!);
                          Navigator.pop(context);
                        }
                      },
                    ),
                    if (results.isNotEmpty)
                      Container(height: 1, color: const Color(0xFF171D38)),
                    ListView.builder(
                      shrinkWrap: true,
                      physics: const NeverScrollableScrollPhysics(),
                      itemCount: results.length,
                      itemBuilder: (context, index) {
                        final key = results[index];
                        return ListTile(
                          contentPadding: const EdgeInsets.symmetric(horizontal: 24, vertical: 8),
                          title: Text(key, style: const TextStyle(color: Color(0xFFA8B2D1), fontSize: 16)),
                          hoverColor: const Color(0xFF7C5CFF).withOpacity(0.15),
                          onTap: () {
                            _MainWindowState._currentInstance?._selectCredential(key, state.credentials[key]!);
                            Navigator.pop(context);
                          },
                        );
                      },
                    ),
                    const SizedBox(height: 12)
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}