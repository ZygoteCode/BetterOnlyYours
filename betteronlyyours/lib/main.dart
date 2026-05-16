import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';
import 'package:window_manager/window_manager.dart';
import 'package:hotkey_manager/hotkey_manager.dart';

import 'core/app_state.dart';
import 'ui/main_window.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await windowManager.ensureInitialized();
  await hotKeyManager.unregisterAll();

  WindowOptions windowOptions = const WindowOptions(
    size: Size(1100, 750),
    minimumSize: Size(900, 600),
    center: true,
    backgroundColor: Colors.transparent,
    skipTaskbar: false,
    titleBarStyle: TitleBarStyle.hidden,
  );

  windowManager.waitUntilReadyToShow(windowOptions, () async {
    await windowManager.show();
    await windowManager.focus();
  });

  runApp(
    ChangeNotifierProvider(
      create: (_) => AppState(),
      child: const OnlyYoursApp(),
    ),
  );
}

class OnlyYoursApp extends StatefulWidget {
  const OnlyYoursApp({super.key});

  @override
  State<OnlyYoursApp> createState() => _OnlyYoursAppState();
}

class _OnlyYoursAppState extends State<OnlyYoursApp> {
  static final GlobalKey<NavigatorState> navigatorKey =
      GlobalKey<NavigatorState>();

  @override
  void initState() {
    super.initState();
    _registerHotKey();

    // Set up callback for showing search overlay after unlock
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final state = context.read<AppState>();
      state.onShowSearchOverlay = _showSearchOverlay;
    });
  }

  Future<void> _registerHotKey() async {
    final searchHotKey = HotKey(
      key: PhysicalKeyboardKey.keyP,
      modifiers: [HotKeyModifier.control, HotKeyModifier.alt],
      scope: HotKeyScope.system,
    );

    await hotKeyManager.register(
      searchHotKey,
      keyDownHandler: (hotKey) async {
        if (!mounted) return;

        final state = context.read<AppState>();

        // Step 1: Always restore and focus the window first
        await windowManager.show();
        await windowManager.focus();
        await windowManager.restore();

        // Step 2: If locked or not initialized, request search overlay after unlock
        if (state.isLocked || !state.isInitialized) {
          state.requestShowSearchOverlay();
          return;
        }

        // Step 3: If unlocked, show search overlay immediately
        _showSearchOverlay();
      },
    );
  }

  void _showSearchOverlay() {
    navigatorKey.currentState?.push(
      PageRouteBuilder(
        opaque: false,
        barrierColor: Colors.black.withOpacity(0.4),
        pageBuilder: (context, _, _) => const SearchOverlay(),
        transitionsBuilder: (context, anim, _, child) =>
            FadeTransition(opacity: anim, child: child),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      navigatorKey: navigatorKey,
      debugShowCheckedModeBanner: false,
      title: 'BetterOnlyYours',
      theme: ThemeData(
        brightness: Brightness.dark,
        scaffoldBackgroundColor: const Color(0xFF050816),
        primaryColor: const Color(0xFF7C5CFF),
        colorScheme: const ColorScheme.dark(
          primary: Color(0xFF7C5CFF),
          secondary: Color(0xFF00D4FF),
          surface: Color(0xFF12172D),
          error: Color(0xFFFF5470),
        ),
        fontFamily: 'Cascadia Code',
        textTheme: const TextTheme(
          bodyMedium: TextStyle(color: Color(0xFFF5F7FF)),
          bodySmall: TextStyle(color: Color(0xFFA8B2D1)),
        ),
        textSelectionTheme: const TextSelectionThemeData(
          cursorColor: Color(0xFF00D4FF),
          selectionColor: Color(0x737C5CFF),
        ),
      ),
      home: const WindowFrame(),
    );
  }
}

class WindowFrame extends StatelessWidget {
  const WindowFrame({Key? key}) : super(key: key);

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Column(
        children: [
          GestureDetector(
            onPanStart: (details) => windowManager.startDragging(),
            child: Container(
              height: 36,
              decoration: const BoxDecoration(
                color: Color(0xFF0B1023),
                border: Border(
                  bottom: BorderSide(color: Color(0xFF171D38), width: 1),
                ),
              ),
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Row(
                    children: [
                      Container(
                        width: 8,
                        height: 8,
                        decoration: const BoxDecoration(
                          color: Color(0xFF00D4FF),
                          shape: BoxShape.circle,
                          boxShadow: [
                            BoxShadow(color: Color(0x5900D4FF), blurRadius: 6),
                          ],
                        ),
                      ),
                      const SizedBox(width: 12),
                      const Text(
                        "BETTER ONLY YOURS // SECURE VAULT",
                        style: TextStyle(
                          fontSize: 11,
                          color: Color(0xFFA8B2D1),
                          fontWeight: FontWeight.bold,
                          letterSpacing: 1.5,
                        ),
                      ),
                    ],
                  ),
                  Row(
                    children: [
                      IconButton(
                        icon: const Icon(
                          Icons.minimize,
                          size: 16,
                          color: Color(0xFFA8B2D1),
                        ),
                        onPressed: () => windowManager.minimize(),
                        hoverColor: const Color(0xFF12172D),
                        splashRadius: 18,
                        padding: EdgeInsets.zero,
                        constraints: const BoxConstraints(),
                      ),
                      const SizedBox(width: 16),
                      IconButton(
                        icon: const Icon(
                          Icons.close,
                          size: 16,
                          color: Color(0xFFA8B2D1),
                        ),
                        onPressed: () => windowManager.close(),
                        hoverColor: const Color(0xFFFF5470).withOpacity(0.2),
                        splashRadius: 18,
                        padding: EdgeInsets.zero,
                        constraints: const BoxConstraints(),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
          Expanded(
            child: Consumer<AppState>(
              builder: (context, state, child) {
                if (state.isLoadingInit) {
                  return const Center(
                    child: CircularProgressIndicator(color: Color(0xFF7C5CFF)),
                  );
                }
                if (!state.isInitialized) {
                  return const CreateVaultScreen();
                }
                return AnimatedSwitcher(
                  duration: const Duration(milliseconds: 400),
                  switchInCurve: Curves.easeOutExpo,
                  switchOutCurve: Curves.easeInExpo,
                  child: state.isLocked
                      ? const LockScreen()
                      : const MainWindow(),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
