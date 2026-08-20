import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:intl/intl.dart' as intl;

import 'app_localizations_en.dart';
import 'app_localizations_it.dart';

// ignore_for_file: type=lint

/// Callers can lookup localized strings with an instance of AppLocalizations
/// returned by `AppLocalizations.of(context)`.
///
/// Applications need to include `AppLocalizations.delegate()` in their app's
/// `localizationDelegates` list, and the locales they support in the app's
/// `supportedLocales` list. For example:
///
/// ```dart
/// import 'generated/app_localizations.dart';
///
/// return MaterialApp(
///   localizationsDelegates: AppLocalizations.localizationsDelegates,
///   supportedLocales: AppLocalizations.supportedLocales,
///   home: MyApplicationHome(),
/// );
/// ```
///
/// ## Update pubspec.yaml
///
/// Please make sure to update your pubspec.yaml to include the following
/// packages:
///
/// ```yaml
/// dependencies:
///   # Internationalization support.
///   flutter_localizations:
///     sdk: flutter
///   intl: any # Use the pinned version from flutter_localizations
///
///   # Rest of dependencies
/// ```
///
/// ## iOS Applications
///
/// iOS applications define key application metadata, including supported
/// locales, in an Info.plist file that is built into the application bundle.
/// To configure the locales supported by your app, you’ll need to edit this
/// file.
///
/// First, open your project’s ios/Runner.xcworkspace Xcode workspace file.
/// Then, in the Project Navigator, open the Info.plist file under the Runner
/// project’s Runner folder.
///
/// Next, select the Information Property List item, select Add Item from the
/// Editor menu, then select Localizations from the pop-up menu.
///
/// Select and expand the newly-created Localizations item then, for each
/// locale your application supports, add a new item and select the locale
/// you wish to add from the pop-up menu in the Value field. This list should
/// be consistent with the languages listed in the AppLocalizations.supportedLocales
/// property.
abstract class AppLocalizations {
  AppLocalizations(String locale)
    : localeName = intl.Intl.canonicalizedLocale(locale.toString());

  final String localeName;

  static AppLocalizations of(BuildContext context) {
    return Localizations.of<AppLocalizations>(context, AppLocalizations)!;
  }

  static const LocalizationsDelegate<AppLocalizations> delegate =
      _AppLocalizationsDelegate();

  /// A list of this localizations delegate along with the default localizations
  /// delegates.
  ///
  /// Returns a list of localizations delegates containing this delegate along with
  /// GlobalMaterialLocalizations.delegate, GlobalCupertinoLocalizations.delegate,
  /// and GlobalWidgetsLocalizations.delegate.
  ///
  /// Additional delegates can be added by appending to this list in
  /// MaterialApp. This list does not have to be used at all if a custom list
  /// of delegates is preferred or required.
  static const List<LocalizationsDelegate<dynamic>> localizationsDelegates =
      <LocalizationsDelegate<dynamic>>[
        delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
      ];

  /// A list of this localizations delegate's supported locales.
  static const List<Locale> supportedLocales = <Locale>[
    Locale('en'),
    Locale('it'),
  ];

  /// No description provided for @appTagline.
  ///
  /// In en, this message translates to:
  /// **'A local, encrypted vault that stays yours.'**
  String get appTagline;

  /// No description provided for @appDescription.
  ///
  /// In en, this message translates to:
  /// **'BetterOnlyYours keeps credentials and secrets in a single encrypted file on this machine. No account, no sync, no telemetry — the vault is only readable with your master password.'**
  String get appDescription;

  /// No description provided for @navVault.
  ///
  /// In en, this message translates to:
  /// **'Vault'**
  String get navVault;

  /// No description provided for @navFavorites.
  ///
  /// In en, this message translates to:
  /// **'Favorites'**
  String get navFavorites;

  /// No description provided for @navRecent.
  ///
  /// In en, this message translates to:
  /// **'Recent'**
  String get navRecent;

  /// No description provided for @navGenerator.
  ///
  /// In en, this message translates to:
  /// **'Generator'**
  String get navGenerator;

  /// No description provided for @navSecurity.
  ///
  /// In en, this message translates to:
  /// **'Security'**
  String get navSecurity;

  /// No description provided for @navSettings.
  ///
  /// In en, this message translates to:
  /// **'Settings'**
  String get navSettings;

  /// No description provided for @navLockVault.
  ///
  /// In en, this message translates to:
  /// **'Lock vault'**
  String get navLockVault;

  /// No description provided for @navCollapse.
  ///
  /// In en, this message translates to:
  /// **'Collapse'**
  String get navCollapse;

  /// No description provided for @windowMinimize.
  ///
  /// In en, this message translates to:
  /// **'Minimize'**
  String get windowMinimize;

  /// No description provided for @windowMaximize.
  ///
  /// In en, this message translates to:
  /// **'Maximize'**
  String get windowMaximize;

  /// No description provided for @windowRestore.
  ///
  /// In en, this message translates to:
  /// **'Restore'**
  String get windowRestore;

  /// No description provided for @windowClose.
  ///
  /// In en, this message translates to:
  /// **'Close'**
  String get windowClose;

  /// No description provided for @actionSearchTooltip.
  ///
  /// In en, this message translates to:
  /// **'Command palette  ·  Ctrl+K'**
  String get actionSearchTooltip;

  /// No description provided for @actionNewEntryTooltip.
  ///
  /// In en, this message translates to:
  /// **'New entry  ·  Ctrl+N'**
  String get actionNewEntryTooltip;

  /// No description provided for @actionLockTooltip.
  ///
  /// In en, this message translates to:
  /// **'Lock vault  ·  Ctrl+L'**
  String get actionLockTooltip;

  /// No description provided for @statusUnlockedEntries.
  ///
  /// In en, this message translates to:
  /// **'Unlocked · {entries}'**
  String statusUnlockedEntries(String entries);

  /// No description provided for @statusEncrypting.
  ///
  /// In en, this message translates to:
  /// **'Encrypting…'**
  String get statusEncrypting;

  /// No description provided for @statusSaveFailed.
  ///
  /// In en, this message translates to:
  /// **'Save failed — click to retry'**
  String get statusSaveFailed;

  /// No description provided for @statusNoChanges.
  ///
  /// In en, this message translates to:
  /// **'No changes yet'**
  String get statusNoChanges;

  /// No description provided for @statusSavedAgo.
  ///
  /// In en, this message translates to:
  /// **'Saved {when}'**
  String statusSavedAgo(String when);

  /// No description provided for @statusClipboardClears.
  ///
  /// In en, this message translates to:
  /// **'{label} clears in {seconds}s'**
  String statusClipboardClears(String label, int seconds);

  /// No description provided for @statusKeep.
  ///
  /// In en, this message translates to:
  /// **'keep'**
  String get statusKeep;

  /// No description provided for @statusAutoLockOn.
  ///
  /// In en, this message translates to:
  /// **'Auto-lock {minutes}m'**
  String statusAutoLockOn(int minutes);

  /// No description provided for @statusAutoLockOff.
  ///
  /// In en, this message translates to:
  /// **'Auto-lock off'**
  String get statusAutoLockOff;

  /// No description provided for @authLocalVault.
  ///
  /// In en, this message translates to:
  /// **'Local encrypted vault'**
  String get authLocalVault;

  /// No description provided for @authVaultLocked.
  ///
  /// In en, this message translates to:
  /// **'Vault locked'**
  String get authVaultLocked;

  /// No description provided for @authUnlockPrompt.
  ///
  /// In en, this message translates to:
  /// **'Enter your master password to decrypt this vault.'**
  String get authUnlockPrompt;

  /// No description provided for @authMasterPassword.
  ///
  /// In en, this message translates to:
  /// **'Master password'**
  String get authMasterPassword;

  /// No description provided for @authUnlockButton.
  ///
  /// In en, this message translates to:
  /// **'Unlock vault'**
  String get authUnlockButton;

  /// No description provided for @authCapsLockOn.
  ///
  /// In en, this message translates to:
  /// **'Caps Lock is on'**
  String get authCapsLockOn;

  /// No description provided for @authShowPassword.
  ///
  /// In en, this message translates to:
  /// **'Show password'**
  String get authShowPassword;

  /// No description provided for @authHidePassword.
  ///
  /// In en, this message translates to:
  /// **'Hide password'**
  String get authHidePassword;

  /// No description provided for @authLocalOnlyNote.
  ///
  /// In en, this message translates to:
  /// **'Everything stays on this machine. There is no account, no sync and no password reset.'**
  String get authLocalOnlyNote;

  /// No description provided for @setupTitle.
  ///
  /// In en, this message translates to:
  /// **'Set up your vault'**
  String get setupTitle;

  /// No description provided for @setupHeadline.
  ///
  /// In en, this message translates to:
  /// **'One password, one file, no cloud'**
  String get setupHeadline;

  /// No description provided for @setupIntro.
  ///
  /// In en, this message translates to:
  /// **'Your entries are encrypted with AES-256-GCM using a key derived from the master password. The vault file never leaves this machine, and nothing else can decrypt it.'**
  String get setupIntro;

  /// No description provided for @setupPasswordHint.
  ///
  /// In en, this message translates to:
  /// **'Choose something long and memorable'**
  String get setupPasswordHint;

  /// No description provided for @setupConfirmLabel.
  ///
  /// In en, this message translates to:
  /// **'Confirm master password'**
  String get setupConfirmLabel;

  /// No description provided for @setupConfirmHint.
  ///
  /// In en, this message translates to:
  /// **'Type it once more'**
  String get setupConfirmHint;

  /// No description provided for @setupRequirementLength.
  ///
  /// In en, this message translates to:
  /// **'At least {count} characters'**
  String setupRequirementLength(int count);

  /// No description provided for @setupRequirementMatch.
  ///
  /// In en, this message translates to:
  /// **'Both entries match'**
  String get setupRequirementMatch;

  /// No description provided for @setupAcknowledgement.
  ///
  /// In en, this message translates to:
  /// **'I understand this password cannot be recovered or reset. If I lose it, the vault stays encrypted forever — so I will keep a backup of the vault file somewhere safe.'**
  String get setupAcknowledgement;

  /// No description provided for @setupCreateButton.
  ///
  /// In en, this message translates to:
  /// **'Create vault'**
  String get setupCreateButton;

  /// No description provided for @setupVaultFile.
  ///
  /// In en, this message translates to:
  /// **'Vault file: {path}'**
  String setupVaultFile(String path);

  /// No description provided for @setupErrorFields.
  ///
  /// In en, this message translates to:
  /// **'Use at least {count} characters.'**
  String setupErrorFields(int count);

  /// No description provided for @setupErrorMismatch.
  ///
  /// In en, this message translates to:
  /// **'The two passwords do not match.'**
  String get setupErrorMismatch;

