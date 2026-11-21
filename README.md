<div align="center">

<img src="src/OnlyYours.App/Assets/Splash.png" width="150" alt="OnlyYours" />

# OnlyYours

**Your secrets. Sealed.**

A local password manager for Windows, written from scratch in C# / .NET 11 / WPF.
No account, no cloud, no telemetry: extreme-grade cryptography and a key that only you know.

[![.NET](https://img.shields.io/badge/.NET-11-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![WPF](https://img.shields.io/badge/WPF-Windows%2010%2F11-0078D6?style=flat-square&logo=windows&logoColor=white)](https://learn.microsoft.com/dotnet/desktop/wpf/)
[![Crypto](https://img.shields.io/badge/Argon2id%20·%20ChaCha20--Poly1305%20%2B%20AES--256--GCM-B8862F?style=flat-square)](docs/SECURITY.md)
[![PQC](https://img.shields.io/badge/sharing-ML--KEM--1024%20%2B%20ML--DSA--87-2D896C?style=flat-square)](docs/SECURITY.md#sealed-sharing)

</div>

---

OnlyYours is the successor of **BetterOnlyYours**: the same philosophy (one encrypted file, on your
own disk, opened only by your master password) with a new identity, a far stronger file format,
memory protections designed to keep passwords sealed even in RAM, and a cinematic interface. Your
old `credentials.plf` imports in one click.

## Contents

- [Features](#features)
- [Security at a glance](#security-at-a-glance)
- [Migrating from BetterOnlyYours](#migrating-from-betteronlyyours)
- [Performance](#performance)
- [Keyboard shortcuts](#keyboard-shortcuts)
- [Build, run, publish](#build-run-publish)
- [Tests](#tests)
- [Where the data lives](#where-the-data-lives)
- [Repository layout](#repository-layout)

## Features

**The vault**
- Eight entry types: login, secure note, payment card, identity, Wi‑Fi network, SSH key, API key,
  bank account — each with its own fields, plus reorderable custom fields (text, secret, link,
  number, date).
- Built-in **2FA (TOTP)** codes: `otpauth://` links or base32 keys, SHA‑1/256/512, 4–10 digits, any
  period, Steam Guard. The secret is never displayed again once it is set up.
- **Password history** (up to 30 per entry) with restore; favorites, tags, expiry reminders, a
  trash with configurable retention, duplication, sort orders.
- Instant fuzzy search with highlighting, filters by type, tag and risk category.

**Active security**
- **Security center**: an overall score plus weak, reused, old, expired, 2FA‑less and empty
  passwords — one click shows the entries involved. It also shows the real encryption
  parameters, the state of the process protections, backups and the event log.
- **Generator**: characters (with exclusions and without look-alikes), passphrases (English
  words, or Italian on request), non-trivial PINs; live entropy and brute-force time.
- **Master password change** with optional rotation of the vault key (everything is
  re-encrypted), a regenerable **recovery key**, **Windows Hello** (a TPM-bound key) and
  strengthening of the Argon2id derivation.
- **Auto-lock** on inactivity, with Windows (Win+L), before sleep and (optionally) on minimize.

**Convenience**
- **Command palette** (`Ctrl+K`) and **global quick search** from any app (`Ctrl+Alt+P`): find an
  entry and copy its password, username or code without opening the window.
- **Auto-type** into the foreground window (`Ctrl+Alt+A`) with custom sequences
  (`{USERNAME}{TAB}{PASSWORD}{ENTER}`, `{TOTP}`, `{DELAY=200}`…).
- **CSV import** that recognizes Chrome/Edge/Brave, Firefox, Bitwarden, 1Password,
  KeePass/KeePassXC, LastPass, Proton Pass, Dashlane and generic files; export as an encrypted
  package, a vault copy or (with explicit warnings) plain CSV.
- **Sealed sharing** with hybrid post-quantum cryptography and sender signatures.
- Notification area, start with Windows, single instance, Windows 11 Snap Layouts.

**Look and feel**
- Two themes, **Obsidian** (dark) and **Ivory** (light), or follow Windows; four accent metals
  (gold, copper, platinum, jade). WCAG AA contrast is verified by automated tests.
- A **keyframed cinematic opening**: letterbox, spark, a ring engraved by light, runes, the
  medallion, an impact with a golden bloom and a shockwave, a camera dolly, the wordmark
  converging and gilding, and a *match cut* that lands the seal exactly on the seal of the
  screen behind it. Skippable; short and off variants.
- Scene transitions (the seal splits open on unlock, a blur seals it on lock), spring
  animations, particles, 2FA codes that flip like a split-flap board. Adjustable motion level
  (cinematic, reduced, off).

## Security at a glance

| Layer | What OnlyYours does |
| --- | --- |
| Key derivation | **Argon2id** (RFC 9106, own AVX2-vectorized implementation): 256 MiB · 3 passes · 4 lanes by default; 128 MiB / 1 GiB presets |
| File encryption | **Cascaded AEADs**: ChaCha20‑Poly1305 inside, AES‑256‑GCM outside, independent keys from HKDF‑SHA‑512 |
| Integrity | Header authenticated as associated data + **HMAC‑SHA‑512 key commitment** over the whole file |
| Metadata | Size hidden by **PADMÉ** padding; anti-rollback write counter |
| Keys | A random vault master key (VMK) wrapped by independent slots: master password, 240-bit recovery key, Windows Hello |
| Sensitive fields | Every password, note, card number or 2FA secret is **sealed on its own** (AES‑256‑GCM bound to its entry and field) and **stays encrypted in RAM** while the vault is open |
| Memory | Buffers locked in RAM (kept out of the page file), keys encrypted by the kernel while idle, zeroed on every release, a string-free password editor |
| Process | DLLs only from trusted folders, no legacy extension points, memory access denied to other processes, window excluded from screenshots and screen sharing |
| Clipboard | Kept out of Win+V history and cloud sync, cleared automatically |

The full write-up — threat model, file format, key hierarchy, sharing protocol and stated
limits — is in **[docs/SECURITY.md](docs/SECURITY.md)**.

## Migrating from BetterOnlyYours

On first start OnlyYours looks for `credentials.plf` (`%APPDATA%\BetterOnlyYours\BetterOnlyYours`,
the working folder, the `BETTERONLYYOURS_VAULT` variable) and offers to import it.

- **Every historical format** is supported: v1 (PBKDF2, 3000 iterations), v2 (PBKDF2 with the
  iteration count in the file), v3 with PBKDF2 or Argon2id; if the main file is damaged, its
  `.bak` copy is used.
- It brings over every entry, username, password, URL, note, tag, favorite, date, custom field
  (secret ones too), the **password history**, last use and the **2FA codes** (the `BOYTOTP1`
  boxes are opened with the content key and resealed). Unknown keys from future versions become
  fields instead of being lost.
- Preferences are imported (auto-lock, clipboard, generator…).
- The old file is **only ever read**: never modified or deleted.
- No plaintext strings: the decrypted JSON stays in locked memory and every secret goes straight
  from there into a sealed field.

Correctness is verified against files **generated by BetterOnlyYours' own code**
(`tools/legacy-fixtures`, run with Flutter): every format, KDF, 2FA box and edge case.

## Performance

Measured on a Release ReadyToRun build (Windows 11, x64, AVX2) with a 200-entry vault:

| Operation | Time |
| --- | --- |
| Seal on screen (native splash) | **~110 ms** after launch |
| First frame of the window (lock screen) | **~730–760 ms** (an empty WPF window takes ~550–650 ms on the same machine) |
| Argon2id 256 MiB · t=3 · p=4 (unlock) | **~240 ms** (128 MiB: ~125 ms · 1 GiB: ~830 ms) |
| Opening and decrypting the vault (200 entries, Argon2id included) | **~250 ms** |
| Exit (pending save, secrets wiped, process gone) | **~20–30 ms**; the window disappears at once |

How: ReadyToRun, settings read before WPF loads, the opening sequence built only when it plays,
deferred initialization (tray, hotkeys, auto-type) once the UI is idle, writes on a dedicated
thread, a fast exit that skips WPF teardown. Start-up traces: `ONLYYOURS_TRACE_STARTUP=1`.

## Keyboard shortcuts

| Keys | Action |
| --- | --- |
| `Ctrl+K` | Command palette |
| `Ctrl+Alt+P` | Quick search from any app |
| `Ctrl+Alt+A` | Auto-type into the active window |
| `Ctrl+N` | New login |
| `Ctrl+F` | Search |
| `Ctrl+E` / `F2` | Edit the selected entry |
| `Ctrl+S` / `Esc` | Save / cancel the edit |
| `Ctrl+Shift+C` · `Ctrl+Shift+U` · `Ctrl+Shift+T` | Copy password · username · 2FA code |
| `Ctrl+Shift+A` | Auto-type the selected entry |
| `Ctrl+Shift+O` | Open the website |
| `Ctrl+D` | Toggle favorite |
| `Del` | Move to trash |
| `Ctrl+1` … `Ctrl+7` | Vault, Generator, Security, Sharing, Trash, Import/Export, Settings |
| `Ctrl+G` · `Ctrl+,` | Generator · Settings |
| `Ctrl+L` | Lock |

## Build, run, publish

Requirements: Windows 10 (19041) or later and the **.NET 11 SDK**. Post-quantum sharing needs an
up-to-date Windows 11 (ML‑KEM / ML‑DSA in the system cryptography provider).

```powershell
dotnet build OnlyYours.slnx
dotnet run --project src/OnlyYours.App
dotnet publish src/OnlyYours.App -c Release -o publish
```

The Release build is precompiled (ReadyToRun) for `win-x64` and depends on the .NET 11 Desktop
runtime. An `onlyyours.portable` file next to the executable turns on portable mode (data in
`.\data`).

## Tests

```powershell
dotnet test OnlyYours.slnx
```

- **`tests/OnlyYours.Core.Tests`** (161 tests): official Argon2d/i/id vectors (RFC 9106, both the
  scalar and the AVX2 path), BLAKE2b (RFC 7693), TOTP (RFC 6238, SHA‑1/256/512) and HOTP
  (RFC 4226); cross-vectors from PointyCastle; cascaded encryption and detection of every altered
  byte; vault life cycle, key slots, rotation, a simulated Windows Hello, backups and restore; the
  TLV format preserving future fields; generator (including statistical uniformity), password
  strength, search, CSV from every manager, vault health, settings; sharing with ML‑KEM/ML‑DSA
  and tampering; migration from every BetterOnlyYours format.
- **`tests/OnlyYours.App.Tests`** (43 tests): view models (creation, lock, unlock, editor, trash,
  generator, settings, palette, migration), completeness of the English text (singulars
  included) and a guard against leftovers of earlier names, WCAG contrast for every theme and
  accent, **rendering of every screen** in two themes, three accent metals and two window sizes
  (binding errors, exceptions, blank screens and system-black text all fail the test), the opening
  sequence frame by frame and in real time, and **end-to-end** tests that drive the real
  `OnlyYours.exe` through UI Automation (first run, creation, an entry, lock, a wrong password,
  unlock, exit, second instance) while measuring start-up and shutdown.

Rendered images land in `tests/OnlyYours.App.Tests/Snapshots/` (ignored by git). Tests always use
temporary folders (`ONLYYOURS_HOME`) and never touch the real profile.

The BetterOnlyYours fixtures are regenerated with:

```powershell
cd tools/legacy-fixtures
flutter pub get
flutter test test/generate_fixtures_test.dart
```

## Where the data lives

| File | Default location |
| --- | --- |
| Vault | `%APPDATA%\OnlyYours\vault.onlyyours` |
| Encrypted backups | `%APPDATA%\OnlyYours\backups\` (on the first save of each session and at most every 15 minutes; 20 kept) |
| Settings (never secrets) | `%APPDATA%\OnlyYours\settings.json` |

Environment variables: `ONLYYOURS_HOME` (data folder), `ONLYYOURS_VAULT` (vault file),
`BETTERONLYYOURS_VAULT` (old vault to import; `none` turns discovery off).

File types: `.onlyyours` (vault), `.oypkg` (sealed package), `.oyid` (public sharing identity).

## Repository layout

```
src/
  OnlyYours.Core/        domain logic, no WPF dependency
    Security/Crypto/     Argon2 (RFC 9106, AVX2), BLAKE2b, cascaded AEAD, PADMÉ, KDF parameters
    Security/Memory/     locked regions, kernel-protected buffers, char buffers, secret drafts
    Vault/               .onlyyours format, key slots, session, field cipher, backups
    Legacy/              reading credentials.plf (v1–v3) and string-free migration
    Otp/ Generators/ Analysis/ Search/ ImportExport/ Sharing/ Settings/ Platform/
  OnlyYours.App/         WPF application (MVVM without third-party libraries)
    Controls/            the seal, opening sequence, particles, transitions, secure field, …
    Views/ ViewModels/   screens and presentation logic
    Theme/ Localization/ Obsidian/Ivory themes, English text
    Services/ Interop/   protected clipboard, hotkeys, tray, auto-type, Windows Hello, hardening
tests/                   OnlyYours.Core.Tests, OnlyYours.App.Tests
tools/
  OnlyYours.IconGen/     renders the icon and the splash from the seal artwork
  legacy-fixtures/       writes genuine BetterOnlyYours vaults with the original code
betteronlyyours/         the previous Flutter project, kept as the reference for migration
docs/SECURITY.md         the complete security design
```

---

<div align="center">

**OnlyYours** — keeper of secrets.

</div>
