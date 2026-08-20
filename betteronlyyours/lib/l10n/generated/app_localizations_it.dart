// ignore: unused_import
import 'package:intl/intl.dart' as intl;
import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for Italian (`it`).
class AppLocalizationsIt extends AppLocalizations {
  AppLocalizationsIt([String locale = 'it']) : super(locale);

  @override
  String get appTagline =>
      'Una cassaforte locale e cifrata che resta solo tua.';

  @override
  String get appDescription =>
      'BetterOnlyYours conserva credenziali e segreti in un unico file cifrato su questo computer. Nessun account, nessuna sincronizzazione, nessuna telemetria: la cassaforte è leggibile solo con la tua password principale.';

  @override
  String get navVault => 'Cassaforte';

  @override
  String get navFavorites => 'Preferiti';

  @override
  String get navRecent => 'Recenti';

  @override
  String get navGenerator => 'Generatore';

  @override
  String get navSecurity => 'Sicurezza';

  @override
  String get navSettings => 'Impostazioni';

  @override
  String get navLockVault => 'Blocca cassaforte';

  @override
  String get navCollapse => 'Comprimi';

  @override
  String get windowMinimize => 'Riduci a icona';

  @override
  String get windowMaximize => 'Ingrandisci';

  @override
  String get windowRestore => 'Ripristina';

  @override
  String get windowClose => 'Chiudi';

  @override
  String get actionSearchTooltip => 'Riquadro comandi  ·  Ctrl+K';

  @override
  String get actionNewEntryTooltip => 'Nuova voce  ·  Ctrl+N';

  @override
  String get actionLockTooltip => 'Blocca cassaforte  ·  Ctrl+L';

  @override
  String statusUnlockedEntries(String entries) {
    return 'Sbloccata · $entries';
  }

  @override
  String get statusEncrypting => 'Cifratura in corso…';

  @override
  String get statusSaveFailed => 'Salvataggio fallito — clicca per riprovare';

  @override
  String get statusNoChanges => 'Ancora nessuna modifica';

  @override
  String statusSavedAgo(String when) {
    return 'Salvata $when';
  }

  @override
  String statusClipboardClears(String label, int seconds) {
    return '$label si cancella tra ${seconds}s';
  }

  @override
  String get statusKeep => 'mantieni';

  @override
  String statusAutoLockOn(int minutes) {
    return 'Blocco automatico ${minutes}m';
  }

  @override
  String get statusAutoLockOff => 'Blocco automatico disattivo';

  @override
  String get authLocalVault => 'Cassaforte cifrata locale';

  @override
  String get authVaultLocked => 'Cassaforte bloccata';

  @override
  String get authUnlockPrompt =>
      'Inserisci la password principale per decifrare questa cassaforte.';

  @override
  String get authMasterPassword => 'Password principale';

  @override
  String get authUnlockButton => 'Sblocca cassaforte';

  @override
  String get authCapsLockOn => 'Bloc Maiusc è attivo';

  @override
  String get authShowPassword => 'Mostra password';

  @override
  String get authHidePassword => 'Nascondi password';

  @override
  String get authLocalOnlyNote =>
      'Tutto resta su questo computer. Nessun account, nessuna sincronizzazione e nessun ripristino della password.';

  @override
  String get setupTitle => 'Configura la cassaforte';

  @override
  String get setupHeadline => 'Una password, un file, nessun cloud';

  @override
  String get setupIntro =>
      'Le tue voci sono cifrate con AES-256-GCM usando una chiave derivata dalla password principale. Il file della cassaforte non lascia mai questo computer e nulla può decifrarlo.';

  @override
  String get setupPasswordHint => 'Scegli qualcosa di lungo e memorabile';

  @override
  String get setupConfirmLabel => 'Conferma password principale';

  @override
  String get setupConfirmHint => 'Scrivila un’altra volta';

  @override
  String setupRequirementLength(int count) {
    return 'Almeno $count caratteri';
  }

  @override
  String get setupRequirementMatch => 'Le due voci coincidono';

  @override
  String get setupAcknowledgement =>
      'Ho capito che questa password non può essere recuperata né reimpostata. Se la perdo, la cassaforte resta cifrata per sempre: terrò quindi una copia di backup del file in un posto sicuro.';

  @override
  String get setupCreateButton => 'Crea cassaforte';

  @override
  String setupVaultFile(String path) {
    return 'File della cassaforte: $path';
  }

  @override
  String setupErrorFields(int count) {
    return 'Usa almeno $count caratteri.';
  }

  @override
  String get setupErrorMismatch => 'Le due password non coincidono.';

  @override
  String get setupErrorAcknowledge =>
      'Conferma di aver capito che non esiste alcun recupero della password.';

  @override
  String get setupHide => 'Nascondi';

  @override
  String get setupShow => 'Mostra';

  @override
  String get timeNever => 'mai';

  @override
  String get timeJustNow => 'proprio ora';

