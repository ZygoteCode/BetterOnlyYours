import 'package:material_ui/material_ui.dart';

import '../core/models/app_settings.dart';
import '../core/security/vault_exception.dart';
import '../core/services/password_strength.dart';
import '../state/shell_controller.dart';
import '../state/vault_controller.dart';
import 'generated/app_localizations.dart';

export 'generated/app_localizations.dart';

/// Shorthand for the generated strings: `context.l10n.vaultLocked`.
extension L10nContext on BuildContext {
  AppLocalizations get l10n => AppLocalizations.of(this);
}

/// Delegates to hand to `MaterialApp.localizationsDelegates`.
///
/// Deliberately not the generated `AppLocalizations.localizationsDelegates`:
/// that list takes `GlobalMaterialLocalizations` from `flutter_localizations`,
/// which localises the SDK's legacy material library. This app is built on the
/// `material_ui` package, whose widgets look up a different
/// `MaterialLocalizations` type — so with the generated list every text field,
/// menu and dialog would fail to find its localisations outside English.
/// `GlobalMaterialLocalizations.delegates` from `material_ui` bundles the
/// matching material, cupertino and widgets delegates.
const List<LocalizationsDelegate<dynamic>> appLocalizationsDelegates =
    <LocalizationsDelegate<dynamic>>[
      AppLocalizations.delegate,
      ...GlobalMaterialLocalizations.delegates,
    ];

/// Locales the interface is translated into.
const List<Locale> appSupportedLocales = AppLocalizations.supportedLocales;

/// Localised relative time ("3 minutes ago"), replacing an English-only
/// formatter.
String formatRelativeTime(
  AppLocalizations l10n,
  DateTime? time, {
  DateTime? now,
}) {
  if (time == null) return l10n.timeNever;
  final reference = now ?? DateTime.now();
  final diff = reference.difference(time);

  if (diff.isNegative || diff.inSeconds < 45) return l10n.timeJustNow;
  if (diff.inMinutes < 60) return l10n.timeMinutesAgo(diff.inMinutes);
  if (diff.inHours < 24) return l10n.timeHoursAgo(diff.inHours);
  if (diff.inDays < 7) return l10n.timeDaysAgo(diff.inDays);
  if (diff.inDays < 30) return l10n.timeWeeksAgo((diff.inDays / 7).floor());
  if (diff.inDays < 365) return l10n.timeMonthsAgo((diff.inDays / 30).floor());
  return l10n.timeYearsAgo((diff.inDays / 365).floor());
}

extension ShellDestinationL10n on ShellDestination {
  String localizedLabel(AppLocalizations l10n) => switch (this) {
    ShellDestination.vault => l10n.navVault,
    ShellDestination.favorites => l10n.navFavorites,
    ShellDestination.recent => l10n.navRecent,
    ShellDestination.generator => l10n.navGenerator,
    ShellDestination.security => l10n.navSecurity,
    ShellDestination.settings => l10n.navSettings,
  };
}

extension EntrySortL10n on EntrySort {
  String localizedLabel(AppLocalizations l10n) => switch (this) {
    EntrySort.titleAsc => l10n.sortTitleAsc,
    EntrySort.titleDesc => l10n.sortTitleDesc,
    EntrySort.recentlyUsed => l10n.sortRecentlyUsed,
    EntrySort.recentlyUpdated => l10n.sortRecentlyUpdated,
  };
}

extension StrengthLevelL10n on StrengthLevel {
  String localizedLabel(AppLocalizations l10n) => switch (this) {
    StrengthLevel.empty => l10n.strengthEmpty,
    StrengthLevel.veryWeak => l10n.strengthVeryWeak,
    StrengthLevel.weak => l10n.strengthWeak,
    StrengthLevel.fair => l10n.strengthFair,
    StrengthLevel.strong => l10n.strengthStrong,
    StrengthLevel.excellent => l10n.strengthExcellent,
  };
}

extension AppThemeVariantL10n on AppThemeVariant {
  String localizedLabel(AppLocalizations l10n) => switch (this) {
    AppThemeVariant.midnight => l10n.themeMidnight,
    AppThemeVariant.obsidian => l10n.themeObsidian,
    AppThemeVariant.violet => l10n.themeViolet,
  };

  String localizedDescription(AppLocalizations l10n) => switch (this) {
    AppThemeVariant.midnight => l10n.themeMidnightDescription,
    AppThemeVariant.obsidian => l10n.themeObsidianDescription,
    AppThemeVariant.violet => l10n.themeVioletDescription,
  };
}

extension UiDensityL10n on UiDensity {
  String localizedLabel(AppLocalizations l10n) => switch (this) {
    UiDensity.comfortable => l10n.densityComfortable,
    UiDensity.compact => l10n.densityCompact,
  };
}

extension AppLanguageL10n on AppLanguage {
  String localizedLabel(AppLocalizations l10n) => switch (this) {
    AppLanguage.system => l10n.settingsLanguageSystem,
    AppLanguage.english => l10n.settingsLanguageEnglish,
    AppLanguage.italian => l10n.settingsLanguageItalian,
  };
}

extension VaultExceptionL10n on VaultException {
  String localizedTitle(AppLocalizations l10n) => switch (kind) {
    VaultErrorKind.notFound => l10n.errorNotFoundTitle,
    VaultErrorKind.invalidPassword => l10n.errorInvalidPasswordTitle,
    VaultErrorKind.badSignature => l10n.errorBadSignatureTitle,
    VaultErrorKind.unsupportedVersion => l10n.errorUnsupportedVersionTitle,
    VaultErrorKind.truncated => l10n.errorTruncatedTitle,
    VaultErrorKind.malformedPayload => l10n.errorMalformedTitle,
    VaultErrorKind.permissionDenied => l10n.errorPermissionTitle,
    VaultErrorKind.ioFailure => l10n.errorIoTitle,
  };

  String localizedHint(AppLocalizations l10n) => switch (kind) {
    VaultErrorKind.notFound => l10n.errorNotFoundHint,
    VaultErrorKind.invalidPassword => l10n.errorInvalidPasswordHint,
    VaultErrorKind.badSignature => l10n.errorBadSignatureHint,
    VaultErrorKind.unsupportedVersion => l10n.errorUnsupportedVersionHint,
    VaultErrorKind.truncated => l10n.errorTruncatedHint,
    VaultErrorKind.malformedPayload => l10n.errorMalformedHint,
    VaultErrorKind.permissionDenied => l10n.errorPermissionHint,
    VaultErrorKind.ioFailure => l10n.errorIoHint,
  };
}