  /// No description provided for @setupErrorAcknowledge.
  ///
  /// In en, this message translates to:
  /// **'Confirm you understand there is no password recovery.'**
  String get setupErrorAcknowledge;

  /// No description provided for @setupHide.
  ///
  /// In en, this message translates to:
  /// **'Hide'**
  String get setupHide;

  /// No description provided for @setupShow.
  ///
  /// In en, this message translates to:
  /// **'Show'**
  String get setupShow;

  /// No description provided for @timeNever.
  ///
  /// In en, this message translates to:
  /// **'never'**
  String get timeNever;

  /// No description provided for @timeJustNow.
  ///
  /// In en, this message translates to:
  /// **'just now'**
  String get timeJustNow;

  /// No description provided for @timeMinutesAgo.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, =1{1 minute ago} other{{count} minutes ago}}'**
  String timeMinutesAgo(int count);

  /// No description provided for @timeHoursAgo.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, =1{1 hour ago} other{{count} hours ago}}'**
  String timeHoursAgo(int count);

  /// No description provided for @timeDaysAgo.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, =1{1 day ago} other{{count} days ago}}'**
  String timeDaysAgo(int count);

  /// No description provided for @timeWeeksAgo.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, =1{1 week ago} other{{count} weeks ago}}'**
  String timeWeeksAgo(int count);

  /// No description provided for @timeMonthsAgo.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, =1{1 month ago} other{{count} months ago}}'**
  String timeMonthsAgo(int count);

  /// No description provided for @timeYearsAgo.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, =1{1 year ago} other{{count} years ago}}'**
  String timeYearsAgo(int count);

  /// No description provided for @entriesCount.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, =1{1 entry} other{{count} entries}}'**
  String entriesCount(int count);

  /// No description provided for @previousPasswordsCount.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, =1{1 previous password} other{{count} previous passwords}}'**
  String previousPasswordsCount(int count);

  /// No description provided for @listSearchHint.
  ///
  /// In en, this message translates to:
  /// **'Search vault...'**
  String get listSearchHint;

  /// No description provided for @listSearchSemantics.
  ///
  /// In en, this message translates to:
  /// **'Search vault'**
  String get listSearchSemantics;

  /// No description provided for @listClearSearch.
  ///
  /// In en, this message translates to:
  /// **'Clear search'**
  String get listClearSearch;

  /// No description provided for @listSortTooltip.
  ///
  /// In en, this message translates to:
  /// **'Sort and filter'**
  String get listSortTooltip;

  /// No description provided for @listFavoritesOnly.
  ///
  /// In en, this message translates to:
  /// **'Favorites only'**
  String get listFavoritesOnly;

  /// No description provided for @listClearTagFilter.
  ///
  /// In en, this message translates to:
  /// **'Clear tag filter ({tag})'**
  String listClearTagFilter(String tag);

  /// No description provided for @listShownOfTotal.
  ///
  /// In en, this message translates to:
  /// **'{shown} of {total}'**
  String listShownOfTotal(int shown, int total);

  /// No description provided for @listClearFilters.
  ///
  /// In en, this message translates to:
  /// **'Clear filters'**
  String get listClearFilters;

  /// No description provided for @listEntryActions.
  ///
  /// In en, this message translates to:
  /// **'Entry actions'**
  String get listEntryActions;

  /// No description provided for @emptySearchTitle.
  ///
  /// In en, this message translates to:
  /// **'Nothing matched'**
  String get emptySearchTitle;

  /// No description provided for @emptySearchMessage.
  ///
  /// In en, this message translates to:
  /// **'No entry matches the current search or filters.'**
  String get emptySearchMessage;

  /// No description provided for @emptySearchCreate.
  ///
  /// In en, this message translates to:
  /// **'Create \"{query}\"'**
  String emptySearchCreate(String query);

  /// No description provided for @emptyFavoritesTitle.
  ///
  /// In en, this message translates to:
  /// **'No favorites yet'**
  String get emptyFavoritesTitle;

  /// No description provided for @emptyFavoritesMessage.
  ///
  /// In en, this message translates to:
  /// **'Star the entries you reach for daily and they will appear here.'**
  String get emptyFavoritesMessage;

  /// No description provided for @emptyFavoritesHint.
  ///
  /// In en, this message translates to:
  /// **'Ctrl+D toggles a favorite on the selected entry.'**
  String get emptyFavoritesHint;

  /// No description provided for @emptyRecentTitle.
  ///
  /// In en, this message translates to:
  /// **'Nothing opened yet'**
  String get emptyRecentTitle;

  /// No description provided for @emptyRecentMessage.
  ///
  /// In en, this message translates to:
  /// **'Entries you open appear here, newest first. The history lives inside the encrypted vault.'**
  String get emptyRecentMessage;

  /// No description provided for @emptyVaultTitle.
  ///
  /// In en, this message translates to:
  /// **'Your vault is empty'**
  String get emptyVaultTitle;

  /// No description provided for @emptyVaultMessage.
  ///
  /// In en, this message translates to:
  /// **'Create your first entry — it is encrypted the moment you save.'**
  String get emptyVaultMessage;

  /// No description provided for @emptyVaultHint.
  ///
  /// In en, this message translates to:
  /// **'Ctrl+N creates an entry from anywhere.'**
  String get emptyVaultHint;

  /// No description provided for @newEntry.
  ///
  /// In en, this message translates to:
  /// **'New entry'**
  String get newEntry;

  /// No description provided for @sortTitleAsc.
  ///
  /// In en, this message translates to:
  /// **'Name (A-Z)'**
  String get sortTitleAsc;

  /// No description provided for @sortTitleDesc.
  ///
  /// In en, this message translates to:
  /// **'Name (Z-A)'**
  String get sortTitleDesc;

  /// No description provided for @sortRecentlyUsed.
  ///
  /// In en, this message translates to:
  /// **'Recently opened'**
  String get sortRecentlyUsed;

  /// No description provided for @sortRecentlyUpdated.
  ///
  /// In en, this message translates to:
  /// **'Recently modified'**
  String get sortRecentlyUpdated;

  /// No description provided for @dashboardSubtitle.
  ///
  /// In en, this message translates to:
  /// **'{entries} encrypted on this machine. Select an entry to view it, or start something new.'**
  String dashboardSubtitle(String entries);

  /// No description provided for @dashboardReadyTitle.
  ///
  /// In en, this message translates to:
  /// **'Your vault is ready'**
  String get dashboardReadyTitle;

  /// No description provided for @dashboardReadyMessage.
  ///
  /// In en, this message translates to:
  /// **'Nothing is stored yet. Create your first entry — it is encrypted locally the moment you save it.'**
  String get dashboardReadyMessage;

  /// No description provided for @dashboardOpenGenerator.
  ///
  /// In en, this message translates to:
  /// **'Open generator'**
  String get dashboardOpenGenerator;

  /// No description provided for @dashboardShortcutHint.
  ///
  /// In en, this message translates to:
  /// **'Ctrl+N new entry · Ctrl+K command palette · Ctrl+L lock'**
  String get dashboardShortcutHint;

  /// No description provided for @dashboardSearch.
  ///
  /// In en, this message translates to:
  /// **'Search'**
  String get dashboardSearch;

  /// No description provided for @dashboardGeneratePassword.
  ///
  /// In en, this message translates to:
  /// **'Generate password'**
  String get dashboardGeneratePassword;

  /// No description provided for @statEntries.
  ///
  /// In en, this message translates to:
  /// **'Entries'**
  String get statEntries;

  /// No description provided for @statEntriesAllStructured.
  ///
  /// In en, this message translates to:
  /// **'All structured'**
  String get statEntriesAllStructured;

  /// No description provided for @statEntriesLegacy.
  ///
  /// In en, this message translates to:
  /// **'{count} legacy'**
  String statEntriesLegacy(int count);

  /// No description provided for @statFavorites.
  ///
  /// In en, this message translates to:
  /// **'Favorites'**
  String get statFavorites;

  /// No description provided for @statFavoritesCaption.
  ///
  /// In en, this message translates to:
  /// **'Starred for quick access'**
  String get statFavoritesCaption;

  /// No description provided for @statTags.
  ///
  /// In en, this message translates to:
  /// **'Tags'**
  String get statTags;

  /// No description provided for @statTagsEmpty.
  ///
  /// In en, this message translates to:
  /// **'Add tags to group entries'**
  String get statTagsEmpty;

  /// No description provided for @statHealth.
  ///
  /// In en, this message translates to:
  /// **'Password health'**
  String get statHealth;

  /// No description provided for @statHealthClean.
  ///
  /// In en, this message translates to:
  /// **'No weak or reused passwords'**
  String get statHealthClean;

  /// No description provided for @statHealthIssues.
  ///
  /// In en, this message translates to:
  /// **'{weak} weak · {reused} reused'**
  String statHealthIssues(int weak, int reused);

  /// No description provided for @dashboardRecentlyOpened.
  ///
  /// In en, this message translates to:
  /// **'Recently opened'**
  String get dashboardRecentlyOpened;

  /// No description provided for @dashboardRecentlyOpenedEmpty.
  ///
  /// In en, this message translates to:
  /// **'Open an entry and it shows up here.'**
  String get dashboardRecentlyOpenedEmpty;

  /// No description provided for @dashboardRecentlyModified.
  ///
  /// In en, this message translates to:
  /// **'Recently modified'**
  String get dashboardRecentlyModified;

  /// No description provided for @dashboardRecentlyModifiedEmpty.
  ///
  /// In en, this message translates to:
  /// **'Entries you edit appear here with their timestamp.'**
  String get dashboardRecentlyModifiedEmpty;

  /// No description provided for @dashboardSecuritySnapshot.
  ///
  /// In en, this message translates to:
  /// **'Security snapshot'**
  String get dashboardSecuritySnapshot;

  /// No description provided for @dashboardSecuritySnapshotSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Current protection settings for this vault.'**
  String get dashboardSecuritySnapshotSubtitle;

  /// No description provided for @dashboardSecurityCenter.
  ///
  /// In en, this message translates to:
  /// **'Security center'**
  String get dashboardSecurityCenter;

  /// No description provided for @infoEncryption.
  ///
  /// In en, this message translates to:
  /// **'Encryption'**
  String get infoEncryption;

  /// No description provided for @infoAutoLock.
  ///
  /// In en, this message translates to:
  /// **'Auto-lock'**
  String get infoAutoLock;

  /// No description provided for @infoAutoLockOff.
  ///
  /// In en, this message translates to:
  /// **'Off — minimizing never locks the vault'**
  String get infoAutoLockOff;

  /// No description provided for @infoAutoLockAfter.
  ///
  /// In en, this message translates to:
  /// **'After {minutes} minutes of inactivity'**
  String infoAutoLockAfter(int minutes);

  /// No description provided for @infoClipboard.
  ///
  /// In en, this message translates to:
  /// **'Clipboard'**
  String get infoClipboard;

