# DoubleTake 🚀

> **Zero-friction desktop translation companion for Windows.**

DoubleTake is a native Windows 11 utility built with **WinUI 3**, **Windows App SDK**, and **.NET 8**. Highlight any text in any application and double-tap <kbd>Ctrl</kbd> to pop up an instant translation overlay.

---

## ✨ Features

- ⚡ **Instant Gesture:** Double-tap <kbd>Ctrl</kbd> triggers a lightweight floating Mica popup near your cursor.
- 🌐 **7 Translation Engines Supported:**
  - **Google Translate** (Free & Built-in · Global #1)
  - **Bing / Edge Translator** (Free & Built-in · Microsoft)
  - **DeepL API** (Global & High Accuracy / EU / JP)
  - **Baidu Fanyi (百度翻译)** (China #1 · AppID + SecretKey)
  - **Naver Papago (파파고)** (South Korea #1 · ClientID + Secret)
  - **Yandex Translate** (Russia & CIS #1 · API Key)
  - **Youdao Translate (网易有道)** (China #2 · AppKey + Secret)
- 🔒 **Encrypted Key Storage:** All API credentials are encrypted with Windows Credential Vault (`PasswordVault`).
- 🛡️ **Smart Auto-Failover:** Automatically fails over to Google/Bing if paid API quotas are exceeded.
- 🎨 **Fluent 2 Design:** Built with Windows 11 Mica backdrop, rounded geometry, and full dark/light theme support.

---

## 🛠️ Build & Run

### Prerequisites
- Windows 10/11 (Version 1809+ / Build 17763+)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows App SDK 1.5

### Publish & Package MSIX
```powershell
dotnet publish -c Release -r win-x64 -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true -p:AppxPackageDir="bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\PackageOutput\"
```

---

## 📄 License
MIT License