  @override
  String timeMinutesAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count minuti fa',
      one: '1 minuto fa',
    );
    return '$_temp0';
  }

  @override
  String timeHoursAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count ore fa',
      one: '1 ora fa',
    );
    return '$_temp0';
  }

  @override
  String timeDaysAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count giorni fa',
      one: '1 giorno fa',
    );
    return '$_temp0';
  }

  @override
  String timeWeeksAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count settimane fa',
      one: '1 settimana fa',
    );
    return '$_temp0';
  }

  @override
  String timeMonthsAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count mesi fa',
      one: '1 mese fa',
    );
    return '$_temp0';
  }

  @override
  String timeYearsAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count anni fa',
      one: '1 anno fa',
    );
    return '$_temp0';
  }

  @override
  String entriesCount(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count voci',
      one: '1 voce',
    );
    return '$_temp0';
  }

  @override
  String previousPasswordsCount(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count password precedenti',
      one: '1 password precedente',
    );
    return '$_temp0';
  }

  @override
  String get listSearchHint => 'Cerca nella cassaforte...';

  @override
  String get listSearchSemantics => 'Cerca nella cassaforte';

  @override
  String get listClearSearch => 'Cancella ricerca';

  @override
  String get listSortTooltip => 'Ordina e filtra';

  @override
  String get listFavoritesOnly => 'Solo preferiti';

  @override
  String listClearTagFilter(String tag) {
    return 'Rimuovi filtro etichetta ($tag)';
  }

  @override
  String listShownOfTotal(int shown, int total) {
    return '$shown di $total';
  }

  @override
  String get listClearFilters => 'Azzera i filtri';

  @override
  String get listEntryActions => 'Azioni voce';

  @override
  String get emptySearchTitle => 'Nessun risultato';

  @override
  String get emptySearchMessage =>
      'Nessuna voce corrisponde alla ricerca o ai filtri attuali.';

  @override
  String emptySearchCreate(String query) {
    return 'Crea \"$query\"';
  }

  @override
  String get emptyFavoritesTitle => 'Ancora nessun preferito';

  @override
  String get emptyFavoritesMessage =>
      'Metti una stella alle voci che usi ogni giorno e appariranno qui.';

  @override
  String get emptyFavoritesHint =>
      'Ctrl+D attiva o disattiva il preferito sulla voce selezionata.';

  @override
  String get emptyRecentTitle => 'Ancora nulla di aperto';

  @override
  String get emptyRecentMessage =>
      'Le voci che apri compaiono qui, dalla piu recente. La cronologia resta dentro la cassaforte cifrata.';

  @override
  String get emptyVaultTitle => 'La cassaforte e vuota';

  @override
  String get emptyVaultMessage =>
      'Crea la tua prima voce: viene cifrata nel momento in cui la salvi.';

  @override
  String get emptyVaultHint => 'Ctrl+N crea una voce da qualunque schermata.';

  @override
  String get newEntry => 'Nuova voce';

  @override
  String get sortTitleAsc => 'Nome (A-Z)';

  @override
  String get sortTitleDesc => 'Nome (Z-A)';

  @override
  String get sortRecentlyUsed => 'Aperte di recente';

  @override
  String get sortRecentlyUpdated => 'Modificate di recente';

  @override
  String dashboardSubtitle(String entries) {
    return '$entries cifrate su questo computer. Seleziona una voce per aprirla o creane una nuova.';
  }

  @override
  String get dashboardReadyTitle => 'La cassaforte e pronta';

  @override
  String get dashboardReadyMessage =>
      'Non c\'e ancora nulla. Crea la tua prima voce: viene cifrata localmente nel momento in cui la salvi.';

  @override
  String get dashboardOpenGenerator => 'Apri generatore';

  @override
  String get dashboardShortcutHint =>
      'Ctrl+N nuova voce · Ctrl+K riquadro comandi · Ctrl+L blocca';

  @override
  String get dashboardSearch => 'Cerca';

  @override
  String get dashboardGeneratePassword => 'Genera password';

  @override
  String get statEntries => 'Voci';

  @override
  String get statEntriesAllStructured => 'Tutte strutturate';

  @override
  String statEntriesLegacy(int count) {
    return '$count legacy';
  }

  @override
  String get statFavorites => 'Preferiti';

  @override
  String get statFavoritesCaption => 'Con stella per accesso rapido';

  @override
  String get statTags => 'Etichette';

  @override
  String get statTagsEmpty => 'Aggiungi etichette per raggruppare le voci';

  @override
  String get statHealth => 'Salute password';

  @override
  String get statHealthClean => 'Nessuna password debole o riutilizzata';

  @override
  String statHealthIssues(int weak, int reused) {
    return '$weak deboli · $reused riutilizzate';
  }

  @override
  String get dashboardRecentlyOpened => 'Aperte di recente';

  @override
  String get dashboardRecentlyOpenedEmpty => 'Apri una voce e comparira qui.';

  @override
  String get dashboardRecentlyModified => 'Modificate di recente';

  @override
  String get dashboardRecentlyModifiedEmpty =>
      'Le voci che modifichi compaiono qui con la data.';

  @override
  String get dashboardSecuritySnapshot => 'Riepilogo di sicurezza';

  @override
  String get dashboardSecuritySnapshotSubtitle =>
      'Impostazioni di protezione attuali di questa cassaforte.';

  @override
  String get dashboardSecurityCenter => 'Centro sicurezza';

  @override
  String get infoEncryption => 'Cifratura';

  @override
  String get infoAutoLock => 'Blocco automatico';

  @override
  String get infoAutoLockOff =>
      'Disattivo — ridurre a icona non blocca mai la cassaforte';

  @override
  String infoAutoLockAfter(int minutes) {
    return 'Dopo $minutes minuti di inattivita';
  }

  @override
  String get infoClipboard => 'Appunti';

  @override
  String get infoClipboardKeep => 'Mantenuti finche non li sostituisci';

  @override
  String infoClipboardClear(int seconds) {
    return 'Cancellati $seconds secondi dopo la copia';
  }

  @override
  String get infoLastSaved => 'Ultimo salvataggio';

  @override
  String dashboardLegacyNote(int count) {
    return '$count voci usano ancora il vecchio formato in chiaro. Aprine una e compila un campo per aggiornarla.';
  }

  @override
  String get fieldName => 'Nome';

  @override
  String get fieldNameHint => 'Nome della voce';

  @override
  String get fieldUsername => 'Nome utente o email';

  @override
  String get fieldNotSet => 'Non impostato';

  @override
  String get fieldPassword => 'Password';

  @override
  String get fieldWebsite => 'Sito web';

  @override
  String get fieldNotes => 'Note';

  @override
  String get fieldNotesHint =>
      'Codici di recupero, promemoria, qualsiasi altra cosa...';

  @override
  String get fieldTags => 'ETICHETTE';

  @override
  String get fieldCustomFields => 'CAMPI PERSONALIZZATI';

  @override
  String get fieldDetails => 'DETTAGLI';

  @override
  String get fieldLabel => 'Etichetta';

  @override
  String get fieldValue => 'Valore';

  @override
  String get fieldTagName => 'Nome etichetta';

  @override
  String get fieldAddTag => 'Aggiungi etichetta';

  @override
  String get fieldSuggestions => 'Suggerimenti';

  @override
  String get fieldAddField => 'Aggiungi campo';

  @override
  String get fieldCustomFieldsEmpty =>
      'Codici di recupero, domande di sicurezza, chiavi API: tutto cio che appartiene a questa voce.';

  @override
  String get fieldCopyValue => 'Copia valore';

  @override
  String get fieldHideValue => 'Nascondi questo valore';

  @override
  String get fieldShowValue => 'Smetti di nascondere questo valore';

  @override
  String get fieldRemoveField => 'Rimuovi campo';

  @override
  String get fieldGenericField => 'Campo';

  @override
  String get detailBackToList => 'Torna all\'elenco';

  @override
  String get detailAddFavorite => 'Aggiungi ai preferiti  ·  Ctrl+D';

  @override
  String get detailRemoveFavorite => 'Rimuovi dai preferiti  ·  Ctrl+D';

  @override
  String get detailOpenWebsite => 'Apri il sito';

  @override
  String get detailOpenInBrowser => 'Apri nel browser';

  @override
  String get detailCopyPasswordShortcut => 'Copia password  ·  Ctrl+Shift+C';

  @override
  String get detailCopyUsernameShortcut => 'Copia nome utente  ·  Ctrl+Shift+U';

  @override
  String get detailUnsavedChanges => 'Modifiche non salvate';

  @override
  String get detailRevert => 'Annulla modifiche';

  @override
  String get detailSave => 'Salva';

  @override
  String get detailSaved => 'Salvato';

  @override
  String get detailNameRequired => 'Il nome e obbligatorio.';

  @override
  String get detailNameTaken => 'Un\'altra voce usa gia questo nome.';

  @override
  String get detailLegacyBanner =>
      'Importata da una cassaforte piu vecchia, dove le voci erano testo in chiaro. Il tuo testo e nelle Note; compilando un altro campo la voce passa al formato strutturato al prossimo salvataggio.';

  @override
  String get detailChangesDiscarded => 'Modifiche annullate';

  @override
  String get detailUnsavedPrompt =>
      'Hai modificato questa voce senza salvare. Vuoi tenere le modifiche o scartarle?';

  @override
  String get detailSaveChanges => 'Salva modifiche';

  @override
  String get detailDiscard => 'Scarta';

  @override
  String get detailPasswordGenerated => 'Password generata';

  @override
  String get detailPasswordGeneratedDetail =>
      'Non ancora salvata: premi Ctrl+S per memorizzarla.';

  @override
  String get detailCreated => 'Creata';

  @override
  String get detailLastModified => 'Ultima modifica';

  @override
  String get detailLastOpened => 'Ultima apertura';

  @override
  String get detailStorageFormat => 'Formato di archiviazione';

  @override
  String get detailUnknownLegacy => 'Sconosciuta (voce legacy)';

  @override
  String get detailFormatLegacy => 'Testo in chiaro legacy';

  @override
  String get detailFormatStructured => 'Strutturato (BOY1)';

  @override
  String get secretReveal => 'Mostra password';

  @override
  String get secretHide => 'Nascondi password';

  @override
  String get secretCopyShortcut => 'Copia password  ·  Ctrl+Shift+C';

  @override
  String get secretGenerate => 'Genera una nuova password';

  @override
  String get createDialogTitle => 'Nuova voce';

  @override
  String get createDialogSubtitle =>
      'Salvata cifrata nella tua cassaforte locale.';

  @override
  String get createDialogNameHint => 'GitHub, Banca, Server di casa...';

  @override
  String get createDialogOptional => 'Facoltativo';

  @override
  String get createDialogPasswordHint => 'Facoltativa: puoi generarla';

  @override
  String get createDialogFooter =>
      'Puoi aggiungere note, etichette e campi personalizzati dopo la creazione.';

  @override
  String get createDialogNameEmpty => 'Assegna un nome alla voce.';

  @override
  String createDialogNameExists(String title) {
    return 'Esiste gia una voce chiamata \"$title\".';
  }

  @override
  String get createDialogSaveFailed =>
      'Impossibile salvare la voce. Controlla la scheda Sicurezza.';

  @override
  String get createDialogCreate => 'Crea voce';

  @override
  String get createDialogEntryCreated => 'Voce creata';

  @override
  String get commonCancel => 'Annulla';

  @override
  String get commonConfirm => 'Conferma';

  @override
  String get commonClose => 'Chiudi';

  @override
  String get commonRetry => 'Riprova';

  @override
  String get commonUndo => 'Annulla';

  @override
  String get commonOpen => 'Apri';

  @override
  String get commonDelete => 'Elimina';

  @override
  String get commonClear => 'Cancella';

  @override
  String get historyTitle => 'CRONOLOGIA PASSWORD';

  @override
  String get historyEmptyHint =>
      'Quando cambi questa password, la precedente resta qui per poter annullare una modifica sbagliata.';

  @override
  String historyCountOf(int count, int max) {
    return '$count di $max';
  }

  @override
  String get historyClear => 'Cancella cronologia';

  @override
  String historyCap(int max) {
    return 'La cronologia resta dentro la cassaforte cifrata ed e limitata a $max voci.';
  }

  @override
  String get historyClearTitle => 'Cancellare la cronologia password?';

  @override
  String get historyClearMessage =>
      'Tutte le password precedenti di questa voce vengono eliminate dalla cassaforte. L\'operazione non e reversibile.';

  @override
  String get historyCleared => 'Cronologia password cancellata';

  @override
  String historyReplacedAt(String relative, String absolute) {
    return 'Sostituita $relative · $absolute';
  }

  @override
  String get historyReveal => 'Mostra';

  @override
  String get historyHide => 'Nascondi';

  @override
  String get historyCopy => 'Copia questa password';

  @override
  String get historyRestore => 'Riusa questa password';

  @override
  String get historyForget => 'Dimentica questa password';

  @override
  String get historyRestoreTitle => 'Riusare questa password?';

  @override
  String get historyRestoreMessage =>
      'La voce torna a questa password. Quella attuale resta in cronologia, cosi puoi tornare indietro.';

  @override
  String get historyRestoreConfirm => 'Ripristina';

  @override
  String get historyRestored => 'Password ripristinata';

  @override
  String get historyForgotten => 'Rimossa dalla cronologia';

  @override
  String get historyPreviousPassword => 'Password precedente';

  @override
  String get paletteHint => 'Cerca voci o esegui un comando...';

  @override
  String paletteNoMatches(String query) {
    return 'Nessun risultato per \"$query\"';
  }

  @override
  String paletteCreateNamed(String query) {
    return 'Crea una voce chiamata \"$query\"';
  }

  @override
  String get paletteNavigate => 'naviga';

  @override
  String get paletteOpen => 'apri';

  @override
  String get paletteCopyPassword => 'copia password';

  @override
  String get paletteCopyUsername => 'copia nome utente';

  @override
  String get paletteEnterToOpen => 'Invio per aprire';

  @override
  String get paletteCommandNewEntry => 'Crea una credenziale';

  @override
  String get paletteCommandGenerator => 'Genera una password o una passphrase';

  @override
  String get paletteCommandLock =>
      'Chiude la sessione e torna alla schermata di blocco';

  @override
  String get paletteCommandFavorites => 'Vai alle voci con stella';

  @override
  String get paletteCommandRecent => 'Le ultime voci aperte';

  @override
  String get paletteCommandSecurity =>
      'Cifratura, file della cassaforte e protezioni';

  @override
  String get paletteCommandSettings =>
      'Aspetto, sicurezza, tastiera, cassaforte';

  @override
  String get generatorTitle => 'Generatore di password';

  @override
  String get generatorSubtitle =>
      'Tutto viene generato in locale usando la sorgente casuale crittografica del sistema operativo.';

  @override
  String get generatorModeCharacters => 'Caratteri casuali';

  @override
  String get generatorModePassphrase => 'Passphrase';

  @override
  String get generatorCopy => 'Copia';

  @override
  String get generatorRegenerate => 'Rigenera';

  @override
  String get generatorRngNote => 'Generata con un RNG crittografico';

  @override
  String get generatorLength => 'Lunghezza';

  @override
  String get generatorLowercase => 'Minuscole (a-z)';

  @override
  String get generatorUppercase => 'Maiuscole (A-Z)';

  @override
  String get generatorDigits => 'Cifre (0-9)';

  @override
  String get generatorSymbols => 'Simboli (!@#...)';

  @override
  String get generatorAvoidAmbiguous => 'Evita caratteri ambigui';

  @override
  String get generatorNoClassWarning =>
      'Serve almeno un insieme di caratteri: fino ad allora vengono usate lettere e cifre.';

  @override
  String get generatorWords => 'Parole';

  @override
  String get generatorSeparator => 'Separatore';

  @override
  String get generatorCapitalize => 'Iniziali maiuscole';

  @override
  String get generatorAppendNumber => 'Aggiungi un numero';

  @override
  String generatorWordListNote(int words, int bits) {
    return 'Lista di $words parole · ogni parola aggiunge circa $bits bit di entropia.';
  }

  @override
  String get generatorSessionTitle => 'Questa sessione';

  @override
  String get generatorSessionSubtitle =>
      'Valori generati in precedenza, tenuti solo in memoria.';

  @override
  String get generatorDialogTitle => 'Genera una password';

  @override
  String get generatorDialogSubtitle =>
      'Scegli la forma che ti serve, poi usala nella voce.';

  @override
  String get generatorUsePassword => 'Usa password';

  @override
  String get securityTitle => 'Sicurezza';

  @override
  String get securitySubtitle =>
      'Come questa cassaforte e cifrata, archiviata e protetta.';

  @override
  String get securityLockNow => 'Blocca ora';

  @override
  String get securitySession => 'Sessione';

  @override
  String get securitySessionSubtitle =>
      'Stato attuale della cassaforte sbloccata.';

  @override
  String get securityStateSaving => 'Salvataggio...';

  @override
  String get securityStateSaveFailed => 'Salvataggio fallito';

  @override
  String get securityStateSaved => 'Salvata';

  @override
  String get securityStateUnlocked => 'Sbloccata';

  @override
  String get securityEntries => 'Voci';

  @override
  String get securityLastWritten => 'Ultima scrittura';

  @override
  String get securityMasterPasswordMemory => 'Password principale in memoria';

  @override
  String get securityMasterPasswordMemoryValue =>
      'No: mentre e sbloccata resta solo la chiave derivata, azzerata al blocco.';

  @override
  String get securityRenderingEngine => 'Motore di rendering';

  @override
  String get securityRetrySave => 'Riprova salvataggio';

  @override
  String get securityEncryption => 'Cifratura';

  @override
  String get securityEncryptionSubtitle => 'Primitive usate da questa build.';

  @override
  String get securityCipher => 'Cifrario';

  @override
  String get securityCipherValue =>
      'AES-256-GCM (tag di autenticazione a 128 bit)';

  @override
  String get securityKeyDerivation => 'Derivazione della chiave';

  @override
  String get securityVaultFormat => 'Formato cassaforte';

  @override
  String get securityFormatUnknown => 'Sconosciuto';

  @override
  String get securityFormatLegacySuffix => ' (legacy)';

  @override
  String get securityMemoryHard => 'Memory-hard';

  @override
  String get securityMemoryHardNo => 'No: questo file usa ancora PBKDF2';

  @override
  String get securityMemoryHardYes =>
      'Si: Argon2id resiste ad attacchi con GPU e ASIC';

  @override
  String get securityHeaderAuth => 'Autenticazione dell\'intestazione';

  @override
  String get securityHeaderAuthValue =>
      'L\'intera intestazione (versione, KDF, parametri, salt, nonce) e autenticata come dato associato GCM.';

  @override
  String get securityNonce => 'Nonce';

  @override
  String get securityNonceValue =>
      'Nuovo nonce casuale a 96 bit a ogni salvataggio';

  @override
  String get securityLegacyFormatTitle => 'Rilevato formato cassaforte legacy';

  @override
  String get securityOldKdfTitle =>
      'Rilevata derivazione della chiave obsoleta';

  @override
  String securityOldKdfMessage(String current, String target) {
    return 'Questo file e stato scritto prima di Argon2id ($current). Al prossimo sblocco riuscito verra ricifrato con $target.';
  }

  @override
  String get securityArgonNote =>
      'Argon2id e memory-hard: l\'hardware di cracking non puo scambiare memoria per parallelismo come fa con PBKDF2. Resta comunque la password principale, lunga e unica, a proteggere la cassaforte.';

  @override
  String get securityHealthTitle => 'Salute delle password';

  @override
  String get securityHealthSubtitle =>
      'Riuso e robustezza di tutte le password salvate.';

  @override
  String securityHealthScore(int score) {
    return '$score / 100';
  }

  @override
  String get securityHealthNoPasswords => 'Ancora nessuna password';

  @override
  String get securityHealthWithPassword => 'Con password';

  @override
  String get securityHealthReused => 'Riutilizzate';

  @override
  String get securityHealthWeak => 'Deboli';

  @override
  String get securityHealthNoPassword => 'Senza password';

  @override
  String get securityHealthEmptyMessage =>
      'Ancora nulla da analizzare: aggiungi una credenziale con password.';

  @override
  String get securityHealthCleanMessage =>
      'Ogni password e unica e nessuna sembra debole.';

  @override
  String get securityHealthReusedTitle => 'PASSWORD RIUTILIZZATE';

  @override
  String securityHealthSharedBy(int count) {
    return 'Condivisa da $count voci';
  }

  @override
  String securityHealthMoreGroups(int count) {
    return '+ altri $count gruppi';
  }

  @override
  String get securityHealthReuseNote =>
      'Un solo servizio compromesso espone tutte le voci che condividono quella password. Dai a ciascuna una password generata dedicata.';

  @override
  String get securityHealthWeakTitle => 'PASSWORD DEBOLI';

  @override
  String securityHealthMore(int count) {
    return '+ altre $count';
  }

  @override
  String get securityVaultFile => 'File della cassaforte';

  @override
  String get securityVaultFileSubtitle =>
      'Tutto risiede in un unico file cifrato.';

  @override
  String get securityRefresh => 'Aggiorna';

  @override
  String get securityLocation => 'Percorso';

  @override
  String get securityResolving => 'Risoluzione in corso...';

  @override
  String get securityCopyPath => 'Copia percorso';

  @override
  String get securityOpenFolder => 'Apri la cartella';

  @override
  String get securitySize => 'Dimensione';

  @override
  String get securityModified => 'Modificato';

  @override
  String get securityBackupCopy => 'Copia di backup';

  @override
  String get securityBackupPresent =>
      'credentials.plf.bak conservato accanto alla cassaforte';

  @override
  String get securityBackupOnNextSave =>
      'Creata automaticamente al prossimo salvataggio';

  @override
  String get securityWrites => 'Scritture';

  @override
  String get securityWritesValue =>
      'Atomiche: scrittura su file temporaneo e poi rinomina sulla cassaforte, cosi un salvataggio interrotto non puo corromperla.';

  @override
  String get securityFolderOpenFailed => 'Impossibile aprire la cartella';

  @override
  String get securityVaultPathLabel => 'Percorso cassaforte';

  @override
  String get securityProtection => 'Protezione';

  @override
  String get securityProtectionSubtitle =>
      'Comportamento della sessione, configurabile nelle Impostazioni.';

  @override
  String get securityMinimizeLabel => 'Riduci a icona / Alt-Tab';

  @override
  String get securityMinimizeValue =>
      'Non blocca mai la cassaforte: copia un segreto, cambia app e ritrova la cassaforte aperta.';

  @override
  String securityClipboardCleared(int seconds) {
    return 'Cancellati dopo ${seconds}s e solo se il valore copiato e ancora li';
  }

  @override
  String get securityClipboardNever => 'Mai cancellati automaticamente';

  @override
  String get securitySecretsOnScreen => 'Segreti a schermo';

  @override
  String get securitySecretsRevealed =>
      'Mostrati per impostazione predefinita nell\'editor';

  @override
  String get securitySecretsHidden => 'Nascosti finche non li mostri';

  @override
  String get securityNetwork => 'Rete';

  @override
  String get securityNetworkValue =>
      'Nessuna. Nessuna telemetria, sincronizzazione, ricerca di favicon o controllo aggiornamenti.';

  @override
  String get securityChangeMasterPassword => 'Cambia password principale';

  @override
  String get changePasswordSubtitle =>
      'La cassaforte viene ricifrata immediatamente.';

  @override
  String get changePasswordCurrent => 'Password principale attuale';

  @override
  String get changePasswordNew => 'Nuova password principale';

  @override
  String get changePasswordConfirm => 'Conferma nuova password';

  @override
  String changePasswordTooShort(int count) {
    return 'La nuova password deve avere almeno $count caratteri.';
  }

  @override
  String get changePasswordMismatch => 'Le nuove password non coincidono.';

  @override
  String get changePasswordBackupNote =>
      'Ricorda i backup: le copie del vecchio file della cassaforte richiedono ancora la vecchia password.';

  @override
  String get changePasswordAction => 'Cambia password';

  @override
  String get changePasswordDone => 'Password principale cambiata';

  @override
  String get changePasswordDoneDetail =>
      'La cassaforte e stata ricifrata con la nuova chiave.';

  @override
  String get settingsTitle => 'Impostazioni';

  @override
  String get settingsSubtitle =>
      'Aspetto, comportamento di sicurezza, tastiera e archiviazione.';

  @override
  String get settingsAppearance => 'Aspetto';

  @override
  String get settingsTheme => 'Tema';

  @override
  String get settingsThemeDescription =>
      'Tutte le varianti sono scure: scegli quella che preferisci.';

  @override
  String get settingsDensity => 'Densita';

  @override
  String get settingsDensityDescription =>
      'Compatta mostra piu righe nelle finestre piccole.';

  @override
  String get settingsAnimations => 'Animazioni';

  @override
  String get settingsAnimationsDescription =>
      'Disattiva del tutto transizioni e micro-interazioni.';

  @override
  String get settingsLanguage => 'Lingua';

  @override
  String get settingsLanguageDescription =>
      'Lingua dell\'interfaccia. \"Sistema\" segue Windows.';

  @override
  String get settingsLanguageSystem => 'Sistema';

  @override
  String get settingsLanguageEnglish => 'Inglese';

  @override
  String get settingsLanguageItalian => 'Italiano';

  @override
  String get themeMidnight => 'Mezzanotte';

  @override
  String get themeMidnightDescription =>
      'Base indaco profondo con accento viola';

  @override
  String get themeObsidian => 'Ossidiana';

  @override
  String get themeObsidianDescription => 'Grafite neutro con accento ciano';

  @override
  String get themeViolet => 'Viola';

  @override
  String get themeVioletDescription =>
      'Viola saturo, accenti a contrasto piu alto';

  @override
  String get densityComfortable => 'Comoda';

  @override
  String get densityCompact => 'Compatta';

  @override
  String get settingsSecurity => 'Sicurezza';

  @override
  String get settingsAutoLock => 'Blocco automatico dopo inattivita';

  @override
  String get settingsAutoLockDescription =>
      'Ridurre a icona, Alt-Tab o perdere il focus non bloccano mai la cassaforte: lo fanno solo questo timer e il blocco manuale.';

  @override
  String get settingsNever => 'Mai';

  @override
  String settingsMinutesShort(int minutes) {
    return '${minutes}m';
  }

  @override
  String settingsSecondsShort(int seconds) {
    return '${seconds}s';
  }

  @override
  String get settingsClipboard => 'Cancella gli appunti dopo la copia';

  @override
  String get settingsClipboardDescription =>
      'Cancella solo se negli appunti c\'e ancora il segreto copiato.';

  @override
  String get settingsRevealSecrets =>
      'Mostra i segreti per impostazione predefinita';

  @override
  String get settingsRevealSecretsDescription =>
      'Se disattivo, password e campi segreti partono nascosti.';

  @override
  String get settingsConfirmDelete => 'Conferma prima di eliminare';

  @override
  String get settingsConfirmDeleteDescription =>
      'L\'eliminazione offre sempre un annulla; questo aggiunge una finestra di conferma.';

  @override
  String get settingsKeepHistory => 'Conserva la cronologia password';

  @override
  String settingsKeepHistoryDescription(int max) {
    return 'Le password sostituite restano sulla voce (fino a $max) per poter annullare una modifica sbagliata. Sono salvate cifrate dentro la cassaforte.';
  }

  @override
  String get settingsStoredHistory => 'Cronologia password memorizzata';

  @override
  String get settingsStoredHistoryEmpty =>
      'Al momento non e memorizzata alcuna password sostituita.';

  @override
  String settingsStoredHistoryCount(int count, int entries) {
    return '$count password sostituite su $entries voci.';
  }

  @override
  String get settingsClearAll => 'Cancella tutto';

  @override
  String get settingsClearAllTitle =>
      'Cancellare tutta la cronologia password?';

  @override
  String get settingsClearAllMessage =>
      'Tutte le password sostituite nella cassaforte vengono eliminate. Le password attuali restano intatte. Operazione non reversibile.';

  @override
  String settingsClearAllDetail(int count) {
    return '$count password memorizzate';
  }

  @override
  String get settingsClearAllConfirm => 'Cancella tutto';

  @override
  String settingsClearAllDone(int count) {
    return '$count rimosse';
  }

  @override
  String get settingsMasterPassword => 'Password principale';

  @override
  String get settingsMasterPasswordDescription =>
      'Ricifra la cassaforte con una nuova chiave.';

  @override
  String get settingsChange => 'Cambia';

  @override
  String get settingsKeyboard => 'Tastiera';

  @override
  String get settingsGlobalShortcut => 'Scorciatoia globale (Ctrl+Alt+P)';

  @override
  String get settingsGlobalShortcutDescription =>
      'Porta BetterOnlyYours in primo piano da qualsiasi applicazione e apre il riquadro comandi.';

  @override
  String get settingsShortcuts => 'Scorciatoie';

  @override
  String get settingsShortcutsDescription => 'Tutto raggiungibile senza mouse.';

  @override
  String get settingsVaultWindow => 'Cassaforte e finestra';

  @override
  String get settingsVaultFile => 'File della cassaforte';

  @override
  String get settingsVaultFileResolving =>
      'Individuazione del percorso della cassaforte...';

  @override
  String get settingsRememberWindow =>
      'Ricorda dimensione e posizione della finestra';

  @override
  String get settingsRememberWindowDescription =>
      'Al prossimo avvio ripristina la finestra dove l\'hai lasciata.';

  @override
  String get settingsAbout => 'Informazioni';

  @override
  String get settingsVersion => 'Versione';

  @override
  String get settingsStorage => 'Archiviazione';

  @override
  String get settingsStorageValue =>
      'Un unico file cifrato, scritto in modo atomico.';

  @override
  String get settingsNetworkAccess => 'Accesso di rete';

  @override
  String get settingsNetworkAccessValue =>
      'Nessuno: l\'app non si connette mai a niente.';

  @override
  String actionCopied(String label) {
    return '$label copiato';
  }

  @override
  String actionCopiedDetail(int seconds) {
    return 'Gli appunti si cancellano tra ${seconds}s se non copi altro.';
  }

  @override
  String get actionClipboardUnavailable => 'Appunti non disponibili';

  @override
  String get actionClipboardUnavailableDetail =>
      'Windows ha rifiutato la copia: forse un\'altra app la sta bloccando.';

  @override
  String get labelUsername => 'Nome utente';

  @override
  String get labelPassword => 'Password';

  @override
  String get actionNoUsername => 'Nessun nome utente su questa voce';

  @override
  String get actionNoUsernameDetail => 'Aggiungilo dai dettagli della voce.';

  @override
  String get actionNoPassword => 'Nessuna password su questa voce';

  @override
  String get actionNoPasswordLegacy =>
      'Questa voce contiene ancora solo note libere.';

  @override
  String get actionNoPasswordDetail =>
      'Aggiungila o generala dai dettagli della voce.';

  @override
  String get actionNoWebsite => 'Nessun sito su questa voce';

  @override
  String get actionBadUrl => 'Impossibile aprire questo indirizzo';

  @override
  String get actionBadUrlDetail =>
      'Vengono aperti solo collegamenti http e https.';

  @override
  String get actionNoBrowser => 'Nessun browser ha risposto';

  @override
  String get actionLinkFailed => 'Impossibile aprire il collegamento';

  @override
  String get actionFavoriteAdded => 'Aggiunta ai preferiti';

  @override
  String get actionFavoriteRemoved => 'Rimossa dai preferiti';

  @override
  String get actionDuplicated => 'Voce duplicata';

  @override
  String get actionDeleteTitle => 'Eliminare questa voce?';

  @override
  String get actionDeleteMessage =>
      'Viene rimossa dalla cassaforte cifrata. Puoi annullare subito dopo dalla notifica.';

  @override
  String get actionDeleted => 'Voce eliminata';

  @override
  String get menuOpen => 'Apri';

  @override
  String get menuCopyUsername => 'Copia nome utente';

  @override
  String get menuCopyPassword => 'Copia password';

  @override
  String get menuOpenWebsite => 'Apri il sito';

  @override
  String get menuAddFavorite => 'Aggiungi ai preferiti';

  @override
  String get menuRemoveFavorite => 'Rimuovi dai preferiti';

  @override
  String get menuDuplicate => 'Duplica';

  @override
  String get menuDelete => 'Elimina';

  @override
  String get vaultLocked => 'Cassaforte bloccata';

  @override
  String vaultLockedInactivity(int minutes) {
    return 'Bloccata dopo $minutes minuti di inattivita.';
  }

  @override
  String get vaultRestoredBackup => 'Cassaforte ripristinata dal backup';

  @override
  String get vaultRestoredBackupDetail =>
      'Il file principale non era leggibile, quindi e stata aperta la copia .bak.';

  @override
  String get vaultUpgraded => 'Cassaforte aggiornata';

  @override
  String vaultUpgradedDetail(String kdf) {
    return 'Ricifrata con una derivazione della chiave piu robusta ($kdf).';
  }

  @override
  String vaultSaveRetryHint(String hint) {
    return '$hint Le tue modifiche sono ancora qui: riprova a salvare.';
  }

  @override
  String get vaultSaveFailed => 'Impossibile salvare la cassaforte';

  @override
  String get vaultSaveFailedDetail =>
      'Le tue modifiche sono ancora qui: riprova a salvare.';

  @override
  String get errorNotFoundTitle => 'File della cassaforte non trovato';

  @override
  String get errorNotFoundHint =>
      'Ripristina il file della cassaforte o creane una nuova per continuare.';

  @override
  String get errorInvalidPasswordTitle => 'Password principale non accettata';

  @override
  String get errorInvalidPasswordHint =>
      'Controlla Bloc Maiusc e il layout della tastiera. Se la password e corretta, il file potrebbe essere stato modificato su disco.';

  @override
  String get errorBadSignatureTitle =>
      'Questo file non e una cassaforte BetterOnlyYours';

  @override
  String get errorBadSignatureHint =>
      'L\'intestazione del file non corrisponde al formato. Ripristina una copia di backup.';

  @override
  String get errorUnsupportedVersionTitle =>
      'Versione della cassaforte non supportata';

  @override
  String get errorUnsupportedVersionHint =>
      'La cassaforte e stata scritta da una versione piu recente di BetterOnlyYours. Aggiorna l\'app per aprirla.';

  @override
  String get errorTruncatedTitle => 'File della cassaforte incompleto';

  @override
  String get errorTruncatedHint =>
      'Il file e piu corto di una cassaforte valida. Ripristina il backup .bak accanto ad esso.';

  @override
  String get errorMalformedTitle =>
      'Impossibile leggere il contenuto della cassaforte';

  @override
  String get errorMalformedHint =>
      'La decifratura e riuscita ma il contenuto non e valido.';

  @override
  String get errorPermissionTitle => 'Accesso al file della cassaforte negato';

  @override
  String get errorPermissionHint =>
      'Chiudi le altre app che usano il file o esegui da una cartella in cui puoi scrivere.';

  @override
  String get errorIoTitle => 'Impossibile leggere o scrivere la cassaforte';

  @override
  String get errorIoHint =>
      'Verifica che la cartella esista e abbia spazio libero, poi riprova.';

  @override
  String get strengthEmpty => '—';

  @override
  String get strengthVeryWeak => 'Molto debole';

  @override
  String get strengthWeak => 'Debole';

  @override
  String get strengthFair => 'Discreta';

  @override
  String get strengthStrong => 'Robusta';

  @override
  String get strengthExcellent => 'Eccellente';

  @override
  String strengthBits(int bits) {
    return '~ $bits bit';
  }

  @override
  String get strengthEstimateNote =>
      'Solo una stima: si basa su lunghezza, varieta dei caratteri e schemi evidenti.';

  @override
  String get inspectorTitle => 'ISPETTORE';

  @override
  String get inspectorPasswordStrength => 'Robustezza password';

  @override
  String get inspectorReused =>
      'Questa password e usata anche da un\'altra voce.';

  @override
  String get inspectorQuickActions => 'Azioni rapide';

  @override
  String get inspectorDuplicate => 'Duplica voce';

  @override
  String get inspectorDelete => 'Elimina voce';

  @override
  String get inspectorTimeline => 'Cronologia';

  @override
  String get inspectorCreated => 'Creata';

  @override
  String get inspectorModified => 'Modificata';

  @override
  String get inspectorOpened => 'Aperta';

  @override
  String get inspectorFormat => 'Formato';

  @override
  String get inspectorFormatLegacy => 'Testo legacy';

  @override
  String get inspectorFormatStructured => 'Strutturato';

  @override
  String get inspectorHistory => 'Cronologia';

  @override
  String get inspectorNoHistory => 'Nessuna password precedente';

  @override
  String get inspectorUnknown => 'Sconosciuta';

  @override
  String get inspectorTags => 'Etichette';

  @override
  String get inspectorRelated => 'Correlate';

  @override
  String get vaultPageFavoritesTitle => 'Preferiti';

  @override
  String get vaultPageFavoritesMessage =>
      'Scegli una voce a sinistra o metti la stella a quelle che usi di piu per trovarle qui.';

  @override
  String get vaultPageRecentTitle => 'Aperte di recente';

  @override
  String get vaultPageRecentMessage =>
      'Le ultime voci aperte sono elencate a sinistra. Selezionane una per vederla qui.';

  @override
  String get vaultPageRecentHint =>
      'La cronologia e salvata dentro la cassaforte cifrata, mai in chiaro su disco.';

  @override
  String get shortcutPalette => 'Riquadro comandi';

  @override
  String get shortcutPaletteGlobal =>
      'Riquadro comandi da qualunque punto di Windows';

  @override
  String get shortcutNewEntry => 'Nuova voce';

  @override
  String get shortcutFocusSearch =>
      'Attiva il campo di ricerca della cassaforte';

  @override
  String get shortcutSave => 'Salva la voce in modifica';

  @override
  String get shortcutFavorite =>
      'Attiva o disattiva il preferito sulla voce selezionata';

  @override
  String get shortcutCopyPassword => 'Copia la password selezionata';

  @override
  String get shortcutCopyUsername => 'Copia il nome utente selezionato';

  @override
  String get shortcutGenerator => 'Generatore di password';

  @override
  String get shortcutSettings => 'Impostazioni';

  @override
  String get shortcutLock => 'Blocca la cassaforte';

  @override
  String get shortcutDelete => 'Elimina la voce selezionata';

  @override
  String get shortcutEscape => 'Chiude riquadro, finestra o ricerca';

  @override
  String get shortcutNavigate => 'Scorre i risultati e apre';

  @override
  String get settingsGlobalShortcutFailed =>
      'Impossibile registrare Ctrl+Alt+P: probabilmente e usata da un\'altra applicazione.';

  @override
  String get vaultLockedManual => 'Cassaforte bloccata';
}