  /// No description provided for @infoClipboardKeep.
  ///
  /// In en, this message translates to:
  /// **'Kept until you replace it'**
  String get infoClipboardKeep;

  /// No description provided for @infoClipboardClear.
  ///
  /// In en, this message translates to:
  /// **'Cleared {seconds} seconds after copying'**
  String infoClipboardClear(int seconds);

  /// No description provided for @infoLastSaved.
  ///
  /// In en, this message translates to:
  /// **'Last saved'**
  String get infoLastSaved;

  /// No description provided for @dashboardLegacyNote.
  ///
  /// In en, this message translates to:
  /// **'{count} entries still use the legacy plain-text format. Open one and fill in a field to upgrade it.'**
  String dashboardLegacyNote(int count);

  /// No description provided for @fieldName.
  ///
  /// In en, this message translates to:
  /// **'Name'**
  String get fieldName;

  /// No description provided for @fieldNameHint.
  ///
  /// In en, this message translates to:
  /// **'Entry name'**
  String get fieldNameHint;

  /// No description provided for @fieldUsername.
  ///
  /// In en, this message translates to:
  /// **'Username or email'**
  String get fieldUsername;

  /// No description provided for @fieldNotSet.
  ///
  /// In en, this message translates to:
  /// **'Not set'**
  String get fieldNotSet;

  /// No description provided for @fieldPassword.
  ///
  /// In en, this message translates to:
  /// **'Password'**
  String get fieldPassword;

  /// No description provided for @fieldWebsite.
  ///
  /// In en, this message translates to:
  /// **'Website'**
  String get fieldWebsite;

  /// No description provided for @fieldNotes.
  ///
  /// In en, this message translates to:
  /// **'Notes'**
  String get fieldNotes;

  /// No description provided for @fieldNotesHint.
  ///
  /// In en, this message translates to:
  /// **'Recovery codes, hints, anything else...'**
  String get fieldNotesHint;

  /// No description provided for @fieldTags.
  ///
  /// In en, this message translates to:
  /// **'TAGS'**
  String get fieldTags;

  /// No description provided for @fieldCustomFields.
  ///
  /// In en, this message translates to:
  /// **'CUSTOM FIELDS'**
  String get fieldCustomFields;

  /// No description provided for @fieldDetails.
  ///
  /// In en, this message translates to:
  /// **'DETAILS'**
  String get fieldDetails;

  /// No description provided for @fieldLabel.
  ///
  /// In en, this message translates to:
  /// **'Label'**
  String get fieldLabel;

  /// No description provided for @fieldValue.
  ///
  /// In en, this message translates to:
  /// **'Value'**
  String get fieldValue;

  /// No description provided for @fieldTagName.
  ///
  /// In en, this message translates to:
  /// **'Tag name'**
  String get fieldTagName;

  /// No description provided for @fieldAddTag.
  ///
  /// In en, this message translates to:
  /// **'Add tag'**
  String get fieldAddTag;

  /// No description provided for @fieldSuggestions.
  ///
  /// In en, this message translates to:
  /// **'Suggestions'**
  String get fieldSuggestions;

  /// No description provided for @fieldAddField.
  ///
  /// In en, this message translates to:
  /// **'Add field'**
  String get fieldAddField;

  /// No description provided for @fieldCustomFieldsEmpty.
  ///
  /// In en, this message translates to:
  /// **'Recovery codes, security questions, API keys — anything that belongs to this entry.'**
  String get fieldCustomFieldsEmpty;

  /// No description provided for @fieldCopyValue.
  ///
  /// In en, this message translates to:
  /// **'Copy value'**
  String get fieldCopyValue;

  /// No description provided for @fieldHideValue.
  ///
  /// In en, this message translates to:
  /// **'Hide this value'**
  String get fieldHideValue;

  /// No description provided for @fieldShowValue.
  ///
  /// In en, this message translates to:
  /// **'Stop hiding this value'**
  String get fieldShowValue;

  /// No description provided for @fieldRemoveField.
  ///
  /// In en, this message translates to:
  /// **'Remove field'**
  String get fieldRemoveField;

  /// No description provided for @fieldGenericField.
  ///
  /// In en, this message translates to:
  /// **'Field'**
  String get fieldGenericField;

  /// No description provided for @detailBackToList.
  ///
  /// In en, this message translates to:
  /// **'Back to list'**
  String get detailBackToList;

  /// No description provided for @detailAddFavorite.
  ///
  /// In en, this message translates to:
  /// **'Add to favorites  ·  Ctrl+D'**
  String get detailAddFavorite;

  /// No description provided for @detailRemoveFavorite.
  ///
  /// In en, this message translates to:
  /// **'Remove from favorites  ·  Ctrl+D'**
  String get detailRemoveFavorite;

  /// No description provided for @detailOpenWebsite.
  ///
  /// In en, this message translates to:
  /// **'Open website'**
  String get detailOpenWebsite;

  /// No description provided for @detailOpenInBrowser.
  ///
  /// In en, this message translates to:
  /// **'Open in browser'**
  String get detailOpenInBrowser;

  /// No description provided for @detailCopyPasswordShortcut.
  ///
  /// In en, this message translates to:
  /// **'Copy password  ·  Ctrl+Shift+C'**
  String get detailCopyPasswordShortcut;

  /// No description provided for @detailCopyUsernameShortcut.
  ///
  /// In en, this message translates to:
  /// **'Copy username  ·  Ctrl+Shift+U'**
  String get detailCopyUsernameShortcut;

  /// No description provided for @detailUnsavedChanges.
  ///
  /// In en, this message translates to:
  /// **'Unsaved changes'**
  String get detailUnsavedChanges;

  /// No description provided for @detailRevert.
  ///
  /// In en, this message translates to:
  /// **'Revert'**
  String get detailRevert;

  /// No description provided for @detailSave.
  ///
  /// In en, this message translates to:
  /// **'Save'**
  String get detailSave;

  /// No description provided for @detailSaved.
  ///
  /// In en, this message translates to:
  /// **'Saved'**
  String get detailSaved;

  /// No description provided for @detailNameRequired.
  ///
  /// In en, this message translates to:
  /// **'A name is required.'**
  String get detailNameRequired;

  /// No description provided for @detailNameTaken.
  ///
  /// In en, this message translates to:
  /// **'Another entry already uses that name.'**
  String get detailNameTaken;

  /// No description provided for @detailLegacyBanner.
  ///
  /// In en, this message translates to:
  /// **'Imported from an older vault, where entries were plain text. Your text is in Notes; filling any other field upgrades this entry to the structured format on the next save.'**
  String get detailLegacyBanner;

  /// No description provided for @detailChangesDiscarded.
  ///
  /// In en, this message translates to:
  /// **'Changes discarded'**
  String get detailChangesDiscarded;

  /// No description provided for @detailUnsavedPrompt.
  ///
  /// In en, this message translates to:
  /// **'You edited this entry but did not save. Keep the changes or drop them?'**
  String get detailUnsavedPrompt;

  /// No description provided for @detailSaveChanges.
  ///
  /// In en, this message translates to:
  /// **'Save changes'**
  String get detailSaveChanges;

  /// No description provided for @detailDiscard.
  ///
  /// In en, this message translates to:
  /// **'Discard'**
  String get detailDiscard;

  /// No description provided for @detailPasswordGenerated.
  ///
  /// In en, this message translates to:
  /// **'Password generated'**
  String get detailPasswordGenerated;

  /// No description provided for @detailPasswordGeneratedDetail.
  ///
  /// In en, this message translates to:
  /// **'Not saved yet — press Ctrl+S to store it.'**
  String get detailPasswordGeneratedDetail;

  /// No description provided for @detailCreated.
  ///
  /// In en, this message translates to:
  /// **'Created'**
  String get detailCreated;

  /// No description provided for @detailLastModified.
  ///
  /// In en, this message translates to:
  /// **'Last modified'**
  String get detailLastModified;

  /// No description provided for @detailLastOpened.
  ///
  /// In en, this message translates to:
  /// **'Last opened'**
  String get detailLastOpened;

  /// No description provided for @detailStorageFormat.
  ///
  /// In en, this message translates to:
  /// **'Storage format'**
  String get detailStorageFormat;

  /// No description provided for @detailUnknownLegacy.
  ///
  /// In en, this message translates to:
  /// **'Unknown (legacy entry)'**
  String get detailUnknownLegacy;

  /// No description provided for @detailFormatLegacy.
  ///
  /// In en, this message translates to:
  /// **'Legacy plain text'**
  String get detailFormatLegacy;

  /// No description provided for @detailFormatStructured.
  ///
  /// In en, this message translates to:
  /// **'Structured (BOY1)'**
  String get detailFormatStructured;

  /// No description provided for @secretReveal.
  ///
  /// In en, this message translates to:
  /// **'Reveal password'**
  String get secretReveal;

  /// No description provided for @secretHide.
  ///
  /// In en, this message translates to:
  /// **'Hide password'**
  String get secretHide;

  /// No description provided for @secretCopyShortcut.
  ///
  /// In en, this message translates to:
  /// **'Copy password  ·  Ctrl+Shift+C'**
  String get secretCopyShortcut;

  /// No description provided for @secretGenerate.
  ///
  /// In en, this message translates to:
  /// **'Generate a new password'**
  String get secretGenerate;

  /// No description provided for @createDialogTitle.
  ///
  /// In en, this message translates to:
  /// **'New entry'**
  String get createDialogTitle;

  /// No description provided for @createDialogSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Stored encrypted in your local vault.'**
  String get createDialogSubtitle;

  /// No description provided for @createDialogNameHint.
  ///
  /// In en, this message translates to:
  /// **'GitHub, Bank, Home server...'**
  String get createDialogNameHint;

  /// No description provided for @createDialogOptional.
  ///
  /// In en, this message translates to:
  /// **'Optional'**
  String get createDialogOptional;

  /// No description provided for @createDialogPasswordHint.
  ///
  /// In en, this message translates to:
  /// **'Optional — generate one'**
  String get createDialogPasswordHint;

  /// No description provided for @createDialogFooter.
  ///
  /// In en, this message translates to:
  /// **'You can add notes, tags and custom fields after creating it.'**
  String get createDialogFooter;

  /// No description provided for @createDialogNameEmpty.
  ///
  /// In en, this message translates to:
  /// **'Give the entry a name.'**
  String get createDialogNameEmpty;

  /// No description provided for @createDialogNameExists.
  ///
  /// In en, this message translates to:
  /// **'An entry named \"{title}\" already exists.'**
  String createDialogNameExists(String title);

  /// No description provided for @createDialogSaveFailed.
  ///
  /// In en, this message translates to:
  /// **'The entry could not be saved. Check the Security tab.'**
  String get createDialogSaveFailed;

