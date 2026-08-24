// ignore: unused_import
import 'package:intl/intl.dart' as intl;

import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for English (`en`).
class AppLocalizationsEn extends AppLocalizations {
  AppLocalizationsEn([String locale = 'en']) : super(locale);

  @override
  String get appTagline => 'A local, encrypted vault that stays yours.';

  @override
  String get appDescription =>
      'BetterOnlyYours keeps credentials and secrets in a single encrypted file on this machine. No account, no sync, no telemetry — the vault is only readable with your master password.';

  @override
  String get navVault => 'Vault';

  @override
  String get navFavorites => 'Favorites';

  @override
  String get navRecent => 'Recent';

  @override
  String get navGenerator => 'Generator';

  @override
  String get navSecurity => 'Security';

  @override
  String get navSettings => 'Settings';

  @override
  String get navLockVault => 'Lock vault';

  @override
  String get navCollapse => 'Collapse';

  @override
  String get windowMinimize => 'Minimize';

  @override
  String get windowMaximize => 'Maximize';

  @override
  String get windowRestore => 'Restore';

  @override
  String get windowClose => 'Close';

  @override
  String get actionSearchTooltip => 'Command palette  ·  Ctrl+K';

  @override
  String get actionNewEntryTooltip => 'New entry  ·  Ctrl+N';

  @override
  String get actionLockTooltip => 'Lock vault  ·  Ctrl+L';

  @override
  String statusUnlockedEntries(String entries) {
    return 'Unlocked · $entries';
  }

  @override
  String get statusEncrypting => 'Encrypting…';

  @override
  String get statusSaveFailed => 'Save failed — click to retry';

  @override
  String get statusNoChanges => 'No changes yet';

  @override
  String statusSavedAgo(String when) {
    return 'Saved $when';
  }

  @override
  String statusClipboardClears(String label, int seconds) {
    return '$label clears in ${seconds}s';
  }

  @override
  String get statusKeep => 'keep';

  @override
  String statusAutoLockOn(int minutes) {
    return 'Auto-lock ${minutes}m';
  }

  @override
  String get statusAutoLockOff => 'Auto-lock off';

  @override
  String get authLocalVault => 'Local encrypted vault';

  @override
  String get authVaultLocked => 'Vault locked';

  @override
  String get authUnlockPrompt =>
      'Enter your master password to decrypt this vault.';

  @override
  String get authMasterPassword => 'Master password';

  @override
  String get authUnlockButton => 'Unlock vault';

  @override
  String get authCapsLockOn => 'Caps Lock is on';

  @override
  String get authShowPassword => 'Show password';

  @override
  String get authHidePassword => 'Hide password';

  @override
  String get authLocalOnlyNote =>
      'Everything stays on this machine. There is no account, no sync and no password reset.';

  @override
  String get setupTitle => 'Set up your vault';

  @override
  String get setupHeadline => 'One password, one file, no cloud';

  @override
  String get setupIntro =>
      'Your entries are encrypted with AES-256-GCM using a key derived from the master password. The vault file never leaves this machine, and nothing else can decrypt it.';

  @override
  String get setupPasswordHint => 'Choose something long and memorable';

  @override
  String get setupConfirmLabel => 'Confirm master password';

  @override
  String get setupConfirmHint => 'Type it once more';

  @override
  String setupRequirementLength(int count) {
    return 'At least $count characters';
  }

  @override
  String get setupRequirementMatch => 'Both entries match';

  @override
  String get setupAcknowledgement =>
      'I understand this password cannot be recovered or reset. If I lose it, the vault stays encrypted forever — so I will keep a backup of the vault file somewhere safe.';

  @override
  String get setupCreateButton => 'Create vault';

  @override
  String setupVaultFile(String path) {
    return 'Vault file: $path';
  }

  @override
  String setupErrorFields(int count) {
    return 'Use at least $count characters.';
  }

  @override
  String get setupErrorMismatch => 'The two passwords do not match.';

  @override
  String get setupErrorAcknowledge =>
      'Confirm you understand there is no password recovery.';

  @override
  String get setupHide => 'Hide';

  @override
  String get setupShow => 'Show';

  @override
  String get timeNever => 'never';

  @override
  String get timeJustNow => 'just now';

