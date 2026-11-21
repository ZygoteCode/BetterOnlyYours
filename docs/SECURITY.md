# OnlyYours — security design

This document describes exactly how OnlyYours protects secrets: what it defends against, how the
file is built, how keys are derived and used, what happens in memory and where the limits are.
Every statement matches the code in `src/OnlyYours.Core` and `src/OnlyYours.App` and is covered
by the tests in `tests/`.

## Contents

1. [Threat model](#1-threat-model)
2. [Cryptographic primitives](#2-cryptographic-primitives)
3. [Key hierarchy](#3-key-hierarchy)
4. [The `.onlyyours` file](#4-the-onlyyours-file)
5. [Per-field encryption](#5-per-field-encryption)
6. [Writes, backups and anti-rollback](#6-writes-backups-and-anti-rollback)
7. [Memory protection](#7-memory-protection)
8. [Process and desktop protections](#8-process-and-desktop-protections)
9. [Locking and unlocking](#9-locking-and-unlocking)
10. [Sealed sharing](#sealed-sharing)
11. [Imports, exports and migration](#11-imports-exports-and-migration)
12. [Stated limits](#12-stated-limits)
13. [Verification](#13-verification)

---

## 1. Threat model

**OnlyYours protects against:**

| Threat | Defence |
| --- | --- |
| Theft or copy of the vault file (disk, backups, USB drive, personal cloud) | Heavy Argon2id + cascaded authenticated encryption + key commitment; the file does not even reveal the exact size of its contents |
| Tampering with the file (flipped bits, altered header, weakened KDF parameters, swapped slots) | The header is authenticated by both AEADs and by the HMAC‑SHA‑512; out-of-range parameters are rejected before any derivation |
| Restoring an older version of the file (rollback) | Authenticated write counter compared with the highest one already seen on this PC |
| Brute force against the master password | Argon2id 256 MiB · t=3 · p=4 (up to 1 GiB), exponential delay after 5 failures |
| Memory reads by other processes of the same user | A process DACL that denies reading/writing its memory; secrets encrypted even in RAM |
| Memory spilled to disk (page file, partial hibernation) | Buffers locked in RAM (`VirtualLock`), keys encrypted by the kernel while idle |
| "Light" malware: planted DLLs, global hooks, AppInit DLLs | Restricted DLL search, process mitigation policies |
| Screenshots, recordings, screen sharing | `WDA_EXCLUDEFROMCAPTURE` on the window |
| Clipboard history, cloud clipboard sync | Exclusion formats + automatic clearing |
| Prying eyes / an unattended PC | Locking on inactivity, with Windows, before sleep, on minimize |
| Interception or tampering of shared credentials | Hybrid post-quantum encryption to the recipient, dual sender signatures, verifiable fingerprint |

**Out of scope** (no local password manager can truly defend against these): an administrator or
malware with administrator/kernel privileges on the machine, a keylogger active while the master
password is typed, an attacker with physical access to the unlocked machine and the open vault,
a compromise of Windows itself.

---

## 2. Cryptographic primitives

| Use | Primitive | Implementation |
| --- | --- | --- |
| Master password derivation | **Argon2id** v1.3 (RFC 9106) | own (`Argon2.cs`), own BLAKE2b, AVX2 + scalar paths, lanes in parallel, memory in a zeroed native arena |
| Sub-key derivation | **HKDF‑SHA‑512** | .NET (`HKDF`) |
| Vault and package encryption | **ChaCha20‑Poly1305** inside **AES‑256‑GCM** | .NET / CNG |
| Key commitment and integrity | **HMAC‑SHA‑512** | .NET |
| Field and slot encryption | **AES‑256‑GCM** | .NET / CNG |
| Padding | **PADMÉ** | own (`Padme.cs`) |
| Key exchange | **ML‑KEM‑1024** (FIPS 203) + **ECDH P‑384** | Windows CNG |
| Signatures | **ML‑DSA‑87** (FIPS 204) + **ECDSA P‑384/SHA‑384** | Windows CNG |
| 2FA codes | HOTP/TOTP (RFC 4226/6238) | own, the secret never becomes a string |
| Randomness | `RandomNumberGenerator` (CNG `BCryptGenRandom`) | .NET |

The Argon2 and BLAKE2b implementations are checked against the official vectors (Argon2d,
Argon2i and Argon2id from RFC 9106 on both computation paths; BLAKE2b‑512 from RFC 7693) and
against cross-vectors produced by PointyCastle (the library BetterOnlyYours used).

**Why two cascaded AEADs.** ChaCha20‑Poly1305 and AES‑GCM are independent constructions (an ARX
stream cipher vs a block cipher with GHASH). With independent keys and nonces, a practical attack
would have to break both. The cost is negligible (a 200-entry vault weighs ~80 KiB).

**Why key commitment.** AES‑GCM and ChaCha20‑Poly1305 are not *key-committing*: in theory a
ciphertext can be valid under more than one key. The HMAC‑SHA‑512 over the whole file, keyed
from the same root, binds the file to a single key and closes the "partitioning oracle" class of
attacks.

---

## 3. Key hierarchy

```
                     ┌──────────────────────────────────────────────┐
 master password ───►│ Argon2id(m, t, p, salt 32 B, AD = vault id)  │──► KEK₁ ─┐
                     └──────────────────────────────────────────────┘          │ AES‑256‑GCM
 recovery key ──────► HKDF‑SHA‑512(salt, "OnlyYours/v1/slot/recovery"‖id) ─► KEK₂ ─┼──► VMK (32 random bytes)
 Windows Hello ─────► HKDF‑SHA‑512(TPM signature, "…/slot/hello"‖id‖challenge) ─► KEK₃ ┘
                                                                                     │
          ┌──────────────────────────────────────────────┬──────────────────────────┼─────────────────────┐
          ▼                                              ▼                          ▼                     ▼
 HKDF(VMK, per-save salt, "…/payload/chacha20-poly1305")  "…/payload/aes-256-gcm"  "…/payload/commitment"  HKDF(VMK, id, "…/field-key")
          │                                              │                          │                     │
     ChaCha key (32 B)                              AES key (32 B)             HMAC key (64 B)       field key
```

- The **VMK** (vault master key) is random, 256 bits, and derived from no password: changing the
  password only re-wraps it. **Rotation** generates a new one and re-encrypts every sealed
  field: an old copy of the file plus the old password reveal nothing about later data.
- Every slot wraps the same VMK with **AES‑256‑GCM**, a random nonce and the associated data
  `"OnlyYours/v1/slot" ‖ vault id ‖ kind ‖ parameters`: slots cannot be moved between vaults or
  have their parameters changed without invalidating them.
- **Recovery key**: 240 random bits + a 16-bit checksum, in Crockford base32 (52 symbols in groups
  of 4; capitals and dashes optional, `O→0`, `I/L→1`). The checksum catches typos before any
  derivation.
- **Windows Hello**: an RSA key in the TPM (KeyCredentialManager) signs, after user
  verification, a random *challenge* stored in the slot; the (deterministic) signature is the
  raw material of KEK₃. Copying the file to another PC is not enough: that TPM is required.
- Every derivation includes the **vault id** in the HKDF `info` field (domain separation between
  vaults) and a versioned label.

---

## 4. The `.onlyyours` file

```
magic[8]          89 4F 59 56 54 0D 0A 1A        ("\x89OYVT\r\n\x1a", detects text-mode corruption)
u16 version · u16 flags · u32 header length
vault id[16] · u64 write counter
u8 slot count · { u8 kind · u16 length · body }*
   password:  algorithm · m (KiB) · t · p · salt[32] · nonce[12] · wrapped VMK[48]
   recovery:  salt[32] · nonce[12] · wrapped VMK[48]
   Hello:     credential name · challenge · salt[32] · nonce[12] · wrapped VMK[48]
per-save salt[32] · ChaCha nonce[12] · AES nonce[12]
──────────── end of header (plaintext, but authenticated) ────────────
AES‑256‑GCM( ChaCha20‑Poly1305( u32 length ‖ document ‖ zeros up to PADMÉ ) )   + 2 tags of 16 B
HMAC‑SHA‑512( header ‖ ciphertext )                                             64 B
```

- The whole header is the **associated data of both AEADs** and is covered by the HMAC.
- Salt and nonces change on every save: the payload keys are always fresh.
- Opening checks the HMAC **first** (in constant time) and only then decrypts.
- Argon2 parameters are checked before deriving (8 MiB ≤ m ≤ 4 GiB, 1 ≤ t ≤ 64, 1 ≤ p ≤ 64,
  m ≥ 8p): a tampered file can neither request absurd allocations nor weak parameters without
  failing slot authentication.
- **PADMÉ** rounds the document size up (minimum 512 bytes, overhead ≤ ~12 %): the file does not
  reveal how many entries it holds.
- The document is a versioned binary TLV format; unknown fields written by future versions are
  **preserved byte for byte** on save.

---

## 5. Per-field encryption

Every sensitive value (passwords, notes, card numbers, CVVs, PINs, IBANs, private keys, API
secrets, 2FA secrets, secret custom fields, password history, the private keys of the sharing
identity) is sealed **on its own**:

```
blob = 0x01 ‖ nonce[12] ‖ AES‑256‑GCM( u32 length ‖ UTF‑8 ‖ zeros up to a multiple of 32 B ) ‖ tag[16]
aad  = "OnlyYours/v1/field" ‖ entry id ‖ field id
```

- The associated data binds the blob to its entry and field: a secret cannot be moved from one
  entry or field to another.
- Fields **stay sealed in RAM** for the whole session: the open document holds only blobs. A value
  is opened only to show, copy or type it, in locked memory, and wiped right afterwards.
- Reused passwords are recognized by comparing **HMAC‑SHA‑256** digests under a dedicated index
  key, without keeping any plaintext.
- The 2FA secret is a blob too: issuer, account, algorithm, digits and period are sealed with it
  (the file does not even say which service the code is for).

---

## 6. Writes, backups and anti-rollback

- **Atomic write**: a temporary file opened write-through, `FlushFileBuffers`, then
  `ReplaceFile`. A crash can never leave a half-written vault.
- Writes run on a dedicated thread: every snapshot is encrypted with the key that is valid *at
  that moment* (even during a rotation).
- **Encrypted backups** in `backups\`: on the first save of each session and at most every
  15 minutes, the last 20 kept. They are complete vaults: they open only with the password that
  was valid when they were made (after a password change you can choose to delete them).
- **Anti-rollback**: the write counter lives in the authenticated header; OnlyYours remembers the
  highest value seen for each vault (`settings.json`). If the opened file has a lower counter, it
  warns and records the event in the audit log.
- The audit log (inside the vault, hence encrypted) keeps creation, unlocks and the method used,
  password changes, recovery regeneration, Hello, imports, exports, migrations, restores,
  sharing, rollbacks and emptying of the trash.

---

## 7. Memory protection

| Structure | Protection |
| --- | --- |
| `SecureRegion` / `SecureBuffer` | `VirtualAlloc` + `VirtualLock` (out of the page file), working-set quota raised at start-up, zeroing guaranteed on release (finalizer included), global registry for `WipeAll` |
| `ProtectedBuffer` (VMK, field key, index key) | encrypted with `CryptProtectMemory(SAME_PROCESS)` and the page set to `PAGE_NOACCESS` while unused; opened only through a reference-counted *lease*, re-sealed when the last one ends |
| `SecretDraft` (what you type) | protected buffer; the password editor (`SecureField`) is a custom control that draws glyphs directly, with no `TextBox` and no `string`; IME disabled |
| Migration | BetterOnlyYours' decrypted JSON is read with `Utf8JsonReader` straight from locked memory: no secret ever becomes a string |
| Clipboard | data is copied with the Win32 API from locked memory; the clipboard block is zeroed before it is released |
| Exit and crashes | `SecureMemory.WipeAll()` on exit, on session lock and from `UnhandledException`; a fast exit that does not wait for WPF teardown |

---

## 8. Process and desktop protections

Applied before WPF is loaded:

- `SetDefaultDllDirectories(APPLICATION_DIR | SYSTEM32 | USER_DIRS)`: no DLLs from the current
  folder or the `PATH`.
- Mitigation policies: no remote or low-integrity images, System32 images preferred; **legacy
  extension points disabled** (AppInit DLLs, global hooks, legacy IMEs, LSPs).
- **Process DACL**: SYSTEM and administrators keep control, the user can see and close the
  process, but no other unprivileged process can read or write its memory, inject threads or
  duplicate its handles.
- **Capture exclusion** (`WDA_EXCLUDEFROMCAPTURE`): the window shows up black in screenshots,
  recordings and screen sharing.
- **Protected clipboard**: the `ExcludeClipboardContentFromMonitorProcessing`,
  `CanIncludeInClipboardHistory = 0` and `CanUploadToCloudClipboard = 0` formats; cleared after
  the chosen delay, only if the clipboard still holds what OnlyYours put there (sequence number),
  and on lock.
- **Single instance** per user session: a second launch brings the first one forward and exits.
- **Auto-type** sends keys only while the target window is still the chosen one and stops if the
  focus moves.

---

## 9. Locking and unlocking

- Locks on **inactivity** (input anywhere in the session, 10 minutes by default), when
  **Windows** is locked, before **sleep/hibernation** and, if asked, on **minimize**. On lock the
  session is closed (writes completed, keys wiped), dialogs and the palette are closed and the
  clipboard is cleared.
- After **5 wrong attempts** each new attempt is delayed exponentially (2 s, 4 s … up to 60 s);
  the number of failures since the last unlock is shown.
- An unlock with the recovery key is flagged and suggests setting a new master password.

---

## Sealed sharing

**Identity.** Every vault can create an identity: **ECDH P‑384**, **ML‑KEM‑1024**, **ML‑DSA‑87**
and **ECDSA P‑384** key pairs. The private halves (or seeds) are sealed fields inside the vault.
The public half is an `ONLYYOURS-ID:…` block (or an `.oyid` file). **Fingerprint** = the first
20 bytes of `SHA‑512("OnlyYours/v1/identity" ‖ the four public keys)`, shown as 10 hexadecimal
groups and as a unique **seal identicon**, to be compared out loud before marking a contact as
verified.

**Package for a recipient** (`.oypkg`, magic `89 4F 59 50 4B 0D 0A 1A`):

1. Ephemeral ECDH P‑384 with the recipient's key **and** ML‑KEM‑1024 encapsulation.
2. `KEK = HKDF‑SHA‑512(ECDH ‖ ML‑KEM, salt = SHA‑512(ephemeral public ‖ KEM ciphertext ‖ recipient public keys), info = "OnlyYours/v1/package/hybrid-kem" ‖ package id)`:
   it stays secure as long as **at least one** of the two exchanges holds (classical today,
   post-quantum tomorrow).
3. The content is encrypted with the vault's own ChaCha20‑Poly1305 → AES‑256‑GCM cascade +
   HMAC‑SHA‑512, header as associated data.
4. **Dual signature** by the sender over `SHA‑512("OnlyYours/v1/package/signature" ‖ package)`:
   ML‑DSA‑87 (with context) **and** ECDSA P‑384. Both must verify on opening; the sender is
   matched against the verified contacts.

**Password package**: Argon2id (parameters stored in the package, within the safety bounds)
instead of the key exchange, same encryption.

On import, every secret goes from locked memory into a sealed field of the destination vault.

---

## 11. Imports, exports and migration

- **Incoming CSV**: read into locked memory; secret values become sealed fields immediately.
  Afterwards OnlyYours offers to **overwrite and delete** the CSV file.
- **Outgoing CSV**: requires the master password again and shows an explicit warning; cells that
  a spreadsheet would treat as formulas are quoted without altering their value.
- **Vault copy** and **encrypted package** are the recommended exports.
- **Migration from BetterOnlyYours**: the old file is opened read-only (its `.bak` copy too);
  AES‑256‑GCM with the header as associated data, PBKDF2‑HMAC‑SHA256 (v1: 3000 iterations; v2/v3:
  from the file) or Argon2id (v3); the `BOYTOTP1` 2FA boxes are opened with HKDF‑SHA256
  (`betteronlyyours/totp/v1/entry-key`) + AES‑256‑GCM and resealed. The new vault always uses
  OnlyYours' own parameters.

---

## 12. Stated limits

- **Non-sensitive fields** (title, username, URL, tags, plain custom text fields) are encrypted in
  the file but, while the vault is open, live as .NET strings so they can be searched and shown.
  That is what *secret* custom fields are for.
- **Multi-line secrets** (notes, private keys) are shown masked, but while they are being edited
  they must use WPF's text engine, so they exist as text in that box while the editor is open.
  Around that window: no undo history, a *password* input scope (TSF, IMEs and Windows text
  suggestions never learn the content), copy/cut only through the protected clipboard, drag-out
  to other apps blocked, and the box is emptied when the editor closes.
- The .NET **garbage collector** may have copied strings of non-sensitive fields in the past;
  OnlyYours cannot zero those (it does for every buffer it controls).
- Showing a secret on screen means WPF draws it: revealed text goes through rendering glyphs for
  as long as it stays visible.
- **Auto-type** and the clipboard hand the secret to another application by definition.
- **Windows Hello** relies on the security of the TPM and of the system; the master password
  always remains valid and is required for critical operations.
- Memory protection from other processes does not hold against administrators, privileged
  debuggers, drivers or kernel malware.

---

## 13. Verification

```powershell
dotnet test OnlyYours.slnx
```

Security-relevant tests (in `tests/OnlyYours.Core.Tests`): RFC 9106 / 7693 / 6238 / 4226 vectors;
tampering with every byte of a cascaded ciphertext; bit flips at different points of the vault
file (header, slots, payload, HMAC) and truncation; wrong password and wrong recovery key;
recovery keys with typos; key rotation (old blobs and the old recovery key stop working);
binding of blobs to entry and field; out-of-range KDF parameters; tampered packages and packages
opened by the wrong recipient; ML‑DSA/ECDSA signatures; migration from genuine BetterOnlyYours
files of every version.
