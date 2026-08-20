import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../features/auth/create_vault_screen.dart';
import '../features/auth/lock_screen.dart';
import '../features/search/command_palette.dart';
import '../features/shell/app_shell.dart';
import '../features/shell/title_bar.dart';
import '../features/vault/vault_actions.dart';
import '../shared/widgets/brand_mark.dart';
import '../shared/widgets/toast_overlay.dart';
import '../state/settings_controller.dart';
import '../state/shell_controller.dart';
import '../state/vault_controller.dart';
import '../l10n/l10n.dart';
import 'app_info.dart';
import 'hotkey_service.dart';
import 'intents.dart';
import 'theme/app_theme.dart';
import 'theme/tokens.dart';

class BetterOnlyYoursApp extends StatelessWidget {
  const BetterOnlyYoursApp({super.key});

  @override
  Widget build(BuildContext context) {
    final settings = context.watch<SettingsController>();

    return MaterialApp(
      title: AppInfo.name,
      debugShowCheckedModeBanner: false,
      theme: AppTheme.build(settings.settings),
      locale: settings.settings.language.locale,
      supportedLocales: appSupportedLocales,
      localizationsDelegates: appLocalizationsDelegates,
      home: const _AppRoot(),
    );
  }
}

class _AppRoot extends StatefulWidget {
  const _AppRoot();

  @override
  State<_AppRoot> createState() => _AppRootState();
}

class _AppRootState extends State<_AppRoot> {
  final FocusNode _searchFocus = FocusNode(debugLabel: 'vault-search');

  @override
  void dispose() {
    _searchFocus.dispose();
    super.dispose();
  }

  void _noteActivity([_]) => context.read<VaultController>().noteActivity();

  /// The global shortcut is a platform integration: it is absent in tests and
  /// on builds where the hotkey plugin cannot start. Missing it must never
  /// take the whole interface down.
  HotkeyService? _hotkeys(BuildContext context) {
    try {
      return context.read<HotkeyService>();
    } on ProviderNotFoundException {
      return null;
    }
  }

  Map<Type, Action<Intent>> _actions(BuildContext context) {
    final shell = context.read<ShellController>();
    final vault = context.read<VaultController>();

    return <Type, Action<Intent>>{
      OpenPaletteIntent: CallbackAction<OpenPaletteIntent>(
        onInvoke: (_) {
          if (vault.isUnlocked) shell.toggleCommandPalette();
          return null;
        },
      ),
      NewEntryIntent: CallbackAction<NewEntryIntent>(
        onInvoke: (_) {
          if (vault.isUnlocked) VaultActions.createEntry(context);
          return null;
        },
      ),
      LockVaultIntent: CallbackAction<LockVaultIntent>(
        onInvoke: (_) {
          if (vault.isUnlocked) VaultActions.lockVault(context);
          return null;
        },
      ),
      FocusSearchIntent: CallbackAction<FocusSearchIntent>(
        onInvoke: (_) {
          if (!vault.isUnlocked) return null;
          if (shell.destination == ShellDestination.generator ||
              shell.destination == ShellDestination.security ||
              shell.destination == ShellDestination.settings) {
            shell.goTo(ShellDestination.vault);
          }
          shell.showList();
          WidgetsBinding.instance.addPostFrameCallback((_) {
            if (mounted && _searchFocus.canRequestFocus) {
              _searchFocus.requestFocus();
            }
          });
          return null;
        },
      ),
      OpenGeneratorIntent: CallbackAction<OpenGeneratorIntent>(
        onInvoke: (_) {
          if (vault.isUnlocked) shell.goTo(ShellDestination.generator);
          return null;
        },
      ),
      OpenSettingsIntent: CallbackAction<OpenSettingsIntent>(
        onInvoke: (_) {
          if (vault.isUnlocked) shell.goTo(ShellDestination.settings);
          return null;
        },
      ),
      ToggleFavoriteIntent: CallbackAction<ToggleFavoriteIntent>(
        onInvoke: (_) {
          final entry = vault.selectedEntry;
          if (entry != null) VaultActions.toggleFavorite(context, entry);
          return null;
        },
      ),
      CopyPasswordIntent: CallbackAction<CopyPasswordIntent>(
        onInvoke: (_) {
          final entry = vault.selectedEntry;
          if (entry != null) VaultActions.copyPassword(context, entry);
          return null;
        },
      ),
      CopyUsernameIntent: CallbackAction<CopyUsernameIntent>(
        onInvoke: (_) {
          final entry = vault.selectedEntry;
          if (entry != null) VaultActions.copyUsername(context, entry);
          return null;
        },
      ),
      DeleteEntryIntent: CallbackAction<DeleteEntryIntent>(
        onInvoke: (_) {
          final entry = vault.selectedEntry;
          if (entry != null && !shell.commandPaletteOpen) {
            VaultActions.deleteEntry(context, entry);
          }
          return null;
        },
      ),
      DismissOverlayIntent: CallbackAction<DismissOverlayIntent>(
        onInvoke: (_) {
          if (shell.commandPaletteOpen) {
            shell.closeCommandPalette();
          } else if (shell.detailExpandedOnCompact) {
            shell.showList();
          }
          return null;
        },
      ),
      // Ctrl+S is handled by the entry editor when it owns focus; this is the
      // fallback so the shortcut never looks broken elsewhere.
      SaveEntryIntent: CallbackAction<SaveEntryIntent>(onInvoke: (_) => null),
    };
  }

