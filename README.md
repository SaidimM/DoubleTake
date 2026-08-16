# DoubleTake 🚀

> **Zero-friction desktop translation companion for Windows.**

DoubleTake is a modern, native Windows 11 utility built with **WinUI 3**, **Windows App SDK 1.5**, and **.NET 8**. Highlight any text in any application and double-tap <kbd>Ctrl</kbd> to pop up an instant translation overlay.

---

## ✨ Key Features

- ⚡ **Instant Double-Ctrl Gesture:** Low-level global keyboard hook triggers a floating Mica overlay near your mouse cursor.
- 🌐 **7 Translation Engines Supported:**
  - **Google Translate** (Free & Built-in · Global #1)
  - **Bing / Edge Translator** (Free & Built-in · Microsoft)
  - **DeepL API** (Global & High Accuracy / EU / JP)
  - **Baidu Fanyi (百度翻译)** (China #1 · AppID + SecretKey)
  - **Naver Papago (파파고)** (South Korea #1 · ClientID + Secret)
  - **Yandex Translate** (Russia & CIS #1 · API Key)
  - **Youdao Translate (网易有道)** (China #2 · AppKey + Secret)
- 🔒 **Encrypted Key Storage:** All API credentials are stored securely via the **Windows Credential Manager** (`Windows.Security.Credentials.PasswordVault`).
- 🛡️ **Smart Auto-Failover:** Automatically falls back to Google / Bing if paid API quotas or rate limits are reached.
- 🎨 **Fluent 2 Design:** Seamless Windows 11 dark/light mode with Mica backdrop and rounded geometry.

---

## 📂 Repository Architecture

```
DoubleTake/
├── .github/
│   └── workflows/
│       └── ci.yml               # GitHub Actions CI build pipeline
├── docs/
│   ├── MVP_SCOPE.md            # Product MVP specifications
│   └── mockups/                # UI wireframes, prototypes & visual specs
├── scripts/
│   └── create_assets.ps1       # Logo & visual asset generation script
├── src/
│   └── DoubleTake/
│       ├── Assets/             # App icons, splash screens & tiles
│       ├── Helpers/            # Win32 clipboard & keyboard utilities
│       ├── Services/           # Translation providers, hotkey hooks & secure settings
│       ├── Views/              # WinUI 3 XAML views & quick popups
│       ├── App.xaml / .cs      # Application lifecycle entrypoint
│       ├── app.manifest        # Win32 common controls & DPI awareness
│       ├── Package.appxmanifest# MSIX package identity & capabilities
│       └── DoubleTake.csproj   # C# project definition
├── DoubleTake.sln              # Visual Studio solution file
├── .gitignore                  # Git ignore rules for .NET / WinUI 3
├── LICENSE                     # MIT License
└── README.md
```

---

## 🛠️ Build & Package

### Prerequisites
- Windows 10/11 (Version 1809+ / Build 17763+)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows App SDK 1.5

### Publish & Package MSIX
```powershell
# Build and package from root
dotnet publish src/DoubleTake/DoubleTake.csproj -c Release -r win-x64 -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true -p:AppxPackageDir="bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\PackageOutput\"
```

---

## 📄 License
Distributed under the [MIT License](LICENSE).