  /// No description provided for @createDialogCreate.
  ///
  /// In en, this message translates to:
  /// **'Create entry'**
  String get createDialogCreate;

  /// No description provided for @createDialogEntryCreated.
  ///
  /// In en, this message translates to:
  /// **'Entry created'**
  String get createDialogEntryCreated;

  /// No description provided for @commonCancel.
  ///
  /// In en, this message translates to:
  /// **'Cancel'**
  String get commonCancel;

  /// No description provided for @commonConfirm.
  ///
  /// In en, this message translates to:
  /// **'Confirm'**
  String get commonConfirm;

  /// No description provided for @commonClose.
  ///
  /// In en, this message translates to:
  /// **'Close'**
  String get commonClose;

  /// No description provided for @commonRetry.
  ///
  /// In en, this message translates to:
  /// **'Retry'**
  String get commonRetry;

  /// No description provided for @commonUndo.
  ///
  /// In en, this message translates to:
  /// **'Undo'**
  String get commonUndo;

  /// No description provided for @commonOpen.
  ///
  /// In en, this message translates to:
  /// **'Open'**
  String get commonOpen;

  /// No description provided for @commonDelete.
  ///
  /// In en, this message translates to:
  /// **'Delete'**
  String get commonDelete;

  /// No description provided for @commonClear.
  ///
  /// In en, this message translates to:
  /// **'Clear'**
  String get commonClear;

  /// No description provided for @historyTitle.
  ///
  /// In en, this message translates to:
  /// **'PASSWORD HISTORY'**
  String get historyTitle;

  /// No description provided for @historyEmptyHint.
  ///
  /// In en, this message translates to:
  /// **'When you change this password, the previous one is kept here so a mistaken change can be undone.'**
  String get historyEmptyHint;

  /// No description provided for @historyCountOf.
  ///
  /// In en, this message translates to:
  /// **'{count} of {max}'**
  String historyCountOf(int count, int max);

  /// No description provided for @historyClear.
  ///
  /// In en, this message translates to:
  /// **'Clear history'**
  String get historyClear;

  /// No description provided for @historyCap.
  ///
  /// In en, this message translates to:
  /// **'History lives inside the encrypted vault and is capped at {max} entries.'**
  String historyCap(int max);

  /// No description provided for @historyClearTitle.
  ///
  /// In en, this message translates to:
  /// **'Clear password history?'**
  String get historyClearTitle;

  /// No description provided for @historyClearMessage.
  ///
  /// In en, this message translates to:
  /// **'Every superseded password of this entry is deleted from the vault. This cannot be undone.'**
  String get historyClearMessage;

  /// No description provided for @historyCleared.
  ///
  /// In en, this message translates to:
  /// **'Password history cleared'**
  String get historyCleared;

  /// No description provided for @historyReplacedAt.
  ///
  /// In en, this message translates to:
  /// **'Replaced {relative} · {absolute}'**
  String historyReplacedAt(String relative, String absolute);

  /// No description provided for @historyReveal.
  ///
  /// In en, this message translates to:
  /// **'Reveal'**
  String get historyReveal;

  /// No description provided for @historyHide.
  ///
  /// In en, this message translates to:
  /// **'Hide'**
  String get historyHide;

  /// No description provided for @historyCopy.
  ///
  /// In en, this message translates to:
  /// **'Copy this password'**
  String get historyCopy;

  /// No description provided for @historyRestore.
  ///
  /// In en, this message translates to:
  /// **'Use this password again'**
  String get historyRestore;

  /// No description provided for @historyForget.
  ///
  /// In en, this message translates to:
  /// **'Forget this password'**
  String get historyForget;

  /// No description provided for @historyRestoreTitle.
  ///
  /// In en, this message translates to:
  /// **'Use this password again?'**
  String get historyRestoreTitle;

  /// No description provided for @historyRestoreMessage.
  ///
  /// In en, this message translates to:
  /// **'The entry goes back to this password. The one in use now is kept in the history, so you can switch back.'**
  String get historyRestoreMessage;

  /// No description provided for @historyRestoreConfirm.
  ///
  /// In en, this message translates to:
  /// **'Restore'**
  String get historyRestoreConfirm;

  /// No description provided for @historyRestored.
  ///
  /// In en, this message translates to:
  /// **'Password restored'**
  String get historyRestored;

  /// No description provided for @historyForgotten.
  ///
  /// In en, this message translates to:
  /// **'Removed from history'**
  String get historyForgotten;

  /// No description provided for @historyPreviousPassword.
  ///
  /// In en, this message translates to:
  /// **'Previous password'**
  String get historyPreviousPassword;

  /// No description provided for @paletteHint.
  ///
  /// In en, this message translates to:
  /// **'Search entries or run a command...'**
  String get paletteHint;

  /// No description provided for @paletteNoMatches.
  ///
  /// In en, this message translates to:
  /// **'No matches for \"{query}\"'**
  String paletteNoMatches(String query);

  /// No description provided for @paletteCreateNamed.
  ///
  /// In en, this message translates to:
  /// **'Create an entry named \"{query}\"'**
  String paletteCreateNamed(String query);

  /// No description provided for @paletteNavigate.
  ///
  /// In en, this message translates to:
  /// **'navigate'**
  String get paletteNavigate;

  /// No description provided for @paletteOpen.
  ///
  /// In en, this message translates to:
  /// **'open'**
  String get paletteOpen;

  /// No description provided for @paletteCopyPassword.
  ///
  /// In en, this message translates to:
  /// **'copy password'**
  String get paletteCopyPassword;

  /// No description provided for @paletteCopyUsername.
  ///
  /// In en, this message translates to:
  /// **'copy username'**
  String get paletteCopyUsername;

  /// No description provided for @paletteEnterToOpen.
  ///
  /// In en, this message translates to:
  /// **'Enter to open'**
  String get paletteEnterToOpen;

  /// No description provided for @paletteCommandNewEntry.
  ///
  /// In en, this message translates to:
  /// **'Create a credential'**
  String get paletteCommandNewEntry;

  /// No description provided for @paletteCommandGenerator.
  ///
  /// In en, this message translates to:
  /// **'Generate a password or passphrase'**
  String get paletteCommandGenerator;

  /// No description provided for @paletteCommandLock.
  ///
  /// In en, this message translates to:
  /// **'Clear the session and return to the lock screen'**
  String get paletteCommandLock;

  /// No description provided for @paletteCommandFavorites.
  ///
  /// In en, this message translates to:
  /// **'Jump to starred entries'**
  String get paletteCommandFavorites;

  /// No description provided for @paletteCommandRecent.
  ///
  /// In en, this message translates to:
  /// **'Entries you opened last'**
  String get paletteCommandRecent;

  /// No description provided for @paletteCommandSecurity.
  ///
  /// In en, this message translates to:
  /// **'Encryption, vault file and protection settings'**
  String get paletteCommandSecurity;

  /// No description provided for @paletteCommandSettings.
  ///
  /// In en, this message translates to:
  /// **'Appearance, security, keyboard, vault'**
  String get paletteCommandSettings;

  /// No description provided for @generatorTitle.
  ///
  /// In en, this message translates to:
  /// **'Password generator'**
  String get generatorTitle;

  /// No description provided for @generatorSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Everything is generated locally with the operating system\'s cryptographic random source.'**
  String get generatorSubtitle;

  /// No description provided for @generatorModeCharacters.
  ///
  /// In en, this message translates to:
  /// **'Random characters'**
  String get generatorModeCharacters;

  /// No description provided for @generatorModePassphrase.
  ///
  /// In en, this message translates to:
  /// **'Passphrase'**
  String get generatorModePassphrase;

  /// No description provided for @generatorCopy.
  ///
  /// In en, this message translates to:
  /// **'Copy'**
  String get generatorCopy;

  /// No description provided for @generatorRegenerate.
  ///
  /// In en, this message translates to:
  /// **'Regenerate'**
  String get generatorRegenerate;

  /// No description provided for @generatorRngNote.
  ///
  /// In en, this message translates to:
  /// **'Generated with a cryptographic RNG'**
  String get generatorRngNote;

  /// No description provided for @generatorLength.
  ///
  /// In en, this message translates to:
  /// **'Length'**
  String get generatorLength;

  /// No description provided for @generatorLowercase.
  ///
  /// In en, this message translates to:
  /// **'Lowercase (a-z)'**
  String get generatorLowercase;

  /// No description provided for @generatorUppercase.
  ///
  /// In en, this message translates to:
  /// **'Uppercase (A-Z)'**
  String get generatorUppercase;

  /// No description provided for @generatorDigits.
  ///
  /// In en, this message translates to:
  /// **'Digits (0-9)'**
  String get generatorDigits;

  /// No description provided for @generatorSymbols.
  ///
  /// In en, this message translates to:
  /// **'Symbols (!@#...)'**
  String get generatorSymbols;

  /// No description provided for @generatorAvoidAmbiguous.
  ///
  /// In en, this message translates to:
  /// **'Avoid look-alike characters'**
  String get generatorAvoidAmbiguous;

  /// No description provided for @generatorNoClassWarning.
  ///
  /// In en, this message translates to:
  /// **'At least one character set is required — letters and digits are used until you pick one.'**
  String get generatorNoClassWarning;

  /// No description provided for @generatorWords.
  ///
  /// In en, this message translates to:
  /// **'Words'**
  String get generatorWords;

  /// No description provided for @generatorSeparator.
  ///
  /// In en, this message translates to:
  /// **'Separator'**
  String get generatorSeparator;

  /// No description provided for @generatorCapitalize.
  ///
  /// In en, this message translates to:
  /// **'Capitalize words'**
  String get generatorCapitalize;

  /// No description provided for @generatorAppendNumber.
  ///
  /// In en, this message translates to:
  /// **'Append a number'**
  String get generatorAppendNumber;

  /// No description provided for @generatorWordListNote.
  ///
  /// In en, this message translates to:
  /// **'{words} word list · each word adds about {bits} bits of entropy.'**
  String generatorWordListNote(int words, int bits);

  /// No description provided for @generatorSessionTitle.
  ///
  /// In en, this message translates to:
  /// **'This session'**
  String get generatorSessionTitle;

  /// No description provided for @generatorSessionSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Previously generated values, kept in memory only.'**
  String get generatorSessionSubtitle;

  /// No description provided for @generatorDialogTitle.
  ///
  /// In en, this message translates to:
  /// **'Generate a password'**
  String get generatorDialogTitle;

  /// No description provided for @generatorDialogSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Pick the shape you need, then use it in the entry.'**
  String get generatorDialogSubtitle;

  /// No description provided for @generatorUsePassword.
  ///
  /// In en, this message translates to:
  /// **'Use password'**
  String get generatorUsePassword;

  /// No description provided for @securityTitle.
  ///
  /// In en, this message translates to:
  /// **'Security'**
  String get securityTitle;

