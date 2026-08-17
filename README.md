<div align="center">

# DoubleTake 🚀
### Modern, Zero-Friction Desktop Translation Companion for Windows

[![Windows 11 Compatible](https://img.shields.io/badge/Windows%2011-Compatible-0078D4?style=flat-square&logo=windows11&logoColor=white)](https://github.com/SaidimM/DoubleTake/releases)
[![Release](https://img.shields.io/badge/Release-v1.0.0-10B981?style=flat-square&logo=github)](https://github.com/SaidimM/DoubleTake/releases/tag/v1.0.0)
[![Framework](https://img.shields.io/badge/WinUI%203-Windows%20App%20SDK%201.5-6366F1?style=flat-square)](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)

<p align="center">
  <b>Highlight any text, double-tap <kbd>Ctrl</kbd>, and get instant translations right beside your cursor.</b><br>
  No browser tabs. No window switching. No heavyweight bloat.
</p>

[**Download Latest Release (v1.0.0)**](https://github.com/SaidimM/DoubleTake/releases/tag/v1.0.0) • [**Key Features**](#-key-features) • [**Quick Start**](#-quick-start--installation) • [**Supported Engines**](#-7-global-translation-engines)

---

</div>

## 💡 Why DoubleTake?

Most translation tools force you to break your workflow: copy text, alt-tab to a browser or heavy Electron app, paste, read, and alt-tab back.

**DoubleTake** was engineered as an invisible, native desktop companion that stays completely out of your way until called:

* **⚡ Zero-Friction Trigger:** Highlight text in any application (IntelliJ IDEA, VS Code, Chrome, PDF, Slack, Word, Terminal) and double-tap <kbd>Ctrl</kbd>.
* **🪟 Content-Elastic Floating UI:** The translation popup intelligently resizes to fit the exact volume of translated text—giving you zero-scroll clarity on 95% of translations.
* **🎯 100% Focused & Distraction-Free:** No OCR overhead, no chatbot clutter, and no bulky vocabulary decks. Just instant, accurate linguistic translation.
* **🛡️ Zero-Distraction Gaming Mode:** Automatically suppresses the hotkey in fullscreen 3D games and user-blacklisted software.

---

## ✨ Key Features

### 1. ⚡ Cursor-Anchored Quick Popup
* Appears immediately beside your cursor without stealing desktop prominence.
* **Auto-Language Detection:** Intelligently recognizes source languages and chooses the most appropriate target language.
* **One-Click Actions:**
  * 📋 **Copy Translation:** Instant copy with subtle visual confirmation.
  * 🔄 **Replace Selection:** Directly replaces the highlighted text in your active editor with the translated text.
  * 📌 **Pin Window:** Keeps the popup floating for persistent reference.
  * ↗️ **Open in Workspace:** Expands into the full dual-pane translation workspace with a single click.

### 2. 🌐 7 Global Translation Engines
Switch effortlessly between industry-leading global and regional engines:

| Engine | Coverage / Strengths | Authentication |
| :--- | :--- | :--- |
| **Google Translate** | Global #1 · 130+ languages · High speed | **Free & Built-in** (No setup) |
| **Bing / Edge Translator** | Microsoft Azure translation service | **Free & Built-in** (No setup) |
| **DeepL API** | Nuanced European & Japanese phrasing | DeepL API Key (Free & Pro tier) |
| **Baidu Fanyi (百度翻译)** | China #1 leading linguistic engine | App ID + Secret Key |
| **Naver Papago (파파고)** | South Korea #1 conversational model | Client ID + Client Secret |
| **Yandex Translate** | High accuracy for Russian & CIS languages | API Key |
| **Youdao Translate (网易有道)** | Specialized technical & academic lexicon | App Key + App Secret |

### 3. 🔐 Hardware-Encrypted Credential Vault
All user API keys and secrets are encrypted directly into the native Windows `PasswordVault` (Windows Credential Manager). Credentials never touch plain text config files or external servers.

### 4. 🎮 Smart Gaming & Process Exclusions
* **Automatic Fullscreen Detection:** Intelligently recognizes true 3D exclusive/borderless games (e.g., Counter-Strike, Valorant, Apex Legends) and pauses the hotkey.
* **Visual App Exclusion Shelf:** Select specific desktop applications via an interactive process picker to prevent shortcut collisions with specialized software.

### 5. 📜 Persistent Translation History
* All translations are automatically saved locally in `%USERPROFILE%\.doubletake\history.json`.
* History survives application updates and reinstallations.
* Includes real-time search, filter by source/target language, one-click export, and animated card expand/collapse.

### 6. 💎 Windows 11 Native Aesthetic (Fluent 2)
* Full **Mica & Acrylic** backdrop materials with subtle elevation shadows.
* Harmonious light and dark themes synchronized with your Windows system preferences.
* Minimized memory footprint (~150MB active, near-zero CPU when idle).
* Native Win32 system tray integration with single-instance lifecycle management.

---

## ⌨️ Shortcuts & Gestures

| Action | Shortcut / Gesture |
| :--- | :--- |
| **Translate Highlighted Text** | Double-tap <kbd>Ctrl</kbd> |
| **Dismiss Popup** | <kbd>Esc</kbd> or click outside (unless pinned) |
| **Instant Copy Translation** | <kbd>Ctrl</kbd> + <kbd>C</kbd> or click **Copy** button |
| **Translate in Full Workspace** | <kbd>Enter</kbd> (in Workspace) |
| **Switch Primary Engine** | System Tray Right-Click ➔ Select Engine |
| **Restore / Minimize App** | Left-Click System Tray Icon |

---

## 📦 Quick Start & Installation

### Option 1: One-Click PowerShell Script (Recommended)
1. Download `DoubleTake_1.0.7.0_x64.msix`, `DoubleTake_1.0.7.0_x64.cer`, and `Install.ps1` from the [Latest Release](https://github.com/SaidimM/DoubleTake/releases/tag/v1.0.0).
2. Right-click `Install.ps1` and select **Run with PowerShell** (or run `powershell -ExecutionPolicy Bypass -File .\Install.ps1`).
3. DoubleTake will automatically install and launch in your system tray.

### Option 2: Manual Sideloading
1. Double-click `DoubleTake_1.0.7.0_x64.cer` ➔ Click **Install Certificate...** ➔ Select **Local Machine** ➔ Place certificate in the **Trusted People** store.
2. Double-click `DoubleTake_1.0.7.0_x64.msix` and click **Install**.

---

## 🔒 Privacy & Data Ethics

* **100% Client-Side:** DoubleTake has no tracking, no analytics, and no telemetry.
* **Direct Network Calls:** Translation requests travel directly from your machine to the configured translation provider (Google, Microsoft, DeepL, etc.) over HTTPS.
* **Zero Cloud Storage:** Your translation history and settings are stored strictly on your local disk at `%USERPROFILE%\.doubletake\`.

---

## 🛠️ Tech Stack & Architecture

* **UI Framework:** WinUI 3 (Windows App SDK 1.5)
* **Runtime:** .NET 8 (C# 12)
* **Input Layer:** Win32 Low-Level Keyboard Hook (`WH_KEYBOARD_LL`) + `SendInput`
* **Inter-Process Control:** System-wide Named Mutex + Windows Message Subclassing (`comctl32.dll`)
* **Security:** `Windows.Security.Credentials.PasswordVault`

---

## 📄 License

Distributed under the [MIT License](LICENSE). Built with ❤️ for productive developers, writers, and multilingual professionals.
