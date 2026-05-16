<div align="center">

# 🔐⚡ BETTER ONLY YOURS

### _Military-Grade Encrypted Credential Vault // Secure Your Digital Fortress_

[![Flutter](https://img.shields.io/badge/Flutter-3.11+-02569B?style=for-the-badge&logo=flutter&logoColor=white)](https://flutter.dev)
[![Windows](https://img.shields.io/badge/Windows-Desktop-0078D6?style=for-the-badge&logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-Private-FF5470?style=for-the-badge)]()
[![Security](https://img.shields.io/badge/Security-AE--256--GCM-00D4FF?style=for-the-badge)]()

**Your secrets. Encrypted. Untouchable. Yours.**

</div>

---

## 🚀 What Is This?

**BetterOnlyYours** is not just another password manager. It's a **fortress**.

A cyberpunk-themed, military-grade encrypted credential vault built for Windows desktop that treats your sensitive data with the paranoia it deserves. No cloud. No compromises. No backdoors. Just **pure, unbreakable cryptography** wrapped in a sleek, futuristic UI.

---

## 🔥 Features

### 💀 **Zero-Trust Security Architecture**
- **AES-256-GCM** encryption — the gold standard trusted by governments and militaries
- **PBKDF2** key derivation with **60,000 iterations** — brute force? Good luck with that
- **Keccak-512** hashing — SHA-3 family, next-gen cryptographic security
- **128-bit authentication tags** — tamper-proof integrity verification
- **Secure memory wiping** — sensitive data zeroed out after use, even from RAM

### 🛡️ **Bulletproof Data Protection**
- Atomic write operations — no corruption, ever. Power failure? We got you.
- Backup file rotation — automatic fallback if something goes sideways
- Magic byte signature verification — file integrity checked at byte level
- Version-controlled vault format — future-proof architecture

### ⚡ **Cyberpunk UI/UX**
- Dark theme with neon glow effects — because security should look badass
- Custom window frame with draggable title bar
- System-wide hotkey (`Ctrl+Alt+P`) — instant vault access from anywhere
- Real-time search with fuzzy matching — find credentials in milliseconds
- Smooth animations and transitions — 60fps eye candy

### 🎯 **Smart Workflow**
- Auto-lock on window minimize — step away, stay secure
- Global search overlay — access your vault without breaking focus
- One-click strong password generation (64 characters of pure entropy)
- Instant clipboard copy — paste credentials anywhere, anytime
- Credential profiles — organize multiple accounts like a pro

---

## 🛠️ Tech Stack

| Category | Technology |
|----------|-----------|
| **Framework** | Flutter 3.11+ (Dart) |
| **Platform** | Windows Desktop |
| **Encryption** | PointyCastle (AES-256-GCM, PBKDF2, Keccak-512) |
| **State Management** | Provider |
| **Window Control** | window_manager |
| **Hotkeys** | hotkey_manager |
| **Crypto** | encrypt, crypto, archive |

---

## 📦 Installation

### Prerequisites
- **Windows 10/11**
- **Flutter SDK 3.11+**
- **Dart SDK 3.11+**

### Build From Source
```bash
# Clone the repository
git clone https://github.com/YOUR_USERNAME/betteronlyyours.git
cd betteronlyyours

# Install dependencies
flutter pub get

# Run in debug mode
flutter run -d windows

# Build release executable
flutter build windows --release
```

The compiled executable will be in: `build/windows/x64/runner/Release/betteronlyyours.exe`

---

## 🎮 Usage

### First Launch
1. Run the application
2. Create your **Master Password** (minimum 8 characters — but come on, make it stronger)
3. Your encrypted vault is initialized. Welcome to the fortress.

### Daily Use
- **Unlock**: Enter your master password on the lock screen
- **Add Credential**: Click "ADD CREDENTIAL" and give it a name
- **Edit Content**: Select a profile, paste/edit your sensitive data, hit "SAVE DATA"
- **Search**: Use the sidebar search or press `Ctrl+Alt+P` for global overlay
- **Generate Password**: Click the key icon to generate a 64-character beast and copy to clipboard
- **Lock**: Minimize the window — auto-lock engaged

### Keyboard Shortcuts
| Shortcut | Action |
|----------|--------|
| `Ctrl+Alt+P` | Open global search overlay (system-wide) |
| `Enter` | Submit current form/dialog |

---

## 🔐 Security Details

### Vault File Format (`credentials.plf`)
```
[4 bytes]  Magic Bytes: 0xFF 0xFE 0x0D 0x0A
[1 byte]   Version: 0x01
[16 bytes] Salt (random, per-save)
[12 bytes] Nonce (random, per-save)
[Variable] Encrypted payload (AES-256-GCM)
```

### Encryption Flow
1. Master password → UTF-8 bytes
2. Random salt generated (16 bytes)
3. PBKDF2-SHA256 derives 256-bit key (60,000 iterations)
4. Random nonce generated (12 bytes)
5. JSON credentials → UTF-8 plaintext
6. AES-256-GCM encrypts with authenticated header
7. Atomic write with backup rotation
8. **Secure wipe**: password, key, and plaintext zeroed from memory

### Decryption Flow
1. Read vault file, verify magic bytes and version
2. Extract salt and nonce from header
3. Derive key from password + salt
4. AES-256-GCM decrypt + authenticate
5. Parse JSON → credential map
6. **Secure wipe**: sensitive intermediates destroyed

---

## ⚠️ Important Notes

- **NO CLOUD STORAGE** — Everything is local. You lose the file, it's gone forever.
- **NO PASSWORD RECOVERY** — Forget your master password? Your data is cryptographically ashes.
- **BACK UP YOUR VAULT** — Copy `credentials.plf` to secure offline storage.
- **DO NOT SHARE** — The vault file is encrypted, but operational security matters.

---

## 🏗️ Project Structure

```
betteronlyyours/
├── lib/
│   ├── core/
│   │   ├── app_state.dart          # State management with Provider
│   │   └── security_core.dart      # Cryptography engine (the beast)
│   ├── ui/
│   │   └── main_window.dart        # All UI components & screens
│   └── main.dart                   # App entry point + hotkey setup
├── windows/                        # Windows-specific build files
├── test/                           # Unit tests
└── pubspec.yaml                    # Dependencies
```

---

## 🎨 UI Screenshots

> **Cyberpunk Dark Theme** — Neon purple & cyan accents on deep navy

- **Lock Screen**: Sleek authentication with glow effects
- **Vault Interface**: Two-panel layout (credentials list + editor)
- **Search Overlay**: Frosted glass blur with instant results
- **Custom Title Bar**: Draggable, minimal, cyberpunk aesthetic

---

## 🤝 Contributing

This is a **private security-focused project**. If you want to contribute:
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/InsaneSecurity`)
3. Commit your changes (`git commit -m 'Add quantum-resistant encryption'`)
4. Push to the branch (`git push origin feature/InsaneSecurity`)
5. Open a Pull Request

**Security audits and cryptographic improvements are always welcome.** 🔒

---

## 📜 License

Private. Proprietary. **Yours.**

This project is not open for public distribution. Use at your own risk. No warranties. No guarantees. Just **encryption**.

---

## 🙏 Acknowledgments

- **Flutter Team** — For the amazing cross-platform framework
- **PointyCastle** — Dart's cryptographic powerhouse
- **Cyberpunk Aesthetic** — Because security tools shouldn't be boring

---

<div align="center">

### 🔐 Built for paranoia. Designed for power. Encrypted for eternity.

**BetterOnlyYours** — _Because your secrets deserve better._

⚡ _Stay secure. Stay paranoid. Stay yours._ ⚡

</div>
