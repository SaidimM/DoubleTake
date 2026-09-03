# Privacy Policy for DoubleTake

**Effective Date:** September 3, 2026  
**Last Updated:** September 3, 2026

**DoubleTake** ("we", "our", or "the application") is an open-source desktop translation utility developed by Saidi ([SaidimM/DoubleTake](https://github.com/SaidimM/DoubleTake)). We respect your privacy and are committed to protecting it.

---

### 1. Data Collection & Processing

* **No Personal Data Collection:** DoubleTake does not collect, record, track, store, or sell any personal identifying information (PII), device identifiers, telemetry, or user analytics.
* **Text Selection & Clipboard Handling:** When you activate translation (e.g. by highlighting text and double-tapping <kbd>Ctrl</kbd>), DoubleTake momentarily reads the highlighted text in order to send it to the translation engine of your choice (such as Google Translate, Microsoft Bing, DeepL, or Baidu). DoubleTake does not retain, upload, or transmit your clipboard contents to any external servers operated by us.
* **Local Translation History:** If translation history is enabled, history records are stored purely locally on your device in your user directory (`%USERPROFILE%\.doubletake\history.json`). This data never leaves your computer.
* **API Credentials:** If you choose to configure personal API keys (e.g., DeepL or Baidu), they are stored locally and encrypted using the native Windows `PasswordVault`.

---

### 2. Third-Party Services

When performing translations, user-requested text is transmitted directly to the translation service provider you selected:
* **Google Translate** ([Google Privacy Policy](https://policies.google.com/privacy))
* **Microsoft Bing** ([Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement))
* **DeepL** ([DeepL Privacy Policy](https://www.deepl.com/privacy))
* **Baidu Fanyi** ([Baidu Privacy Policy](https://fanyi.baidu.com/))

The processing of translated text by these providers is governed by their respective privacy policies.

---

### 3. Permissions & Capabilities

* **Run Full Trust (`runFullTrust`):** Required as a desktop application to register global low-level keyboard hooks (`SetWindowsHookEx`) for the double-<kbd>Ctrl</kbd> shortcut and window cursor anchoring.
* **Internet Access:** Required solely to send translation requests to the chosen translation providers.

---

### 4. Children's Privacy

DoubleTake does not knowingly collect or solicit any personal information from children under 13.

---

### 5. Contact & Questions

If you have questions about this Privacy Policy or DoubleTake, you may open an issue on GitHub:
* **Repository:** [https://github.com/SaidimM/DoubleTake](https://github.com/SaidimM/DoubleTake)
* **Developer Contact:** [https://github.com/SaidimM](https://github.com/SaidimM)