  @override
  String timeMinutesAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count minutes ago',
      one: '1 minute ago',
    );
    return '$_temp0';
  }

  @override
  String timeHoursAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count hours ago',
      one: '1 hour ago',
    );
    return '$_temp0';
  }

  @override
  String timeDaysAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count days ago',
      one: '1 day ago',
    );
    return '$_temp0';
  }

  @override
  String timeWeeksAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count weeks ago',
      one: '1 week ago',
    );
    return '$_temp0';
  }

  @override
  String timeMonthsAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count months ago',
      one: '1 month ago',
    );
    return '$_temp0';
  }

  @override
  String timeYearsAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count years ago',
      one: '1 year ago',
    );
    return '$_temp0';
  }

  @override
  String entriesCount(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count entries',
      one: '1 entry',
    );
    return '$_temp0';
  }

  @override
  String previousPasswordsCount(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count previous passwords',
      one: '1 previous password',
    );
    return '$_temp0';
  }

  @override
  String get listSearchHint => 'Search vault...';

  @override
  String get listSearchSemantics => 'Search vault';

  @override
  String get listClearSearch => 'Clear search';

  @override
  String get listSortTooltip => 'Sort and filter';

  @override
  String get listFavoritesOnly => 'Favorites only';

  @override
  String listClearTagFilter(String tag) {
    return 'Clear tag filter ($tag)';
  }

  @override
  String listShownOfTotal(int shown, int total) {
    return '$shown of $total';
  }

  @override
  String get listClearFilters => 'Clear filters';

  @override
  String get listEntryActions => 'Entry actions';

  @override
  String get emptySearchTitle => 'Nothing matched';

  @override
  String get emptySearchMessage =>
      'No entry matches the current search or filters.';

  @override
  String emptySearchCreate(String query) {
    return 'Create \"$query\"';
  }

  @override
  String get emptyFavoritesTitle => 'No favorites yet';

  @override
  String get emptyFavoritesMessage =>
      'Star the entries you reach for daily and they will appear here.';

  @override
  String get emptyFavoritesHint =>
      'Ctrl+D toggles a favorite on the selected entry.';

  @override
  String get emptyRecentTitle => 'Nothing opened yet';

  @override
  String get emptyRecentMessage =>
      'Entries you open appear here, newest first. The history lives inside the encrypted vault.';

  @override
  String get emptyVaultTitle => 'Your vault is empty';

  @override
  String get emptyVaultMessage =>
      'Create your first entry — it is encrypted the moment you save.';

  @override
  String get emptyVaultHint => 'Ctrl+N creates an entry from anywhere.';

  @override
  String get newEntry => 'New entry';

  @override
  String get sortTitleAsc => 'Name (A-Z)';

  @override
  String get sortTitleDesc => 'Name (Z-A)';

  @override
  String get sortRecentlyUsed => 'Recently opened';

  @override
  String get sortRecentlyUpdated => 'Recently modified';

  @override
  String dashboardSubtitle(String entries) {
    return '$entries encrypted on this machine. Select an entry to view it, or start something new.';
  }

  @override
  String get dashboardReadyTitle => 'Your vault is ready';

  @override
  String get dashboardReadyMessage =>
      'Nothing is stored yet. Create your first entry — it is encrypted locally the moment you save it.';

  @override
  String get dashboardOpenGenerator => 'Open generator';

  @override
  String get dashboardShortcutHint =>
      'Ctrl+N new entry · Ctrl+K command palette · Ctrl+L lock';

  @override
  String get dashboardSearch => 'Search';

  @override
  String get dashboardGeneratePassword => 'Generate password';

  @override
  String get statEntries => 'Entries';

  @override
  String get statEntriesAllStructured => 'All structured';

  @override
  String statEntriesLegacy(int count) {
    return '$count legacy';
  }

  @override
  String get statFavorites => 'Favorites';

  @override
  String get statFavoritesCaption => 'Starred for quick access';

  @override
  String get statTags => 'Tags';

  @override
  String get statTagsEmpty => 'Add tags to group entries';

  @override
  String get statHealth => 'Password health';

  @override
  String get statHealthClean => 'No weak or reused passwords';

  @override
  String statHealthIssues(int weak, int reused) {
    return '$weak weak · $reused reused';
  }

  @override
  String get dashboardRecentlyOpened => 'Recently opened';

  @override
  String get dashboardRecentlyOpenedEmpty =>
      'Open an entry and it shows up here.';

  @override
  String get dashboardRecentlyModified => 'Recently modified';

  @override
  String get dashboardRecentlyModifiedEmpty =>
      'Entries you edit appear here with their timestamp.';

  @override
  String get dashboardSecuritySnapshot => 'Security snapshot';

  @override
  String get dashboardSecuritySnapshotSubtitle =>
      'Current protection settings for this vault.';

  @override
  String get dashboardSecurityCenter => 'Security center';

  @override
  String get infoEncryption => 'Encryption';

  @override
  String get infoAutoLock => 'Auto-lock';

  @override
  String get infoAutoLockOff => 'Off — minimizing never locks the vault';

  @override
  String infoAutoLockAfter(int minutes) {
    return 'After $minutes minutes of inactivity';
  }

  @override
  String get infoClipboard => 'Clipboard';

  @override
  String get infoClipboardKeep => 'Kept until you replace it';

  @override
  String infoClipboardClear(int seconds) {
    return 'Cleared $seconds seconds after copying';
  }

  @override
  String get infoLastSaved => 'Last saved';

  @override
  String dashboardLegacyNote(int count) {
    return '$count entries still use the legacy plain-text format. Open one and fill in a field to upgrade it.';
  }

  @override
  String get fieldName => 'Name';

  @override
  String get fieldNameHint => 'Entry name';

  @override
  String get fieldUsername => 'Username or email';

  @override
  String get fieldNotSet => 'Not set';

  @override
  String get fieldPassword => 'Password';

  @override
  String get fieldWebsite => 'Website';

  @override
  String get fieldNotes => 'Notes';

  @override
  String get fieldNotesHint => 'Recovery codes, hints, anything else...';

  @override
  String get fieldTags => 'TAGS';

  @override
  String get fieldCustomFields => 'CUSTOM FIELDS';

  @override
  String get fieldDetails => 'DETAILS';

  @override
  String get fieldLabel => 'Label';

  @override
  String get fieldValue => 'Value';

  @override
  String get fieldTagName => 'Tag name';

  @override
  String get fieldAddTag => 'Add tag';

  @override
  String get fieldSuggestions => 'Suggestions';

  @override
  String get fieldAddField => 'Add field';

  @override
  String get fieldCustomFieldsEmpty =>
      'Recovery codes, security questions, API keys — anything that belongs to this entry.';

  @override
  String get fieldCopyValue => 'Copy value';

  @override
  String get fieldHideValue => 'Hide this value';

  @override
  String get fieldShowValue => 'Stop hiding this value';

  @override
  String get fieldRemoveField => 'Remove field';

  @override
  String get fieldGenericField => 'Field';

  @override
  String get detailBackToList => 'Back to list';

  @override
  String get detailAddFavorite => 'Add to favorites  ·  Ctrl+D';

  @override
  String get detailRemoveFavorite => 'Remove from favorites  ·  Ctrl+D';

  @override
  String get detailOpenWebsite => 'Open website';

  @override
  String get detailOpenInBrowser => 'Open in browser';

  @override
  String get detailCopyPasswordShortcut => 'Copy password  ·  Ctrl+Shift+C';

  @override
  String get detailCopyUsernameShortcut => 'Copy username  ·  Ctrl+Shift+U';

  @override
  String get detailUnsavedChanges => 'Unsaved changes';

  @override
  String get detailRevert => 'Revert';

  @override
  String get detailSave => 'Save';

  @override
  String get detailSaved => 'Saved';

  @override
  String get detailNameRequired => 'A name is required.';

  @override
  String get detailNameTaken => 'Another entry already uses that name.';

  @override
  String get detailLegacyBanner =>
      'Imported from an older vault, where entries were plain text. Your text is in Notes; filling any other field upgrades this entry to the structured format on the next save.';

  @override
  String get detailChangesDiscarded => 'Changes discarded';

  @override
  String get detailUnsavedPrompt =>
      'You edited this entry but did not save. Keep the changes or drop them?';

  @override
  String get detailSaveChanges => 'Save changes';

  @override
  String get detailDiscard => 'Discard';

  @override
  String get detailPasswordGenerated => 'Password generated';

  @override
  String get detailPasswordGeneratedDetail =>
      'Not saved yet — press Ctrl+S to store it.';

  @override
  String get detailCreated => 'Created';

  @override
  String get detailLastModified => 'Last modified';

  @override
  String get detailLastOpened => 'Last opened';

  @override
  String get detailStorageFormat => 'Storage format';

  @override
  String get detailUnknownLegacy => 'Unknown (legacy entry)';

  @override
  String get detailFormatLegacy => 'Legacy plain text';

  @override
  String get detailFormatStructured => 'Structured (BOY1)';

  @override
  String get secretReveal => 'Reveal password';

  @override
  String get secretHide => 'Hide password';

  @override
  String get secretCopyShortcut => 'Copy password  ·  Ctrl+Shift+C';

  @override
  String get secretGenerate => 'Generate a new password';

  @override
  String get createDialogTitle => 'New entry';

  @override
  String get createDialogSubtitle => 'Stored encrypted in your local vault.';

  @override
  String get createDialogNameHint => 'GitHub, Bank, Home server...';

  @override
  String get createDialogOptional => 'Optional';

  @override
  String get createDialogPasswordHint => 'Optional — generate one';

  @override
  String get createDialogFooter =>
      'You can add notes, tags and custom fields after creating it.';

  @override
  String get createDialogNameEmpty => 'Give the entry a name.';

  @override
  String createDialogNameExists(String title) {
    return 'An entry named \"$title\" already exists.';
  }

  @override
  String get createDialogSaveFailed =>
      'The entry could not be saved. Check the Security tab.';

  @override
  String get createDialogCreate => 'Create entry';

  @override
  String get createDialogEntryCreated => 'Entry created';

  @override
  String get commonCancel => 'Cancel';

  @override
  String get commonConfirm => 'Confirm';

  @override
  String get commonClose => 'Close';

  @override
  String get commonRetry => 'Retry';

  @override
  String get commonUndo => 'Undo';

  @override
  String get commonOpen => 'Open';

  @override
  String get commonDelete => 'Delete';

  @override
  String get commonClear => 'Clear';

  @override
  String get historyTitle => 'PASSWORD HISTORY';

  @override
  String get historyEmptyHint =>
      'When you change this password, the previous one is kept here so a mistaken change can be undone.';

  @override
  String historyCountOf(int count, int max) {
    return '$count of $max';
  }

  @override
  String get historyClear => 'Clear history';

  @override
  String historyCap(int max) {
    return 'History lives inside the encrypted vault and is capped at $max entries.';
  }

  @override
  String get historyClearTitle => 'Clear password history?';

  @override
  String get historyClearMessage =>
      'Every superseded password of this entry is deleted from the vault. This cannot be undone.';

  @override
  String get historyCleared => 'Password history cleared';

  @override
  String historyReplacedAt(String relative, String absolute) {
    return 'Replaced $relative · $absolute';
  }

  @override
  String get historyReveal => 'Reveal';

  @override
  String get historyHide => 'Hide';

  @override
  String get historyCopy => 'Copy this password';

  @override
  String get historyRestore => 'Use this password again';

  @override
  String get historyForget => 'Forget this password';

  @override
  String get historyRestoreTitle => 'Use this password again?';

  @override
  String get historyRestoreMessage =>
      'The entry goes back to this password. The one in use now is kept in the history, so you can switch back.';

  @override
  String get historyRestoreConfirm => 'Restore';

  @override
  String get historyRestored => 'Password restored';

  @override
  String get historyForgotten => 'Removed from history';

  @override
  String get historyPreviousPassword => 'Previous password';

  @override
  String get paletteHint => 'Search entries or run a command...';

  @override
  String paletteNoMatches(String query) {
    return 'No matches for \"$query\"';
  }

  @override
  String paletteCreateNamed(String query) {
    return 'Create an entry named \"$query\"';
  }

  @override
  String get paletteNavigate => 'navigate';

  @override
  String get paletteOpen => 'open';

  @override
  String get paletteCopyPassword => 'copy password';

  @override
  String get paletteCopyUsername => 'copy username';

  @override
  String get paletteEnterToOpen => 'Enter to open';

  @override
  String get paletteCommandNewEntry => 'Create a credential';

  @override
  String get paletteCommandGenerator => 'Generate a password or passphrase';

  @override
  String get paletteCommandLock =>
      'Clear the session and return to the lock screen';

  @override
  String get paletteCommandFavorites => 'Jump to starred entries';

  @override
  String get paletteCommandRecent => 'Entries you opened last';

  @override
  String get paletteCommandSecurity =>
      'Encryption, vault file and protection settings';

  @override
  String get paletteCommandSettings => 'Appearance, security, keyboard, vault';

  @override
  String get generatorTitle => 'Password generator';

  @override
  String get generatorSubtitle =>
      'Everything is generated locally with the operating system\'s cryptographic random source.';

  @override
  String get generatorModeCharacters => 'Random characters';

  @override
  String get generatorModePassphrase => 'Passphrase';

  @override
  String get generatorCopy => 'Copy';

  @override
  String get generatorRegenerate => 'Regenerate';

  @override
  String get generatorRngNote => 'Generated with a cryptographic RNG';

  @override
  String get generatorLength => 'Length';

  @override
  String get generatorLowercase => 'Lowercase (a-z)';

  @override
  String get generatorUppercase => 'Uppercase (A-Z)';

  @override
  String get generatorDigits => 'Digits (0-9)';

  @override
  String get generatorSymbols => 'Symbols (!@#...)';

  @override
  String get generatorAvoidAmbiguous => 'Avoid look-alike characters';

  @override
  String get generatorNoClassWarning =>
      'At least one character set is required — letters and digits are used until you pick one.';

  @override
  String get generatorWords => 'Words';

  @override
  String get generatorSeparator => 'Separator';

  @override
  String get generatorCapitalize => 'Capitalize words';

  @override
  String get generatorAppendNumber => 'Append a number';

  @override
  String generatorWordListNote(int words, int bits) {
    return '$words word list · each word adds about $bits bits of entropy.';
  }

  @override
  String get generatorSessionTitle => 'This session';

  @override
  String get generatorSessionSubtitle =>
      'Previously generated values, kept in memory only.';

  @override
  String get generatorDialogTitle => 'Generate a password';

  @override
  String get generatorDialogSubtitle =>
      'Pick the shape you need, then use it in the entry.';

  @override
  String get generatorUsePassword => 'Use password';

  @override
  String get securityTitle => 'Security';

  @override
  String get securitySubtitle =>
      'How this vault is encrypted, stored and protected.';

  @override
  String get securityLockNow => 'Lock now';

  @override
  String get securitySession => 'Session';

  @override
  String get securitySessionSubtitle => 'Current state of the unlocked vault.';

  @override
  String get securityStateSaving => 'Saving...';

  @override
  String get securityStateSaveFailed => 'Save failed';

  @override
  String get securityStateSaved => 'Saved';

  @override
  String get securityStateUnlocked => 'Unlocked';

  @override
  String get securityEntries => 'Entries';

  @override
  String get securityLastWritten => 'Last written';

  @override
  String get securityMasterPasswordMemory => 'Master password in memory';

  @override
  String get securityMasterPasswordMemoryValue =>
      'No — only the derived key is kept while unlocked, and it is wiped on lock.';

  @override
  String get securityRenderingEngine => 'Rendering engine';

  @override
  String get securityRetrySave => 'Retry save';

  @override
  String get securityEncryption => 'Encryption';

  @override
  String get securityEncryptionSubtitle => 'Primitives used by this build.';

  @override
  String get securityCipher => 'Cipher';

  @override
  String get securityCipherValue => 'AES-256-GCM (128-bit authentication tag)';

  @override
  String get securityKeyDerivation => 'Key derivation';

  @override
  String get securityVaultFormat => 'Vault format';

  @override
  String get securityFormatUnknown => 'Unknown';

  @override
  String get securityFormatLegacySuffix => ' (legacy)';

  @override
  String get securityMemoryHard => 'Memory-hard';

  @override
  String get securityMemoryHardNo => 'No — this file still uses PBKDF2';

  @override
  String get securityMemoryHardYes =>
      'Yes — Argon2id resists GPU and ASIC cracking';

  @override
  String get securityHeaderAuth => 'Header authentication';

  @override
  String get securityHeaderAuthValue =>
      'The full header (version, KDF, parameters, salt, nonce) is authenticated as GCM associated data.';

  @override
  String get securityNonce => 'Nonce';

  @override
  String get securityNonceValue => 'Fresh 96-bit random nonce for every save';

  @override
  String get securityLegacyFormatTitle => 'Legacy vault format detected';

  @override
  String get securityOldKdfTitle => 'Older key derivation detected';

  @override
  String securityOldKdfMessage(String current, String target) {
    return 'This file was written before Argon2id ($current). The next successful unlock re-encrypts it with $target.';
  }

  @override
  String get securityArgonNote =>
      'Argon2id is memory-hard, so cracking hardware cannot trade memory for parallelism the way it can against PBKDF2. A long, unique master password is still what protects the vault.';

  @override
  String get securityHealthTitle => 'Password health';

  @override
  String get securityHealthSubtitle =>
      'Reuse and strength across every stored password.';

  @override
  String securityHealthScore(int score) {
    return '$score / 100';
  }

  @override
  String get securityHealthNoPasswords => 'No passwords yet';

  @override
  String get securityHealthWithPassword => 'With password';

  @override
  String get securityHealthReused => 'Reused';

  @override
  String get securityHealthWeak => 'Weak';

  @override
  String get securityHealthNoPassword => 'No password';

  @override
  String get securityHealthEmptyMessage =>
      'Nothing to analyse yet — add a credential with a password.';

  @override
  String get securityHealthCleanMessage =>
      'Every password is unique and none of them look weak.';

  @override
  String get securityHealthReusedTitle => 'REUSED PASSWORDS';

  @override
  String securityHealthSharedBy(int count) {
    return 'Shared by $count entries';
  }

  @override
  String securityHealthMoreGroups(int count) {
    return '+ $count more groups';
  }

  @override
  String get securityHealthReuseNote =>
      'One leaked service exposes every entry sharing that password. Give each of them its own generated password.';

  @override
  String get securityHealthWeakTitle => 'WEAK PASSWORDS';

  @override
  String securityHealthMore(int count) {
    return '+ $count more';
  }

  @override
  String get securityVaultFile => 'Vault file';

  @override
  String get securityVaultFileSubtitle =>
      'Everything lives in a single encrypted file.';

  @override
  String get securityRefresh => 'Refresh';

  @override
  String get securityLocation => 'Location';

  @override
  String get securityResolving => 'Resolving...';

  @override
  String get securityCopyPath => 'Copy path';

  @override
  String get securityOpenFolder => 'Open containing folder';

  @override
  String get securitySize => 'Size';

  @override
  String get securityModified => 'Modified';

  @override
  String get securityBackupCopy => 'Backup copy';

  @override
  String get securityBackupPresent =>
      'credentials.plf.bak kept next to the vault';

  @override
  String get securityBackupOnNextSave =>
      'Created automatically on the next save';

  @override
  String get securityWrites => 'Writes';

  @override
  String get securityWritesValue =>
      'Atomic: written to a temp file, then renamed over the vault, so an interrupted save cannot corrupt it.';

  @override
  String get securityFolderOpenFailed => 'The folder could not be opened';

  @override
  String get securityVaultPathLabel => 'Vault path';

  @override
  String get securityProtection => 'Protection';

  @override
  String get securityProtectionSubtitle =>
      'Session behaviour you control in Settings.';

  @override
  String get securityMinimizeLabel => 'Minimize / Alt-Tab';

  @override
  String get securityMinimizeValue =>
      'Never locks the vault — copy a secret, switch apps and come back to an open vault.';

  @override
  String securityClipboardCleared(int seconds) {
    return 'Cleared after ${seconds}s, and only if the copied value is still there';
  }

  @override
  String get securityClipboardNever => 'Never cleared automatically';

  @override
  String get securitySecretsOnScreen => 'Secrets on screen';

  @override
  String get securitySecretsRevealed => 'Revealed by default in the editor';

  @override
  String get securitySecretsHidden => 'Hidden until you reveal them';

  @override
  String get securityNetwork => 'Network';

  @override
  String get securityNetworkValue =>
      'None. No telemetry, no sync, no favicon lookups, no update checks.';

  @override
  String get securityChangeMasterPassword => 'Change master password';

  @override
  String get changePasswordSubtitle => 'The vault is re-encrypted immediately.';

  @override
  String get changePasswordCurrent => 'Current master password';

  @override
  String get changePasswordNew => 'New master password';

  @override
  String get changePasswordConfirm => 'Confirm new password';

  @override
  String changePasswordTooShort(int count) {
    return 'The new password needs at least $count characters.';
  }

  @override
  String get changePasswordMismatch => 'The new passwords do not match.';

  @override
  String get changePasswordBackupNote =>
      'Keep your backups in mind: copies of the old vault file still need the old password.';

  @override
  String get changePasswordAction => 'Change password';

  @override
  String get changePasswordDone => 'Master password changed';

  @override
  String get changePasswordDoneDetail =>
      'The vault was re-encrypted with the new key.';

  @override
  String get settingsTitle => 'Settings';

  @override
  String get settingsSubtitle =>
      'Appearance, security behaviour, keyboard and vault storage.';

  @override
  String get settingsAppearance => 'Appearance';

  @override
  String get settingsTheme => 'Theme';

  @override
  String get settingsThemeDescription =>
      'All variants are dark; pick the one you prefer.';

  @override
  String get settingsDensity => 'Density';

  @override
  String get settingsDensityDescription =>
      'Compact fits more rows on smaller windows.';

  @override
  String get settingsAnimations => 'Animations';

  @override
  String get settingsAnimationsDescription =>
      'Turn off transitions and micro-interactions entirely.';

  @override
  String get settingsLanguage => 'Language';

  @override
  String get settingsLanguageDescription =>
      'Interface language. \"System\" follows Windows.';

  @override
  String get settingsLanguageSystem => 'System';

  @override
  String get settingsLanguageEnglish => 'English';

  @override
  String get settingsLanguageItalian => 'Italiano';

  @override
  String get themeMidnight => 'Midnight';

  @override
  String get themeMidnightDescription => 'Deep indigo base with violet accent';

  @override
  String get themeObsidian => 'Obsidian';

  @override
  String get themeObsidianDescription => 'Neutral graphite with cyan accent';

  @override
  String get themeViolet => 'Violet';

  @override
  String get themeVioletDescription =>
      'Saturated violet, higher contrast accents';

  @override
  String get densityComfortable => 'Comfortable';

  @override
  String get densityCompact => 'Compact';

  @override
  String get settingsSecurity => 'Security';

  @override
  String get settingsAutoLock => 'Auto-lock after inactivity';

  @override
  String get settingsAutoLockDescription =>
      'Minimizing, Alt-Tab or losing focus never lock the vault — only this timer and the manual lock do.';

  @override
  String get settingsNever => 'Never';

  @override
  String settingsMinutesShort(int minutes) {
    return '${minutes}m';
  }

  @override
  String settingsSecondsShort(int seconds) {
    return '${seconds}s';
  }

  @override
  String get settingsClipboard => 'Clear clipboard after copying';

  @override
  String get settingsClipboardDescription =>
      'Only clears if the copied secret is still on the clipboard.';

  @override
  String get settingsRevealSecrets => 'Reveal secrets by default';

  @override
  String get settingsRevealSecretsDescription =>
      'When off, passwords and secret fields start hidden.';

  @override
  String get settingsConfirmDelete => 'Confirm before deleting';

  @override
  String get settingsConfirmDeleteDescription =>
      'Deleting always offers an undo; this adds a dialog first.';

  @override
  String get settingsKeepHistory => 'Keep password history';

  @override
  String settingsKeepHistoryDescription(int max) {
    return 'Superseded passwords stay on the entry (up to $max) so a bad change can be undone. They are stored encrypted, inside the vault.';
  }

  @override
  String get settingsStoredHistory => 'Stored password history';

  @override
  String get settingsStoredHistoryEmpty =>
      'No superseded passwords are stored right now.';

  @override
  String settingsStoredHistoryCount(int count, int entries) {
    return '$count superseded passwords across $entries entries.';
  }

  @override
  String get settingsClearAll => 'Clear all';

  @override
  String get settingsClearAllTitle => 'Clear all password history?';

  @override
  String get settingsClearAllMessage =>
      'Every superseded password in the vault is deleted. Current passwords are untouched. This cannot be undone.';

  @override
  String settingsClearAllDetail(int count) {
    return '$count stored passwords';
  }

  @override
  String get settingsClearAllConfirm => 'Clear everything';

  @override
  String settingsClearAllDone(int count) {
    return '$count removed';
  }

  @override
  String get settingsMasterPassword => 'Master password';

  @override
  String get settingsMasterPasswordDescription =>
      'Re-encrypts the vault with a new key.';

  @override
  String get settingsChange => 'Change';

  @override
  String get settingsKeyboard => 'Keyboard';

  @override
  String get settingsGlobalShortcut => 'Global shortcut (Ctrl+Alt+P)';

  @override
  String get settingsGlobalShortcutDescription =>
      'Brings BetterOnlyYours forward from any application and opens the command palette.';

  @override
  String get settingsShortcuts => 'Shortcuts';

  @override
  String get settingsShortcutsDescription =>
      'Everything reachable without the mouse.';

  @override
  String get settingsVaultWindow => 'Vault & window';

  @override
  String get settingsVaultFile => 'Vault file';

  @override
  String get settingsVaultFileResolving => 'Resolving the vault location...';

  @override
  String get settingsRememberWindow => 'Remember window size and position';

  @override
  String get settingsRememberWindowDescription =>
      'Restores the window where you left it on the next start.';

  @override
  String get settingsAbout => 'About';

  @override
  String get settingsVersion => 'Version';

  @override
  String get settingsStorage => 'Storage';

  @override
  String get settingsStorageValue =>
      'A single encrypted file, written atomically.';

  @override
  String get settingsNetworkAccess => 'Network access';

  @override
  String get settingsNetworkAccessValue =>
      'None — the app never connects to anything.';

  @override
  String actionCopied(String label) {
    return '$label copied';
  }

  @override
  String actionCopiedDetail(int seconds) {
    return 'Clipboard clears in ${seconds}s unless you copy something else.';
  }

  @override
  String get actionClipboardUnavailable => 'Clipboard unavailable';

  @override
  String get actionClipboardUnavailableDetail =>
      'Windows refused the copy — another app may be holding it.';

  @override
  String get labelUsername => 'Username';

  @override
  String get labelPassword => 'Password';

  @override
  String get actionNoUsername => 'No username on this entry';

  @override
  String get actionNoUsernameDetail => 'Add one from the entry details.';

  @override
  String get actionNoPassword => 'No password on this entry';

  @override
  String get actionNoPasswordLegacy =>
      'This entry still holds free-form notes only.';

  @override
  String get actionNoPasswordDetail =>
      'Add or generate one from the entry details.';

  @override
  String get actionNoWebsite => 'No website on this entry';

  @override
  String get actionBadUrl => 'That address cannot be opened';

  @override
  String get actionBadUrlDetail => 'Only http and https links are launched.';

  @override
  String get actionNoBrowser => 'No browser responded';

  @override
  String get actionLinkFailed => 'The link could not be opened';

  @override
  String get actionFavoriteAdded => 'Added to favorites';

  @override
  String get actionFavoriteRemoved => 'Removed from favorites';

  @override
  String get actionDuplicated => 'Entry duplicated';

  @override
  String get actionDeleteTitle => 'Delete this entry?';

  @override
  String get actionDeleteMessage =>
      'It is removed from the encrypted vault. You can undo this right after, from the notification.';

  @override
  String get actionDeleted => 'Entry deleted';

  @override
  String get menuOpen => 'Open';

  @override
  String get menuCopyUsername => 'Copy username';

  @override
  String get menuCopyPassword => 'Copy password';

  @override
  String get menuOpenWebsite => 'Open website';

  @override
  String get menuAddFavorite => 'Add to favorites';

  @override
  String get menuRemoveFavorite => 'Remove from favorites';

  @override
  String get menuDuplicate => 'Duplicate';

  @override
  String get menuDelete => 'Delete';

  @override
  String get vaultLocked => 'Vault locked';

  @override
  String vaultLockedInactivity(int minutes) {
    return 'Locked after $minutes minutes of inactivity.';
  }

  @override
  String get vaultRestoredBackup => 'Vault restored from backup';

  @override
  String get vaultRestoredBackupDetail =>
      'The main file could not be read, so the .bak copy was opened.';

  @override
  String get vaultUpgraded => 'Vault upgraded';

  @override
  String vaultUpgradedDetail(String kdf) {
    return 'Re-encrypted with stronger key derivation ($kdf).';
  }

  @override
  String vaultSaveRetryHint(String hint) {
    return '$hint Your changes are still here — retry the save.';
  }

  @override
  String get vaultSaveFailed => 'Vault could not be saved';

  @override
  String get vaultSaveFailedDetail =>
      'Your changes are still here — retry the save.';

  @override
  String get errorNotFoundTitle => 'Vault file not found';

  @override
  String get errorNotFoundHint =>
      'Restore your vault file or create a new vault to continue.';

  @override
  String get errorInvalidPasswordTitle => 'Master password not accepted';

  @override
  String get errorInvalidPasswordHint =>
      'Check for Caps Lock and keyboard layout. If the password is right, the file may have been modified on disk.';

  @override
  String get errorBadSignatureTitle =>
      'This file is not a BetterOnlyYours vault';

  @override
  String get errorBadSignatureHint =>
      'The file header does not match the vault format. Restore a backup copy.';

  @override
  String get errorUnsupportedVersionTitle => 'Unsupported vault version';

  @override
  String get errorUnsupportedVersionHint =>
      'The vault was written by a newer version of BetterOnlyYours. Update the app to open it.';

  @override
  String get errorTruncatedTitle => 'Vault file is incomplete';

  @override
  String get errorTruncatedHint =>
      'The file is shorter than a valid vault. Restore the .bak backup next to it.';

  @override
  String get errorMalformedTitle => 'Vault contents could not be read';

  @override
  String get errorMalformedHint =>
      'Decryption succeeded but the contents are not valid vault data.';

  @override
  String get errorPermissionTitle => 'Access to the vault file was denied';

  @override
  String get errorPermissionHint =>
      'Close other apps using the file, or run from a folder you can write to.';

  @override
  String get errorIoTitle => 'Vault could not be read or written';

  @override
  String get errorIoHint =>
      'Check that the vault folder exists and has free space, then retry.';

  @override
  String get strengthEmpty => '—';

  @override
  String get strengthVeryWeak => 'Very weak';

  @override
  String get strengthWeak => 'Weak';

  @override
  String get strengthFair => 'Fair';

  @override
  String get strengthStrong => 'Strong';

  @override
  String get strengthExcellent => 'Excellent';

  @override
  String strengthBits(int bits) {
    return '~ $bits bits';
  }

  @override
  String get strengthEstimateNote =>
      'Estimate only — based on length, character mix and obvious patterns.';

  @override
  String get inspectorTitle => 'INSPECTOR';

  @override
  String get inspectorPasswordStrength => 'Password strength';

  @override
  String get inspectorReused => 'This password is also used by another entry.';

  @override
  String get inspectorQuickActions => 'Quick actions';

  @override
  String get inspectorDuplicate => 'Duplicate entry';

  @override
  String get inspectorDelete => 'Delete entry';

  @override
  String get inspectorTimeline => 'Timeline';

  @override
  String get inspectorCreated => 'Created';

  @override
  String get inspectorModified => 'Modified';

  @override
  String get inspectorOpened => 'Opened';

  @override
  String get inspectorFormat => 'Format';

  @override
  String get inspectorFormatLegacy => 'Legacy text';

  @override
  String get inspectorFormatStructured => 'Structured';

  @override
  String get inspectorHistory => 'History';

  @override
  String get inspectorNoHistory => 'No previous passwords';

  @override
  String get inspectorUnknown => 'Unknown';

  @override
  String get inspectorTags => 'Tags';

  @override
  String get inspectorRelated => 'Related';

  @override
  String get vaultPageFavoritesTitle => 'Favorites';

  @override
  String get vaultPageFavoritesMessage =>
      'Pick an entry on the left, or star the ones you use most so they land here.';

  @override
  String get vaultPageRecentTitle => 'Recently opened';

  @override
  String get vaultPageRecentMessage =>
      'Your last opened entries are listed on the left. Select one to see it here.';

  @override
  String get vaultPageRecentHint =>
      'History is stored inside the encrypted vault, never on disk in the clear.';

  @override
  String get shortcutPalette => 'Command palette';

  @override
  String get shortcutPaletteGlobal =>
      'Command palette from anywhere in Windows';

  @override
  String get shortcutNewEntry => 'New entry';

  @override
  String get shortcutFocusSearch => 'Focus the vault search field';

  @override
  String get shortcutSave => 'Save the entry being edited';

  @override
  String get shortcutFavorite => 'Toggle favorite on the selected entry';

  @override
  String get shortcutCopyPassword => 'Copy the selected password';

  @override
  String get shortcutCopyUsername => 'Copy the selected username';

  @override
  String get shortcutGenerator => 'Password generator';

  @override
  String get shortcutSettings => 'Settings';

  @override
  String get shortcutLock => 'Lock the vault';

  @override
  String get shortcutDelete => 'Delete the selected entry';

  @override
  String get shortcutEscape => 'Close the palette, dialog or search';

  @override
  String get shortcutNavigate => 'Move through results and open';

  @override
  String get settingsGlobalShortcutFailed =>
      'Ctrl+Alt+P could not be registered — another application is probably using it.';

  @override
  String get vaultLockedManual => 'Vault locked';

  @override
  String get totpTitle => 'Two-factor code';

  @override
  String get totpEmptyHint =>
      'Add the setup key or the otpauth:// link from the service and this entry generates its own codes.';

  @override
  String get totpAdd => 'Add 2FA';

  @override
  String get totpReplace => 'Replace';

  @override
  String get totpRemove => 'Remove';

  @override
  String get totpSave => 'Save token';

  @override
  String get totpCopy => 'Copy code';

  @override
  String get totpLabel => '2FA code';

  @override
  String totpDetails(String algorithm, int digits, int period) {
    return '$algorithm · $digits digits · ${period}s';
  }

  @override
  String get totpSecretHidden =>
      'The secret is sealed inside the vault and is never shown again — only the codes it produces.';

  @override
  String get totpUnreadable => 'This token cannot be opened';

  @override
  String get totpUnreadableDetail =>
      'The key that seals it is missing from this vault. Remove the token and set it up again from the service.';

  @override
  String get totpRemoveTitle => 'Remove two-factor?';

  @override
  String get totpRemoveMessage =>
      'The sealed secret is deleted. You will need the setup key from the service to add it back.';

  @override
  String get totpRemoved => 'Two-factor removed';

  @override
  String get totpSaved => 'Two-factor enabled';

  @override
  String get totpReplaced => 'Two-factor secret replaced';

  @override
  String get totpSaveFailed => 'The token could not be stored.';

  @override
  String get totpSetupTitle => 'Set up two-factor';

  @override
  String get totpSetupReplaceTitle => 'Replace two-factor secret';

  @override
  String get totpSecretLabel => 'Setup key or otpauth:// link';

  @override
  String get totpSecretHelper =>
      'Paste the base32 key the service shows next to the QR code, or the whole otpauth:// link.';

  @override
  String get totpUriDetected => 'otpauth:// link recognized';

  @override
  String totpUriDetectedFor(String label) {
    return 'otpauth:// link recognized — $label';
  }

  @override
  String get totpPreview => 'Live preview';

  @override
  String get totpAdvanced => 'Advanced settings';

  @override
  String get totpHideAdvanced => 'Hide advanced settings';

  @override
  String get totpAlgorithm => 'Algorithm';

  @override
  String get totpDigits => 'Digits';

  @override
  String get totpPeriod => 'Period';

  @override
  String totpSecondsShort(int count) {
    return '${count}s';
  }

  @override
  String get totpKind => 'Type';

  @override
  String get totpKindStandard => 'Standard';

  @override
  String get totpKindSteam => 'Steam';

  @override
  String get totpWriteOnlyNotice =>
      'Check the preview matches the code the service shows. Once saved, the secret can never be read back out of BetterOnlyYours.';

  @override
  String get menuCopyTotp => 'Copy 2FA code';

  @override
  String get actionNoTotp => 'No two-factor code';

  @override
  String get actionNoTotpDetail =>
      'Add a token to this entry to generate codes.';

  @override
  String get totpBadgeTooltip => 'Two-factor enabled';

  @override
  String get securityTotpTitle => 'Two-factor tokens';

  @override
  String get securityTotpDescription =>
      'Entries generating their own codes. Each secret is sealed with its own key derived from a vault-wide token key.';

  @override
  String securityTotpValue(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count entries',
      one: '1 entry',
      zero: 'No entries',
    );
    return '$_temp0';
  }

  @override
  String get shortcutCopyTotp => 'Copy the selected 2FA code';

  @override
  String get paletteCommandCopyTotp =>
      'Copy the 2FA code of the selected entry';
}