  /// No description provided for @securitySubtitle.
  ///
  /// In en, this message translates to:
  /// **'How this vault is encrypted, stored and protected.'**
  String get securitySubtitle;

  /// No description provided for @securityLockNow.
  ///
  /// In en, this message translates to:
  /// **'Lock now'**
  String get securityLockNow;

  /// No description provided for @securitySession.
  ///
  /// In en, this message translates to:
  /// **'Session'**
  String get securitySession;

  /// No description provided for @securitySessionSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Current state of the unlocked vault.'**
  String get securitySessionSubtitle;

  /// No description provided for @securityStateSaving.
  ///
  /// In en, this message translates to:
  /// **'Saving...'**
  String get securityStateSaving;

  /// No description provided for @securityStateSaveFailed.
  ///
  /// In en, this message translates to:
  /// **'Save failed'**
  String get securityStateSaveFailed;

  /// No description provided for @securityStateSaved.
  ///
  /// In en, this message translates to:
  /// **'Saved'**
  String get securityStateSaved;

  /// No description provided for @securityStateUnlocked.
  ///
  /// In en, this message translates to:
  /// **'Unlocked'**
  String get securityStateUnlocked;

  /// No description provided for @securityEntries.
  ///
  /// In en, this message translates to:
  /// **'Entries'**
  String get securityEntries;

  /// No description provided for @securityLastWritten.
  ///
  /// In en, this message translates to:
  /// **'Last written'**
  String get securityLastWritten;

  /// No description provided for @securityMasterPasswordMemory.
  ///
  /// In en, this message translates to:
  /// **'Master password in memory'**
  String get securityMasterPasswordMemory;

  /// No description provided for @securityMasterPasswordMemoryValue.
  ///
  /// In en, this message translates to:
  /// **'No — only the derived key is kept while unlocked, and it is wiped on lock.'**
  String get securityMasterPasswordMemoryValue;

  /// No description provided for @securityRenderingEngine.
  ///
  /// In en, this message translates to:
  /// **'Rendering engine'**
  String get securityRenderingEngine;

  /// No description provided for @securityRetrySave.
  ///
  /// In en, this message translates to:
  /// **'Retry save'**
  String get securityRetrySave;

  /// No description provided for @securityEncryption.
  ///
  /// In en, this message translates to:
  /// **'Encryption'**
  String get securityEncryption;

  /// No description provided for @securityEncryptionSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Primitives used by this build.'**
  String get securityEncryptionSubtitle;

  /// No description provided for @securityCipher.
  ///
  /// In en, this message translates to:
  /// **'Cipher'**
  String get securityCipher;

  /// No description provided for @securityCipherValue.
  ///
  /// In en, this message translates to:
  /// **'AES-256-GCM (128-bit authentication tag)'**
  String get securityCipherValue;

  /// No description provided for @securityKeyDerivation.
  ///
  /// In en, this message translates to:
  /// **'Key derivation'**
  String get securityKeyDerivation;

  /// No description provided for @securityVaultFormat.
  ///
  /// In en, this message translates to:
  /// **'Vault format'**
  String get securityVaultFormat;

  /// No description provided for @securityFormatUnknown.
  ///
  /// In en, this message translates to:
  /// **'Unknown'**
  String get securityFormatUnknown;

  /// No description provided for @securityFormatLegacySuffix.
  ///
  /// In en, this message translates to:
  /// **' (legacy)'**
  String get securityFormatLegacySuffix;

  /// No description provided for @securityMemoryHard.
  ///
  /// In en, this message translates to:
  /// **'Memory-hard'**
  String get securityMemoryHard;

  /// No description provided for @securityMemoryHardNo.
  ///
  /// In en, this message translates to:
  /// **'No — this file still uses PBKDF2'**
  String get securityMemoryHardNo;

  /// No description provided for @securityMemoryHardYes.
  ///
  /// In en, this message translates to:
  /// **'Yes — Argon2id resists GPU and ASIC cracking'**
  String get securityMemoryHardYes;

  /// No description provided for @securityHeaderAuth.
  ///
  /// In en, this message translates to:
  /// **'Header authentication'**
  String get securityHeaderAuth;

  /// No description provided for @securityHeaderAuthValue.
  ///
  /// In en, this message translates to:
  /// **'The full header (version, KDF, parameters, salt, nonce) is authenticated as GCM associated data.'**
  String get securityHeaderAuthValue;

  /// No description provided for @securityNonce.
  ///
  /// In en, this message translates to:
  /// **'Nonce'**
  String get securityNonce;

  /// No description provided for @securityNonceValue.
  ///
  /// In en, this message translates to:
  /// **'Fresh 96-bit random nonce for every save'**
  String get securityNonceValue;

  /// No description provided for @securityLegacyFormatTitle.
  ///
  /// In en, this message translates to:
  /// **'Legacy vault format detected'**
  String get securityLegacyFormatTitle;

  /// No description provided for @securityOldKdfTitle.
  ///
  /// In en, this message translates to:
  /// **'Older key derivation detected'**
  String get securityOldKdfTitle;

  /// No description provided for @securityOldKdfMessage.
  ///
  /// In en, this message translates to:
  /// **'This file was written before Argon2id ({current}). The next successful unlock re-encrypts it with {target}.'**
  String securityOldKdfMessage(String current, String target);

  /// No description provided for @securityArgonNote.
  ///
  /// In en, this message translates to:
  /// **'Argon2id is memory-hard, so cracking hardware cannot trade memory for parallelism the way it can against PBKDF2. A long, unique master password is still what protects the vault.'**
  String get securityArgonNote;

  /// No description provided for @securityHealthTitle.
  ///
  /// In en, this message translates to:
  /// **'Password health'**
  String get securityHealthTitle;

  /// No description provided for @securityHealthSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Reuse and strength across every stored password.'**
  String get securityHealthSubtitle;

  /// No description provided for @securityHealthScore.
  ///
  /// In en, this message translates to:
  /// **'{score} / 100'**
  String securityHealthScore(int score);

  /// No description provided for @securityHealthNoPasswords.
  ///
  /// In en, this message translates to:
  /// **'No passwords yet'**
  String get securityHealthNoPasswords;

  /// No description provided for @securityHealthWithPassword.
  ///
  /// In en, this message translates to:
  /// **'With password'**
  String get securityHealthWithPassword;

  /// No description provided for @securityHealthReused.
  ///
  /// In en, this message translates to:
  /// **'Reused'**
  String get securityHealthReused;

  /// No description provided for @securityHealthWeak.
  ///
  /// In en, this message translates to:
  /// **'Weak'**
  String get securityHealthWeak;

  /// No description provided for @securityHealthNoPassword.
  ///
  /// In en, this message translates to:
  /// **'No password'**
  String get securityHealthNoPassword;

  /// No description provided for @securityHealthEmptyMessage.
  ///
  /// In en, this message translates to:
  /// **'Nothing to analyse yet — add a credential with a password.'**
  String get securityHealthEmptyMessage;

  /// No description provided for @securityHealthCleanMessage.
  ///
  /// In en, this message translates to:
  /// **'Every password is unique and none of them look weak.'**
  String get securityHealthCleanMessage;

  /// No description provided for @securityHealthReusedTitle.
  ///
  /// In en, this message translates to:
  /// **'REUSED PASSWORDS'**
  String get securityHealthReusedTitle;

  /// No description provided for @securityHealthSharedBy.
  ///
  /// In en, this message translates to:
  /// **'Shared by {count} entries'**
  String securityHealthSharedBy(int count);

  /// No description provided for @securityHealthMoreGroups.
  ///
  /// In en, this message translates to:
  /// **'+ {count} more groups'**
  String securityHealthMoreGroups(int count);

  /// No description provided for @securityHealthReuseNote.
  ///
  /// In en, this message translates to:
  /// **'One leaked service exposes every entry sharing that password. Give each of them its own generated password.'**
  String get securityHealthReuseNote;

  /// No description provided for @securityHealthWeakTitle.
  ///
  /// In en, this message translates to:
  /// **'WEAK PASSWORDS'**
  String get securityHealthWeakTitle;

  /// No description provided for @securityHealthMore.
  ///
  /// In en, this message translates to:
  /// **'+ {count} more'**
  String securityHealthMore(int count);

  /// No description provided for @securityVaultFile.
  ///
  /// In en, this message translates to:
  /// **'Vault file'**
  String get securityVaultFile;

  /// No description provided for @securityVaultFileSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Everything lives in a single encrypted file.'**
  String get securityVaultFileSubtitle;

  /// No description provided for @securityRefresh.
  ///
  /// In en, this message translates to:
  /// **'Refresh'**
  String get securityRefresh;

  /// No description provided for @securityLocation.
  ///
  /// In en, this message translates to:
  /// **'Location'**
  String get securityLocation;

  /// No description provided for @securityResolving.
  ///
  /// In en, this message translates to:
  /// **'Resolving...'**
  String get securityResolving;

  /// No description provided for @securityCopyPath.
  ///
  /// In en, this message translates to:
  /// **'Copy path'**
  String get securityCopyPath;

  /// No description provided for @securityOpenFolder.
  ///
  /// In en, this message translates to:
  /// **'Open containing folder'**
  String get securityOpenFolder;

  /// No description provided for @securitySize.
  ///
  /// In en, this message translates to:
  /// **'Size'**
  String get securitySize;

  /// No description provided for @securityModified.
  ///
  /// In en, this message translates to:
  /// **'Modified'**
  String get securityModified;

  /// No description provided for @securityBackupCopy.
  ///
  /// In en, this message translates to:
  /// **'Backup copy'**
  String get securityBackupCopy;

  /// No description provided for @securityBackupPresent.
  ///
  /// In en, this message translates to:
  /// **'credentials.plf.bak kept next to the vault'**
  String get securityBackupPresent;

  /// No description provided for @securityBackupOnNextSave.
  ///
  /// In en, this message translates to:
  /// **'Created automatically on the next save'**
  String get securityBackupOnNextSave;

  /// No description provided for @securityWrites.
  ///
  /// In en, this message translates to:
  /// **'Writes'**
  String get securityWrites;

  /// No description provided for @securityWritesValue.
  ///
  /// In en, this message translates to:
  /// **'Atomic: written to a temp file, then renamed over the vault, so an interrupted save cannot corrupt it.'**
  String get securityWritesValue;

  /// No description provided for @securityFolderOpenFailed.
  ///
  /// In en, this message translates to:
  /// **'The folder could not be opened'**
  String get securityFolderOpenFailed;

  /// No description provided for @securityVaultPathLabel.
  ///
  /// In en, this message translates to:
  /// **'Vault path'**
  String get securityVaultPathLabel;

  /// No description provided for @securityProtection.
  ///
  /// In en, this message translates to:
  /// **'Protection'**
  String get securityProtection;

  /// No description provided for @securityProtectionSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Session behaviour you control in Settings.'**
  String get securityProtectionSubtitle;

