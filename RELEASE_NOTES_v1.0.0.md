# DoubleTake v1.0.0 — Modern Desktop Translation Companion

> **DoubleTake** is an ultra-fast, lightweight, and distraction-free translation companion for Windows 11. Built with WinUI 3, Windows App SDK, and Microsoft Fluent 2 design principles.

---

### ✨ Key Features

* ⚡ **Zero-Latency Double-<kbd>Ctrl</kbd> Trigger:** Highlight any word, phrase, sentence, or code comment in any app (IDE, browser, terminal, PDF reader) and double-tap <kbd>Ctrl</kbd> to translate instantly.
* 🪟 **Content-Elastic Floating Popup:** Dynamic zero-scroll layout anchored to your cursor. Includes instant copy, inline replacement, target language dropdown, and quick engine switching.
* 🌐 **7 Global Translation Engines:**
  * **Google Translate** *(Free & Built-in)*
  * **Bing / Edge Translator** *(Free & Built-in)*
  * **DeepL API** *(Global / EU / JP)*
  * **Baidu Fanyi / 百度翻译** *(China)*
  * **Naver Papago / 파파고** *(South Korea)*
  * **Yandex Translate** *(Russia & CIS)*
  * **Youdao Translate / 网易有道** *(China)*
* 🎮 **Smart Gaming & Application Exclusions:** Automatically pauses in 3D fullscreen games. Includes a sleek Excluded Apps shelf with native Win32 icon extraction and full-device process picker.
* 📜 **Persistent Translation History:** All translations are safely stored in `%USERPROFILE%\.doubletake\history.json`, surviving app updates and reinstalls.
* 🔐 **Hardware-Encrypted Credential Vault:** API keys are auto-saved directly into Windows `PasswordVault`.
* 🖥️ **Windows 11 Native Polish:** Fluent 2 Mica backdrops, single-instance process management, and a native Win32 system tray context menu.

---

### 📦 Installation Guide

#### Option A: One-Click PowerShell Script (Recommended)
1. Download `DoubleTake_1.0.7.0_x64.msix`, `DoubleTake_1.0.7.0_x64.cer`, and `Install.ps1`.
2. Right-click `Install.ps1` and select **Run with PowerShell** (or run `powershell -ExecutionPolicy Bypass -File .\Install.ps1`).

#### Option B: Manual Sideloading
1. Double-click `DoubleTake_1.0.7.0_x64.cer` ➔ Install Certificate ➔ **Local Machine** ➔ Place in **Trusted People** store.
2. Double-click `DoubleTake_1.0.7.0_x64.msix` and click **Install**.

---

**Full Changelog**: https://github.com/SaidimM/DoubleTake/commits/v1.0.0
