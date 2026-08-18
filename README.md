<div align="center">

# BetterOnlyYours

**A local, encrypted vault that stays yours.**

Credentials, notes and secrets in a single encrypted file on your own machine.
No account, no sync, no telemetry.

[![Flutter](https://img.shields.io/badge/Flutter-3.47-02569B?style=flat-square&logo=flutter&logoColor=white)](https://flutter.dev)
[![Windows](https://img.shields.io/badge/Windows-desktop-0078D6?style=flat-square&logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![Crypto](https://img.shields.io/badge/AES--256--GCM-PBKDF2--HMAC--SHA256-6E56CF?style=flat-square)]()

</div>

---

## What it is

A desktop vault for Windows. Entries live in one file, `credentials.plf`,
encrypted with a key derived from your master password. The app never talks to
the network — not for sync, not for updates, not for favicons.

- **Vault dashboard** — counts, recent activity, weak-password review, quick actions.
- **Structured entries** — title, username, password, URL, notes, tags, favorites, custom fields.
- **Command palette** — fuzzy search over entries and commands, from inside the app (`Ctrl+K`) or from anywhere in Windows (`Ctrl+Alt+P`).
- **Password generator** — random characters or passphrase, with strength estimation.
- **Security center** — the real encryption parameters of your vault file, not marketing copy.
- **Keyboard-first** — every core action has a shortcut; the mouse is optional.
- **Responsive desktop shell** — one, two or three panes depending on window width.

Minimizing, Alt-Tabbing or losing focus **never** locks the vault. Locking is
explicit (`Ctrl+L`) or, if you enable it, after a configurable idle timeout.

---

## Security, precisely

| Property | Implementation |
|---|---|
| Cipher | AES-256-GCM, 128-bit authentication tag (PointyCastle) |
| Key derivation | PBKDF2-HMAC-SHA256, **200,000** iterations, 16-byte random salt |
| Nonce | Fresh 96-bit random nonce per save |
| Authenticated header | Version, KDF id, iteration count, salt and nonce are GCM associated data |
| Writes | Temp file → `fsync` → previous file copied to `.bak` → atomic rename |
| Key lifetime | Only the derived key is held while unlocked; the master password is wiped after unlocking |
| Metadata | Usage history lives inside the encrypted payload, never in a plaintext file |
| Network | None |

What this does **not** claim: PBKDF2 is not memory-hard, so a long, unique
master password remains the real protection. The password strength meter is a
heuristic estimate, not a proof.

### Vault file format

```
v2 (current)
[4]  magic     FF FE 0D 0A
[1]  version   0x02
[1]  kdf id    0x01 (PBKDF2-HMAC-SHA256)
[4]  iterations (big endian)
[16] salt
[12] nonce
[..] AES-256-GCM ciphertext + tag

v1 (legacy, still readable)
[4] magic | [1] 0x01 | [16] salt | [12] nonce | ciphertext + tag
PBKDF2-HMAC-SHA256, fixed 3,000 iterations
```

A v1 vault is read with the old parameters, then transparently re-encrypted as
v2 with the current iteration count on first unlock.

### Entry format and compatibility

Early builds stored `name -> free text`. That still works:

- A value that is plain text is loaded as an entry with those **notes**, and is
  written back byte-identical as long as it stays notes-only.
- Structured entries are stored as `BOY1:<json>` and carry username, password,
  URL, notes, tags, favorite, timestamps and custom fields.
- Fields written by a newer version are preserved on round-trip instead of
  being dropped.
- Vault-level metadata (recently opened) lives under a reserved key inside the
  encrypted payload and is hidden from the entry list.

---

## Where your data lives

Resolution order (first match wins):

1. `BETTERONLYYOURS_VAULT` environment variable (absolute path — useful for portable installs)
2. `credentials.plf` in the working directory (how early builds stored it)
3. `credentials.plf` next to the executable
4. `%APPDATA%\BetterOnlyYours\BetterOnlyYours\credentials.plf`

Preferences (theme, timeouts, window bounds — never secrets) sit in
`settings.json` in the per-user data folder. The Security center shows the
exact path in use and can open the containing folder.

**Back up `credentials.plf`.** There is no recovery: forget the master password
and the data stays encrypted forever.

---

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl + K` | Command palette |
| `Ctrl + Alt + P` | Command palette from anywhere in Windows |
| `Ctrl + N` | New entry |
| `Ctrl + F` | Focus the vault search field |
| `Ctrl + S` | Save the entry being edited |
| `Ctrl + D` | Toggle favorite |
| `Ctrl + Shift + C` | Copy the selected password |
| `Ctrl + Shift + U` | Copy the selected username |
| `Ctrl + G` | Password generator |
| `Ctrl + ,` | Settings |
| `Ctrl + L` | Lock vault |
| `Delete` | Delete the selected entry (with confirmation and undo) |
| `Esc` | Close palette, dialog or search |
| `↑ / ↓ / Enter` | Move through results and open |

In the palette, `Ctrl+Enter` copies the password and `Alt+Enter` the username
of the highlighted entry.

---

## Build

```bash
flutter pub get
flutter run -d windows
flutter build windows --release
```

Output: `build/windows/x64/runner/Release/betteronlyyours.exe`

Checks:

```bash
flutter analyze
flutter test
```

---

## Project structure

```
lib/
  app/            MaterialApp, design tokens and themes, shortcuts, window and hotkey services
  core/
    models/       VaultEntry, VaultMeta, AppSettings, GeneratorOptions
    security/     crypto primitives, file codec, repository, session, typed errors
    services/     clipboard, generator, strength, fuzzy search, settings, word list
    storage/      vault and settings path resolution
    utils/        formatting helpers
  features/
    auth/         lock screen, vault creation
    shell/        title bar, navigation rail, status bar, app shell
    vault/        list, detail editor, inspector, dashboard, shared entry actions
    search/       command palette
    generator/    generator panel and page
    security/     security center, change master password
    settings/     settings page
  shared/         buttons, fields, dialogs, toasts, chips, meters, animations
state/            vault, settings, shell and toast controllers (Provider)
```

Tests cover the vault format and legacy migration, entry serialization,
generator, strength estimation, fuzzy search, settings, controller behaviour,
auth flows, the entry editor, and shell layout at window sizes from 720×520 to
2560×1440.

---

## Privacy

No telemetry, no crash reporting, no remote password services, no icon lookups.
Everything the app does happens on your machine, against your file.

---

<div align="center">

**BetterOnlyYours** — your secrets, your disk, your key.

</div>