  /// No description provided for @securityMinimizeLabel.
  ///
  /// In en, this message translates to:
  /// **'Minimize / Alt-Tab'**
  String get securityMinimizeLabel;

  /// No description provided for @securityMinimizeValue.
  ///
  /// In en, this message translates to:
  /// **'Never locks the vault — copy a secret, switch apps and come back to an open vault.'**
  String get securityMinimizeValue;

  /// No description provided for @securityClipboardCleared.
  ///
  /// In en, this message translates to:
  /// **'Cleared after {seconds}s, and only if the copied value is still there'**
  String securityClipboardCleared(int seconds);

  /// No description provided for @securityClipboardNever.
  ///
  /// In en, this message translates to:
  /// **'Never cleared automatically'**
  String get securityClipboardNever;

  /// No description provided for @securitySecretsOnScreen.
  ///
  /// In en, this message translates to:
  /// **'Secrets on screen'**
  String get securitySecretsOnScreen;

  /// No description provided for @securitySecretsRevealed.
  ///
  /// In en, this message translates to:
  /// **'Revealed by default in the editor'**
  String get securitySecretsRevealed;

  /// No description provided for @securitySecretsHidden.
  ///
  /// In en, this message translates to:
  /// **'Hidden until you reveal them'**
  String get securitySecretsHidden;

  /// No description provided for @securityNetwork.
  ///
  /// In en, this message translates to:
  /// **'Network'**
  String get securityNetwork;

  /// No description provided for @securityNetworkValue.
  ///
  /// In en, this message translates to:
  /// **'None. No telemetry, no sync, no favicon lookups, no update checks.'**
  String get securityNetworkValue;

  /// No description provided for @securityChangeMasterPassword.
  ///
  /// In en, this message translates to:
  /// **'Change master password'**
  String get securityChangeMasterPassword;

  /// No description provided for @changePasswordSubtitle.
  ///
  /// In en, this message translates to:
  /// **'The vault is re-encrypted immediately.'**
  String get changePasswordSubtitle;

  /// No description provided for @changePasswordCurrent.
  ///
  /// In en, this message translates to:
  /// **'Current master password'**
  String get changePasswordCurrent;

  /// No description provided for @changePasswordNew.
  ///
  /// In en, this message translates to:
  /// **'New master password'**
  String get changePasswordNew;

  /// No description provided for @changePasswordConfirm.
  ///
  /// In en, this message translates to:
  /// **'Confirm new password'**
  String get changePasswordConfirm;

  /// No description provided for @changePasswordTooShort.
  ///
  /// In en, this message translates to:
  /// **'The new password needs at least {count} characters.'**
  String changePasswordTooShort(int count);

  /// No description provided for @changePasswordMismatch.
  ///
  /// In en, this message translates to:
  /// **'The new passwords do not match.'**
  String get changePasswordMismatch;

  /// No description provided for @changePasswordBackupNote.
  ///
  /// In en, this message translates to:
  /// **'Keep your backups in mind: copies of the old vault file still need the old password.'**
  String get changePasswordBackupNote;

  /// No description provided for @changePasswordAction.
  ///
  /// In en, this message translates to:
  /// **'Change password'**
  String get changePasswordAction;

  /// No description provided for @changePasswordDone.
  ///
  /// In en, this message translates to:
  /// **'Master password changed'**
  String get changePasswordDone;

  /// No description provided for @changePasswordDoneDetail.
  ///
  /// In en, this message translates to:
  /// **'The vault was re-encrypted with the new key.'**
  String get changePasswordDoneDetail;

  /// No description provided for @settingsTitle.
  ///
  /// In en, this message translates to:
  /// **'Settings'**
  String get settingsTitle;

  /// No description provided for @settingsSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Appearance, security behaviour, keyboard and vault storage.'**
  String get settingsSubtitle;

  /// No description provided for @settingsAppearance.
  ///
  /// In en, this message translates to:
  /// **'Appearance'**
  String get settingsAppearance;

  /// No description provided for @settingsTheme.
  ///
  /// In en, this message translates to:
  /// **'Theme'**
  String get settingsTheme;

  /// No description provided for @settingsThemeDescription.
  ///
  /// In en, this message translates to:
  /// **'All variants are dark; pick the one you prefer.'**
  String get settingsThemeDescription;

  /// No description provided for @settingsDensity.
  ///
  /// In en, this message translates to:
  /// **'Density'**
  String get settingsDensity;

  /// No description provided for @settingsDensityDescription.
  ///
  /// In en, this message translates to:
  /// **'Compact fits more rows on smaller windows.'**
  String get settingsDensityDescription;

  /// No description provided for @settingsAnimations.
  ///
  /// In en, this message translates to:
  /// **'Animations'**
  String get settingsAnimations;

  /// No description provided for @settingsAnimationsDescription.
  ///
  /// In en, this message translates to:
  /// **'Turn off transitions and micro-interactions entirely.'**
  String get settingsAnimationsDescription;

  /// No description provided for @settingsLanguage.
  ///
  /// In en, this message translates to:
  /// **'Language'**
  String get settingsLanguage;

  /// No description provided for @settingsLanguageDescription.
  ///
  /// In en, this message translates to:
  /// **'Interface language. \"System\" follows Windows.'**
  String get settingsLanguageDescription;

  /// No description provided for @settingsLanguageSystem.
  ///
  /// In en, this message translates to:
  /// **'System'**
  String get settingsLanguageSystem;

  /// No description provided for @settingsLanguageEnglish.
  ///
  /// In en, this message translates to:
  /// **'English'**
  String get settingsLanguageEnglish;

  /// No description provided for @settingsLanguageItalian.
  ///
  /// In en, this message translates to:
  /// **'Italiano'**
  String get settingsLanguageItalian;

  /// No description provided for @themeMidnight.
  ///
  /// In en, this message translates to:
  /// **'Midnight'**
  String get themeMidnight;

  /// No description provided for @themeMidnightDescription.
  ///
  /// In en, this message translates to:
  /// **'Deep indigo base with violet accent'**
  String get themeMidnightDescription;

  /// No description provided for @themeObsidian.
  ///
  /// In en, this message translates to:
  /// **'Obsidian'**
  String get themeObsidian;

  /// No description provided for @themeObsidianDescription.
  ///
  /// In en, this message translates to:
  /// **'Neutral graphite with cyan accent'**
  String get themeObsidianDescription;

  /// No description provided for @themeViolet.
  ///
  /// In en, this message translates to:
  /// **'Violet'**
  String get themeViolet;

  /// No description provided for @themeVioletDescription.
  ///
  /// In en, this message translates to:
  /// **'Saturated violet, higher contrast accents'**
  String get themeVioletDescription;

  /// No description provided for @densityComfortable.
  ///
  /// In en, this message translates to:
  /// **'Comfortable'**
  String get densityComfortable;

  /// No description provided for @densityCompact.
  ///
  /// In en, this message translates to:
  /// **'Compact'**
  String get densityCompact;

  /// No description provided for @settingsSecurity.
  ///
  /// In en, this message translates to:
  /// **'Security'**
  String get settingsSecurity;

  /// No description provided for @settingsAutoLock.
  ///
  /// In en, this message translates to:
  /// **'Auto-lock after inactivity'**
  String get settingsAutoLock;

  /// No description provided for @settingsAutoLockDescription.
  ///
  /// In en, this message translates to:
  /// **'Minimizing, Alt-Tab or losing focus never lock the vault — only this timer and the manual lock do.'**
  String get settingsAutoLockDescription;

  /// No description provided for @settingsNever.
  ///
  /// In en, this message translates to:
  /// **'Never'**
  String get settingsNever;

  /// No description provided for @settingsMinutesShort.
  ///
  /// In en, this message translates to:
  /// **'{minutes}m'**
  String settingsMinutesShort(int minutes);

  /// No description provided for @settingsSecondsShort.
  ///
  /// In en, this message translates to:
  /// **'{seconds}s'**
  String settingsSecondsShort(int seconds);

  /// No description provided for @settingsClipboard.
  ///
  /// In en, this message translates to:
  /// **'Clear clipboard after copying'**
  String get settingsClipboard;

  /// No description provided for @settingsClipboardDescription.
  ///
  /// In en, this message translates to:
  /// **'Only clears if the copied secret is still on the clipboard.'**
  String get settingsClipboardDescription;

  /// No description provided for @settingsRevealSecrets.
  ///
  /// In en, this message translates to:
  /// **'Reveal secrets by default'**
  String get settingsRevealSecrets;

  /// No description provided for @settingsRevealSecretsDescription.
  ///
  /// In en, this message translates to:
  /// **'When off, passwords and secret fields start hidden.'**
  String get settingsRevealSecretsDescription;

  /// No description provided for @settingsConfirmDelete.
  ///
  /// In en, this message translates to:
  /// **'Confirm before deleting'**
  String get settingsConfirmDelete;

  /// No description provided for @settingsConfirmDeleteDescription.
  ///
  /// In en, this message translates to:
  /// **'Deleting always offers an undo; this adds a dialog first.'**
  String get settingsConfirmDeleteDescription;

  /// No description provided for @settingsKeepHistory.
  ///
  /// In en, this message translates to:
  /// **'Keep password history'**
  String get settingsKeepHistory;

  /// No description provided for @settingsKeepHistoryDescription.
  ///
  /// In en, this message translates to:
  /// **'Superseded passwords stay on the entry (up to {max}) so a bad change can be undone. They are stored encrypted, inside the vault.'**
  String settingsKeepHistoryDescription(int max);

  /// No description provided for @settingsStoredHistory.
  ///
  /// In en, this message translates to:
  /// **'Stored password history'**
  String get settingsStoredHistory;

  /// No description provided for @settingsStoredHistoryEmpty.
  ///
  /// In en, this message translates to:
  /// **'No superseded passwords are stored right now.'**
  String get settingsStoredHistoryEmpty;

  /// No description provided for @settingsStoredHistoryCount.
  ///
  /// In en, this message translates to:
  /// **'{count} superseded passwords across {entries} entries.'**
  String settingsStoredHistoryCount(int count, int entries);

  /// No description provided for @settingsClearAll.
  ///
  /// In en, this message translates to:
  /// **'Clear all'**
  String get settingsClearAll;

  /// No description provided for @settingsClearAllTitle.
  ///
  /// In en, this message translates to:
  /// **'Clear all password history?'**
  String get settingsClearAllTitle;

  /// No description provided for @settingsClearAllMessage.
  ///
  /// In en, this message translates to:
  /// **'Every superseded password in the vault is deleted. Current passwords are untouched. This cannot be undone.'**
  String get settingsClearAllMessage;

  /// No description provided for @settingsClearAllDetail.
  ///
  /// In en, this message translates to:
  /// **'{count} stored passwords'**
  String settingsClearAllDetail(int count);

