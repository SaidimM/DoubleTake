# QuickTranslator - Product Requirement Document (PRD) & MVP Scope

**Project Name:** QuickTranslator  
**Platform:** Windows 11 / Windows 10 (Targeting Microsoft Store via MSIX)  
**Technology Stack:** WinUI 3 (Windows App SDK + .NET 8 / C#)  
**Design Standard:** Microsoft Fluent 2 Design System (Mica / Acrylic materials, Segoe UI Variable typography, 4px grid spacing)

---

## 1. Executive Summary & Core Value Proposition

QuickTranslator is a modern, lightweight native Windows 11 translation application designed for effortless personal and professional multilingual workflows. It combines a full-featured desktop workspace with an instant-access companion overlay triggered by a global keyboard shortcut.

---

## 2. MVP Screen Architecture

The application comprises **3 primary screens** in the main window plus **1 companion overlay window**:

```
QuickTranslator
│
├── 🪟 Main Window (NavigationView)
│   ├── 1. Translate Screen (Main Workspace)
│   ├── 2. History Screen (Searchable & Filterable Logs)
│   └── 3. Settings Screen (Configuration, Providers, Hotkeys)
│
└── 🪟 Companion Window
    └── 4. Quick Popup Overlay (Mini floating card triggered via Double-tap Ctrl)
```

---

### Screen 1: Translate Screen (`TranslatePage.xaml`)
* **Top Language Bar**:
  * Source Language `ComboBox` (with *Auto Detect* support).
  * Centered Swap Button (`⇄`) with smooth transition animation.
  * Target Language `ComboBox`.
* **Left Input Card**:
  * Translucent Mica material container with 8px rounded corners.
  * Multi-line `TextBox` with auto-focus and auto-expand.
  * Live character count display (`0 / 5000 chars`).
  * `Clear` (`✕`) button.
* **Right Output Card**:
  * Translucent Mica material container.
  * Selectable read-only translated text.
  * `Copy` (`📋`) button with confirmation flyout/tooltip.
  * `Favorite / Star` (`⭐`) toggle to bookmark key phrases.
  * Status indicator badge (`● Ready` / `Translating...` / `Auto-recovered`).

---

### Screen 2: History Screen (`HistoryPage.xaml`)
* **Top Search & Filters Bar**:
  * `AutoSuggestBox` for real-time phrase searching across source and target texts.
  * Filter chips: `All`, `Starred (Favorites)`, and language pair filters (e.g. `EN → ES`).
  * `Clear All History` button with confirmation dialog.
* **History List**:
  * Virtualized `ListView` with rounded card items.
  * Items show: language pair badge, source snippet, translated snippet, relative timestamp (`5 mins ago`), star toggle, copy button, and delete button.
  * Clicking any history card restores the text directly into the Translate workspace.

---

### Screen 3: Settings Screen (`SettingsPage.xaml`)
* **Appearance & Behavior**:
  * App Theme selector (`System Default`, `Light`, `Dark`).
  * Backdrop Material selector (`Mica`, `Mica Alt`, `Acrylic`).
* **Translation Providers & Services** *(Detailed in Section 3)*.
* **Global Shortcuts**:
  * `Double-tap Ctrl` toggle switch.
  * Custom hotkey recorder field (e.g. `Ctrl + Alt + T`).
* **Default Languages**:
  * Default Source and Target language dropdowns for app launch.
* **Startup & System Tray**:
  * `Launch on Windows Startup` toggle (via Windows `StartupTask` API).
  * `Minimize to System Tray on close` toggle.
* **About Card**:
  * Version `1.0.0`, Microsoft Store certification badge, and links to privacy policy & GitHub repository.

---

### Screen 4: Companion Quick Popup (`QuickPopupWindow.xaml`)
* Lightweight, borderless acrylic floating window.
* Activated anywhere on Windows via **Double-tap `Ctrl`**.
* Direct focus on input for instantaneous lookup.
* Light-dismiss behavior: automatically hides on `Esc` key or when clicking outside.

---

## 3. Translation Engine & Global Provider Matrix

### 3.1 Two-Layer Fallback Architecture
To guarantee 99.9% uptime worldwide (including regions where specific services are blocked or restricted):

$$\text{User Configured Provider} \xrightarrow{\text{on failure / timeout}} \text{Google Translate (Free)} \xrightarrow{\text{on failure / blocked}} \text{Bing / Edge Translator (Free)}$$

* **Silent Auto-Recovery**: If the primary provider fails, QuickTranslator automatically tries Fallback 1, then Fallback 2, displaying a subtle notice badge: `"Translated via Bing (Fallback)"`.

### 3.2 Provider Selection & Global Popularity Ordering

| Rank | Provider | Target Region / Audience | Credential Requirements |
| :---: | :--- | :--- | :--- |
| **1** | **Bing / Edge Translator** | Global (accessible worldwide, including China) | **Free / Built-in** (No key required) |
| **2** | **Google Translate** | Global | **Free / Built-in** (No key required) |
| **3** | **DeepL** | Global / Europe / Japan | API Key (Free tier `*-fx` or Pro) |
| **4** | **Baidu Fanyi (百度翻译)** | China (#1 most popular) | App ID + Secret Key |
| **5** | **Naver Papago (네이버 파파고)** | South Korea (#1 most popular) | Client ID + Client Secret |
| **6** | **Yandex Translate** | Russia & CIS countries | API Key |
| **7** | **Youdao Translate (网易有道)** | China (#2 popular for education) | App Key + App Secret |
| **8** | **Microsoft Azure Translator** | Enterprise / Global | Subscription Key + Region |
| **9** | **LibreTranslate** | Privacy / Self-Hosted | Server URL + Optional API Key |

---

## 4. Technical Specifications & Quality Attributes

* **Target Framework:** .NET 8.0 (`net8.0-windows10.0.19041.0` or higher)
* **UI Framework:** Windows App SDK (WinUI 3)
* **Local Storage:** SQLite (`Microsoft.Data.Sqlite`) with EF Core or Dapper for indexed history lookup.
* **Packaging:** MSIX Package for direct Microsoft Store distribution.
* **Global Keyboard Hook:** Low-level Win32 hook (`SetWindowsHookExW` with `WH_KEYBOARD_LL`) running on a dedicated background thread.
* **Network Client:** `HttpClientFactory` with retry policies (`Polly`), connection pooling, and 3-second timeout limits per hop.

---

## 5. Scope Boundaries

### In-Scope for MVP (V1)
- [x] Full WinUI 3 Navigation & Fluent 2 UI.
- [x] Translate workspace with auto-detect, debounce, clear, copy, star.
- [x] Searchable, filterable translation history.
- [x] Two-layer fallback engine (`User Provider → Google → Bing`).
- [x] Provider settings with dynamic fields and "Test Connection".
- [x] Double-tap `Ctrl` global quick popup overlay.
- [x] System Tray minimization & Windows Startup registration.
- [x] Dark / Light / System theme support with Mica material.

### Deferred to Post-MVP (V2+)
- [ ] AI / LLM translation providers (OpenAI GPT-4o, Anthropic Claude, Google Gemini).
- [ ] OCR Screen Capture translation (translate area on screen).
- [ ] Text-to-Speech (TTS) audio pronunciation and Speech-to-Text (STT) voice input.
- [ ] Document / PDF batch translation.