  @override
  Widget build(BuildContext context) {
    final vault = context.watch<VaultController>();
    final shell = context.watch<ShellController>();
    final palette = context.colors;

    // Background notifications come from controllers that have no context.
    // The hotkey service is a platform integration: absent in tests and on
    // builds where the global shortcut is unavailable, so it stays optional.
    final l10n = context.l10n;
    vault.localizations = l10n;
    _hotkeys(context)?.localizations = l10n;

    return Shortcuts(
      shortcuts: appShortcuts,
      child: Actions(
        actions: _actions(context),
        child: Listener(
          onPointerDown: _noteActivity,
          onPointerSignal: _noteActivity,
          child: Focus(
            onKeyEvent: (node, event) {
              _noteActivity();
              return KeyEventResult.ignored;
            },
            child: Scaffold(
              backgroundColor: palette.background,
              body: Stack(
                fit: StackFit.expand,
                children: <Widget>[
                  RepaintBoundary(
                    child: AnimatedSwitcher(
                      duration: context.motion.page,
                      switchInCurve: context.motion.emphasized,
                      switchOutCurve: context.motion.exit,
                      transitionBuilder: (child, animation) => FadeTransition(
                        opacity: animation,
                        child: ScaleTransition(
                          scale: Tween<double>(
                            begin: 0.985,
                            end: 1,
                          ).animate(animation),
                          child: child,
                        ),
                      ),
                      child: _buildStage(vault),
                    ),
                  ),
                  if (shell.commandPaletteOpen && vault.isUnlocked)
                    const CommandPalette(),
                  const ToastLayer(),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildStage(VaultController vault) {
    switch (vault.status) {
      case VaultStatus.loading:
        return const _SplashStage(key: ValueKey<String>('splash'));
      case VaultStatus.missing:
        return const _FramedStage(
          key: ValueKey<String>('create'),
          child: CreateVaultScreen(),
        );
      case VaultStatus.locked:
        return const _FramedStage(
          key: ValueKey<String>('lock'),
          child: LockScreen(),
        );
      case VaultStatus.unlocked:
        return KeyedSubtree(
          key: const ValueKey<String>('shell'),
          child: AppShell(searchFocusNode: _searchFocus),
        );
    }
  }
}

/// Screens shown before the shell still need the custom window frame.
class _FramedStage extends StatelessWidget {
  const _FramedStage({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        const AppTitleBar(),
        Expanded(child: child),
      ],
    );
  }
}

class _SplashStage extends StatelessWidget {
  const _SplashStage({super.key});

  @override
  Widget build(BuildContext context) {
    final tokens = context.tokens;
    return Column(
      children: <Widget>[
        const AppTitleBar(),
        Expanded(
          child: Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                const BrandMark(size: 42, glow: true),
                const SizedBox(height: Insets.lg),
                const BrandWordmark(fontSize: 15),
                const SizedBox(height: Insets.md),
                SizedBox(
                  width: 120,
                  child: LinearProgressIndicator(
                    minHeight: 2,
                    backgroundColor: tokens.color.surfaceHigh,
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}