  /// No description provided for @settingsClearAllConfirm.
  ///
  /// In en, this message translates to:
  /// **'Clear everything'**
  String get settingsClearAllConfirm;

  /// No description provided for @settingsClearAllDone.
  ///
  /// In en, this message translates to:
  /// **'{count} removed'**
  String settingsClearAllDone(int count);

  /// No description provided for @settingsMasterPassword.
  ///
  /// In en, this message translates to:
  /// **'Master password'**
  String get settingsMasterPassword;

  /// No description provided for @settingsMasterPasswordDescription.
  ///
  /// In en, this message translates to:
  /// **'Re-encrypts the vault with a new key.'**
  String get settingsMasterPasswordDescription;

  /// No description provided for @settingsChange.
  ///
  /// In en, this message translates to:
  /// **'Change'**
  String get settingsChange;

  /// No description provided for @settingsKeyboard.
  ///
  /// In en, this message translates to:
  /// **'Keyboard'**
  String get settingsKeyboard;

  /// No description provided for @settingsGlobalShortcut.
  ///
  /// In en, this message translates to:
  /// **'Global shortcut (Ctrl+Alt+P)'**
  String get settingsGlobalShortcut;

  /// No description provided for @settingsGlobalShortcutDescription.
  ///
  /// In en, this message translates to:
  /// **'Brings BetterOnlyYours forward from any application and opens the command palette.'**
  String get settingsGlobalShortcutDescription;

  /// No description provided for @settingsShortcuts.
  ///
  /// In en, this message translates to:
  /// **'Shortcuts'**
  String get settingsShortcuts;

  /// No description provided for @settingsShortcutsDescription.
  ///
  /// In en, this message translates to:
  /// **'Everything reachable without the mouse.'**
  String get settingsShortcutsDescription;

  /// No description provided for @settingsVaultWindow.
  ///
  /// In en, this message translates to:
  /// **'Vault & window'**
  String get settingsVaultWindow;

  /// No description provided for @settingsVaultFile.
  ///
  /// In en, this message translates to:
  /// **'Vault file'**
  String get settingsVaultFile;

  /// No description provided for @settingsVaultFileResolving.
  ///
  /// In en, this message translates to:
  /// **'Resolving the vault location...'**
  String get settingsVaultFileResolving;

  /// No description provided for @settingsRememberWindow.
  ///
  /// In en, this message translates to:
  /// **'Remember window size and position'**
  String get settingsRememberWindow;

  /// No description provided for @settingsRememberWindowDescription.
  ///
  /// In en, this message translates to:
  /// **'Restores the window where you left it on the next start.'**
  String get settingsRememberWindowDescription;

  /// No description provided for @settingsAbout.
  ///
  /// In en, this message translates to:
  /// **'About'**
  String get settingsAbout;

  /// No description provided for @settingsVersion.
  ///
  /// In en, this message translates to:
  /// **'Version'**
  String get settingsVersion;

  /// No description provided for @settingsStorage.
  ///
  /// In en, this message translates to:
  /// **'Storage'**
  String get settingsStorage;

  /// No description provided for @settingsStorageValue.
  ///
  /// In en, this message translates to:
  /// **'A single encrypted file, written atomically.'**
  String get settingsStorageValue;

  /// No description provided for @settingsNetworkAccess.
  ///
  /// In en, this message translates to:
  /// **'Network access'**
  String get settingsNetworkAccess;

  /// No description provided for @settingsNetworkAccessValue.
  ///
  /// In en, this message translates to:
  /// **'None — the app never connects to anything.'**
  String get settingsNetworkAccessValue;

  /// No description provided for @actionCopied.
  ///
  /// In en, this message translates to:
  /// **'{label} copied'**
  String actionCopied(String label);

  /// No description provided for @actionCopiedDetail.
  ///
  /// In en, this message translates to:
  /// **'Clipboard clears in {seconds}s unless you copy something else.'**
  String actionCopiedDetail(int seconds);

  /// No description provided for @actionClipboardUnavailable.
  ///
  /// In en, this message translates to:
  /// **'Clipboard unavailable'**
  String get actionClipboardUnavailable;

  /// No description provided for @actionClipboardUnavailableDetail.
  ///
  /// In en, this message translates to:
  /// **'Windows refused the copy — another app may be holding it.'**
  String get actionClipboardUnavailableDetail;

  /// No description provided for @labelUsername.
  ///
  /// In en, this message translates to:
  /// **'Username'**
  String get labelUsername;

  /// No description provided for @labelPassword.
  ///
  /// In en, this message translates to:
  /// **'Password'**
  String get labelPassword;

  /// No description provided for @actionNoUsername.
  ///
  /// In en, this message translates to:
  /// **'No username on this entry'**
  String get actionNoUsername;

  /// No description provided for @actionNoUsernameDetail.
  ///
  /// In en, this message translates to:
  /// **'Add one from the entry details.'**
  String get actionNoUsernameDetail;

  /// No description provided for @actionNoPassword.
  ///
  /// In en, this message translates to:
  /// **'No password on this entry'**
  String get actionNoPassword;

  /// No description provided for @actionNoPasswordLegacy.
  ///
  /// In en, this message translates to:
  /// **'This entry still holds free-form notes only.'**
  String get actionNoPasswordLegacy;

  /// No description provided for @actionNoPasswordDetail.
  ///
  /// In en, this message translates to:
  /// **'Add or generate one from the entry details.'**
  String get actionNoPasswordDetail;

  /// No description provided for @actionNoWebsite.
  ///
  /// In en, this message translates to:
  /// **'No website on this entry'**
  String get actionNoWebsite;

  /// No description provided for @actionBadUrl.
  ///
  /// In en, this message translates to:
  /// **'That address cannot be opened'**
  String get actionBadUrl;

  /// No description provided for @actionBadUrlDetail.
  ///
  /// In en, this message translates to:
  /// **'Only http and https links are launched.'**
  String get actionBadUrlDetail;

  /// No description provided for @actionNoBrowser.
  ///
  /// In en, this message translates to:
  /// **'No browser responded'**
  String get actionNoBrowser;

  /// No description provided for @actionLinkFailed.
  ///
  /// In en, this message translates to:
  /// **'The link could not be opened'**
  String get actionLinkFailed;

  /// No description provided for @actionFavoriteAdded.
  ///
  /// In en, this message translates to:
  /// **'Added to favorites'**
  String get actionFavoriteAdded;

  /// No description provided for @actionFavoriteRemoved.
  ///
  /// In en, this message translates to:
  /// **'Removed from favorites'**
  String get actionFavoriteRemoved;

  /// No description provided for @actionDuplicated.
  ///
  /// In en, this message translates to:
  /// **'Entry duplicated'**
  String get actionDuplicated;

  /// No description provided for @actionDeleteTitle.
  ///
  /// In en, this message translates to:
  /// **'Delete this entry?'**
  String get actionDeleteTitle;

  /// No description provided for @actionDeleteMessage.
  ///
  /// In en, this message translates to:
  /// **'It is removed from the encrypted vault. You can undo this right after, from the notification.'**
  String get actionDeleteMessage;

  /// No description provided for @actionDeleted.
  ///
  /// In en, this message translates to:
  /// **'Entry deleted'**
  String get actionDeleted;

  /// No description provided for @menuOpen.
  ///
  /// In en, this message translates to:
  /// **'Open'**
  String get menuOpen;

  /// No description provided for @menuCopyUsername.
  ///
  /// In en, this message translates to:
  /// **'Copy username'**
  String get menuCopyUsername;

  /// No description provided for @menuCopyPassword.
  ///
  /// In en, this message translates to:
  /// **'Copy password'**
  String get menuCopyPassword;

  /// No description provided for @menuOpenWebsite.
  ///
  /// In en, this message translates to:
  /// **'Open website'**
  String get menuOpenWebsite;

  /// No description provided for @menuAddFavorite.
  ///
  /// In en, this message translates to:
  /// **'Add to favorites'**
  String get menuAddFavorite;

  /// No description provided for @menuRemoveFavorite.
  ///
  /// In en, this message translates to:
  /// **'Remove from favorites'**
  String get menuRemoveFavorite;

  /// No description provided for @menuDuplicate.
  ///
  /// In en, this message translates to:
  /// **'Duplicate'**
  String get menuDuplicate;

  /// No description provided for @menuDelete.
  ///
  /// In en, this message translates to:
  /// **'Delete'**
  String get menuDelete;

  /// No description provided for @vaultLocked.
  ///
  /// In en, this message translates to:
  /// **'Vault locked'**
  String get vaultLocked;

  /// No description provided for @vaultLockedInactivity.
  ///
  /// In en, this message translates to:
  /// **'Locked after {minutes} minutes of inactivity.'**
  String vaultLockedInactivity(int minutes);

  /// No description provided for @vaultRestoredBackup.
  ///
  /// In en, this message translates to:
  /// **'Vault restored from backup'**
  String get vaultRestoredBackup;

  /// No description provided for @vaultRestoredBackupDetail.
  ///
  /// In en, this message translates to:
  /// **'The main file could not be read, so the .bak copy was opened.'**
  String get vaultRestoredBackupDetail;

  /// No description provided for @vaultUpgraded.
  ///
  /// In en, this message translates to:
  /// **'Vault upgraded'**
  String get vaultUpgraded;

  /// No description provided for @vaultUpgradedDetail.
  ///
  /// In en, this message translates to:
  /// **'Re-encrypted with stronger key derivation ({kdf}).'**
  String vaultUpgradedDetail(String kdf);

  /// No description provided for @vaultSaveRetryHint.
  ///
  /// In en, this message translates to:
  /// **'{hint} Your changes are still here — retry the save.'**
  String vaultSaveRetryHint(String hint);

  /// No description provided for @vaultSaveFailed.
  ///
  /// In en, this message translates to:
  /// **'Vault could not be saved'**
  String get vaultSaveFailed;

  /// No description provided for @vaultSaveFailedDetail.
  ///
  /// In en, this message translates to:
  /// **'Your changes are still here — retry the save.'**
  String get vaultSaveFailedDetail;

  /// No description provided for @errorNotFoundTitle.
  ///
  /// In en, this message translates to:
  /// **'Vault file not found'**
  String get errorNotFoundTitle;

  /// No description provided for @errorNotFoundHint.
  ///
  /// In en, this message translates to:
  /// **'Restore your vault file or create a new vault to continue.'**
  String get errorNotFoundHint;

  /// No description provided for @errorInvalidPasswordTitle.
  ///
  /// In en, this message translates to:
  /// **'Master password not accepted'**
  String get errorInvalidPasswordTitle;

  /// No description provided for @errorInvalidPasswordHint.
  ///
  /// In en, this message translates to:
  /// **'Check for Caps Lock and keyboard layout. If the password is right, the file may have been modified on disk.'**
  String get errorInvalidPasswordHint;

  /// No description provided for @errorBadSignatureTitle.
  ///
  /// In en, this message translates to:
  /// **'This file is not a BetterOnlyYours vault'**
  String get errorBadSignatureTitle;

  /// No description provided for @errorBadSignatureHint.
  ///
  /// In en, this message translates to:
  /// **'The file header does not match the vault format. Restore a backup copy.'**
  String get errorBadSignatureHint;

  /// No description provided for @errorUnsupportedVersionTitle.
  ///
  /// In en, this message translates to:
  /// **'Unsupported vault version'**
  String get errorUnsupportedVersionTitle;

  /// No description provided for @errorUnsupportedVersionHint.
  ///
  /// In en, this message translates to:
  /// **'The vault was written by a newer version of BetterOnlyYours. Update the app to open it.'**
  String get errorUnsupportedVersionHint;

  /// No description provided for @errorTruncatedTitle.
  ///
  /// In en, this message translates to:
  /// **'Vault file is incomplete'**
  String get errorTruncatedTitle;

  /// No description provided for @errorTruncatedHint.
  ///
  /// In en, this message translates to:
  /// **'The file is shorter than a valid vault. Restore the .bak backup next to it.'**
  String get errorTruncatedHint;

  /// No description provided for @errorMalformedTitle.
  ///
  /// In en, this message translates to:
  /// **'Vault contents could not be read'**
  String get errorMalformedTitle;

  /// No description provided for @errorMalformedHint.
  ///
  /// In en, this message translates to:
  /// **'Decryption succeeded but the contents are not valid vault data.'**
  String get errorMalformedHint;

  /// No description provided for @errorPermissionTitle.
  ///
  /// In en, this message translates to:
  /// **'Access to the vault file was denied'**
  String get errorPermissionTitle;

  /// No description provided for @errorPermissionHint.
  ///
  /// In en, this message translates to:
  /// **'Close other apps using the file, or run from a folder you can write to.'**
  String get errorPermissionHint;

  /// No description provided for @errorIoTitle.
  ///
  /// In en, this message translates to:
  /// **'Vault could not be read or written'**
  String get errorIoTitle;

  /// No description provided for @errorIoHint.
  ///
  /// In en, this message translates to:
  /// **'Check that the vault folder exists and has free space, then retry.'**
  String get errorIoHint;

  /// No description provided for @strengthEmpty.
  ///
  /// In en, this message translates to:
  /// **'—'**
  String get strengthEmpty;

  /// No description provided for @strengthVeryWeak.
  ///
  /// In en, this message translates to:
  /// **'Very weak'**
  String get strengthVeryWeak;

  /// No description provided for @strengthWeak.
  ///
  /// In en, this message translates to:
  /// **'Weak'**
  String get strengthWeak;

  /// No description provided for @strengthFair.
  ///
  /// In en, this message translates to:
  /// **'Fair'**
  String get strengthFair;

  /// No description provided for @strengthStrong.
  ///
  /// In en, this message translates to:
  /// **'Strong'**
  String get strengthStrong;

  /// No description provided for @strengthExcellent.
  ///
  /// In en, this message translates to:
  /// **'Excellent'**
  String get strengthExcellent;

  /// No description provided for @strengthBits.
  ///
  /// In en, this message translates to:
  /// **'~ {bits} bits'**
  String strengthBits(int bits);

  /// No description provided for @strengthEstimateNote.
  ///
  /// In en, this message translates to:
  /// **'Estimate only — based on length, character mix and obvious patterns.'**
  String get strengthEstimateNote;

  /// No description provided for @inspectorTitle.
  ///
  /// In en, this message translates to:
  /// **'INSPECTOR'**
  String get inspectorTitle;

  /// No description provided for @inspectorPasswordStrength.
  ///
  /// In en, this message translates to:
  /// **'Password strength'**
  String get inspectorPasswordStrength;

  /// No description provided for @inspectorReused.
  ///
  /// In en, this message translates to:
  /// **'This password is also used by another entry.'**
  String get inspectorReused;

  /// No description provided for @inspectorQuickActions.
  ///
  /// In en, this message translates to:
  /// **'Quick actions'**
  String get inspectorQuickActions;

  /// No description provided for @inspectorDuplicate.
  ///
  /// In en, this message translates to:
  /// **'Duplicate entry'**
  String get inspectorDuplicate;

  /// No description provided for @inspectorDelete.
  ///
  /// In en, this message translates to:
  /// **'Delete entry'**
  String get inspectorDelete;

  /// No description provided for @inspectorTimeline.
  ///
  /// In en, this message translates to:
  /// **'Timeline'**
  String get inspectorTimeline;

  /// No description provided for @inspectorCreated.
  ///
  /// In en, this message translates to:
  /// **'Created'**
  String get inspectorCreated;

  /// No description provided for @inspectorModified.
  ///
  /// In en, this message translates to:
  /// **'Modified'**
  String get inspectorModified;

  /// No description provided for @inspectorOpened.
  ///
  /// In en, this message translates to:
  /// **'Opened'**
  String get inspectorOpened;

  /// No description provided for @inspectorFormat.
  ///
  /// In en, this message translates to:
  /// **'Format'**
  String get inspectorFormat;

  /// No description provided for @inspectorFormatLegacy.
  ///
  /// In en, this message translates to:
  /// **'Legacy text'**
  String get inspectorFormatLegacy;

  /// No description provided for @inspectorFormatStructured.
  ///
  /// In en, this message translates to:
  /// **'Structured'**
  String get inspectorFormatStructured;

  /// No description provided for @inspectorHistory.
  ///
  /// In en, this message translates to:
  /// **'History'**
  String get inspectorHistory;

  /// No description provided for @inspectorNoHistory.
  ///
  /// In en, this message translates to:
  /// **'No previous passwords'**
  String get inspectorNoHistory;

  /// No description provided for @inspectorUnknown.
  ///
  /// In en, this message translates to:
  /// **'Unknown'**
  String get inspectorUnknown;

  /// No description provided for @inspectorTags.
  ///
  /// In en, this message translates to:
  /// **'Tags'**
  String get inspectorTags;

  /// No description provided for @inspectorRelated.
  ///
  /// In en, this message translates to:
  /// **'Related'**
  String get inspectorRelated;

  /// No description provided for @vaultPageFavoritesTitle.
  ///
  /// In en, this message translates to:
  /// **'Favorites'**
  String get vaultPageFavoritesTitle;

  /// No description provided for @vaultPageFavoritesMessage.
  ///
  /// In en, this message translates to:
  /// **'Pick an entry on the left, or star the ones you use most so they land here.'**
  String get vaultPageFavoritesMessage;

  /// No description provided for @vaultPageRecentTitle.
  ///
  /// In en, this message translates to:
  /// **'Recently opened'**
  String get vaultPageRecentTitle;

  /// No description provided for @vaultPageRecentMessage.
  ///
  /// In en, this message translates to:
  /// **'Your last opened entries are listed on the left. Select one to see it here.'**
  String get vaultPageRecentMessage;

  /// No description provided for @vaultPageRecentHint.
  ///
  /// In en, this message translates to:
  /// **'History is stored inside the encrypted vault, never on disk in the clear.'**
  String get vaultPageRecentHint;

  /// No description provided for @shortcutPalette.
  ///
  /// In en, this message translates to:
  /// **'Command palette'**
  String get shortcutPalette;

  /// No description provided for @shortcutPaletteGlobal.
  ///
  /// In en, this message translates to:
  /// **'Command palette from anywhere in Windows'**
  String get shortcutPaletteGlobal;

  /// No description provided for @shortcutNewEntry.
  ///
  /// In en, this message translates to:
  /// **'New entry'**
  String get shortcutNewEntry;

  /// No description provided for @shortcutFocusSearch.
  ///
  /// In en, this message translates to:
  /// **'Focus the vault search field'**
  String get shortcutFocusSearch;

  /// No description provided for @shortcutSave.
  ///
  /// In en, this message translates to:
  /// **'Save the entry being edited'**
  String get shortcutSave;

  /// No description provided for @shortcutFavorite.
  ///
  /// In en, this message translates to:
  /// **'Toggle favorite on the selected entry'**
  String get shortcutFavorite;

  /// No description provided for @shortcutCopyPassword.
  ///
  /// In en, this message translates to:
  /// **'Copy the selected password'**
  String get shortcutCopyPassword;

  /// No description provided for @shortcutCopyUsername.
  ///
  /// In en, this message translates to:
  /// **'Copy the selected username'**
  String get shortcutCopyUsername;

  /// No description provided for @shortcutGenerator.
  ///
  /// In en, this message translates to:
  /// **'Password generator'**
  String get shortcutGenerator;

  /// No description provided for @shortcutSettings.
  ///
  /// In en, this message translates to:
  /// **'Settings'**
  String get shortcutSettings;

  /// No description provided for @shortcutLock.
  ///
  /// In en, this message translates to:
  /// **'Lock the vault'**
  String get shortcutLock;

  /// No description provided for @shortcutDelete.
  ///
  /// In en, this message translates to:
  /// **'Delete the selected entry'**
  String get shortcutDelete;

  /// No description provided for @shortcutEscape.
  ///
  /// In en, this message translates to:
  /// **'Close the palette, dialog or search'**
  String get shortcutEscape;

  /// No description provided for @shortcutNavigate.
  ///
  /// In en, this message translates to:
  /// **'Move through results and open'**
  String get shortcutNavigate;

  /// No description provided for @settingsGlobalShortcutFailed.
  ///
  /// In en, this message translates to:
  /// **'Ctrl+Alt+P could not be registered — another application is probably using it.'**
  String get settingsGlobalShortcutFailed;

  /// No description provided for @vaultLockedManual.
  ///
  /// In en, this message translates to:
  /// **'Vault locked'**
  String get vaultLockedManual;
}

class _AppLocalizationsDelegate
    extends LocalizationsDelegate<AppLocalizations> {
  const _AppLocalizationsDelegate();

  @override
  Future<AppLocalizations> load(Locale locale) {
    return SynchronousFuture<AppLocalizations>(lookupAppLocalizations(locale));
  }

  @override
  bool isSupported(Locale locale) =>
      <String>['en', 'it'].contains(locale.languageCode);

  @override
  bool shouldReload(_AppLocalizationsDelegate old) => false;
}

AppLocalizations lookupAppLocalizations(Locale locale) {
  // Lookup logic when only language code is specified.
  switch (locale.languageCode) {
    case 'en':
      return AppLocalizationsEn();
    case 'it':
      return AppLocalizationsIt();
  }

  throw FlutterError(
    'AppLocalizations.delegate failed to load unsupported locale "$locale". This is likely '
    'an issue with the localizations generation tool. Please file an issue '
    'on GitHub with a reproducible sample app and the gen-l10n configuration '
    'that was used.',
  );
}
