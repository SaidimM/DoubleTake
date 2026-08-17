using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Windows.Security.Credentials;

namespace QuickTranslator
{
    public class AppSettings
    {
        public string ActiveEngine { get; set; } = "Google";
        public string FallbackEngine { get; set; } = "Bing";
        public bool AutoFallback { get; set; } = true;
        public int SpeedWindowMs { get; set; } = 550;
        public string DefaultSourceLang { get; set; } = "auto";
        public string DefaultTargetLang { get; set; } = "zh-CN";
        public string SecondaryTargetLang { get; set; } = "en";
        public bool SmartBiDirectional { get; set; } = true;

        // Dynamic Language MRU order
        public List<string> RecentLanguages { get; set; } = new List<string>
        {
            "zh-CN",
            "en",
            "ja",
            "es",
            "fr",
            "de",
            "ko",
            "ru"
        };

        public string PopupPosition { get; set; } = "Near Cursor";
        public bool AutoDismiss { get; set; } = false;
        public bool LaunchAtStartup { get; set; } = true;
        public bool ShowTrayIcon { get; set; } = true;
        public bool IsHotkeyPaused { get; set; } = false;
        public bool SaveHistoryAcrossSessions { get; set; } = true;

        // Gaming / App Exclusion
        public bool DisableInFullscreen { get; set; } = true;
        public List<string> ExcludedProcesses { get; set; } = new List<string>
        {
            "cs2.exe",
            "VALORANT-Win64-Shipping.exe",
            "LeagueClient.exe",
            "Overwatch.exe",
            "dota2.exe"
        };

        // DeepL
        public bool DeepLIsPro { get; set; } = false;

        // In-memory key caching (hydrated from PasswordVault)
        public string DeepLApiKey { get; set; } = string.Empty;
        public string BaiduAppId { get; set; } = string.Empty;
        public string BaiduSecretKey { get; set; } = string.Empty;
        public string PapagoClientId { get; set; } = string.Empty;
        public string PapagoClientSecret { get; set; } = string.Empty;
        public string YandexApiKey { get; set; } = string.Empty;
        public string YoudaoAppKey { get; set; } = string.Empty;
        public string YoudaoAppSecret { get; set; } = string.Empty;
    }

    public static class SettingsManager
    {
        private static readonly string ConfigFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".doubletake");
        private static readonly string ConfigPath = Path.Combine(ConfigFolder, "settings.json");
        private const string VaultResource = "DoubleTake_Credentials";

        public static readonly (string Code, string DisplayName)[] LanguageCatalog = new[]
        {
            ("zh-CN", "Chinese (Simplified) · 中文"),
            ("en", "English · 英语"),
            ("ja", "Japanese · 日本語"),
            ("es", "Spanish · Español"),
            ("fr", "French · Français"),
            ("de", "German · Deutsch"),
            ("ko", "Korean · 한국어"),
            ("ru", "Russian · Русский")
        };

        private static AppSettings _current;

        public static AppSettings Current
        {
            get
            {
                if (_current == null)
                    LoadSettings();
                return _current;
            }
        }

        public static void RecordLanguageUsed(string langCode)
        {
            if (string.IsNullOrWhiteSpace(langCode)) return;

            var list = Current.RecentLanguages ?? new List<string>();
            list.RemoveAll(x => x.Equals(langCode, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, langCode);

            // Ensure all known languages exist in list
            foreach (var item in LanguageCatalog)
            {
                if (!list.Contains(item.Code, StringComparer.OrdinalIgnoreCase))
                    list.Add(item.Code);
            }

            Current.RecentLanguages = list;
            SaveSettings();
        }

        public static void LoadSettings()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    _current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    _current = new AppSettings();
                }

                // Ensure recent languages list is initialized
                if (_current.RecentLanguages == null || _current.RecentLanguages.Count == 0)
                {
                    _current.RecentLanguages = LanguageCatalog.Select(x => x.Code).ToList();
                }

                // Load secrets from PasswordVault
                LoadVaultSecret("DeepL_ApiKey", v => _current.DeepLApiKey = v);
                LoadVaultSecret("Baidu_AppId", v => _current.BaiduAppId = v);
                LoadVaultSecret("Baidu_SecretKey", v => _current.BaiduSecretKey = v);
                LoadVaultSecret("Papago_ClientId", v => _current.PapagoClientId = v);
                LoadVaultSecret("Papago_ClientSecret", v => _current.PapagoClientSecret = v);
                LoadVaultSecret("Yandex_ApiKey", v => _current.YandexApiKey = v);
                LoadVaultSecret("Youdao_AppKey", v => _current.YoudaoAppKey = v);
                LoadVaultSecret("Youdao_AppSecret", v => _current.YoudaoAppSecret = v);
            }
            catch
            {
                _current = new AppSettings();
            }
        }

        public static void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(ConfigFolder);
                var clone = new AppSettings
                {
                    ActiveEngine = _current.ActiveEngine,
                    FallbackEngine = _current.FallbackEngine,
                    AutoFallback = _current.AutoFallback,
                    SpeedWindowMs = _current.SpeedWindowMs,
                    DefaultSourceLang = _current.DefaultSourceLang,
                    DefaultTargetLang = _current.DefaultTargetLang,
                    SecondaryTargetLang = _current.SecondaryTargetLang,
                    SmartBiDirectional = _current.SmartBiDirectional,
                    RecentLanguages = _current.RecentLanguages ?? new List<string>(),
                    PopupPosition = _current.PopupPosition,
                    AutoDismiss = _current.AutoDismiss,
                    LaunchAtStartup = _current.LaunchAtStartup,
                    ShowTrayIcon = _current.ShowTrayIcon,
                    IsHotkeyPaused = _current.IsHotkeyPaused,
                    SaveHistoryAcrossSessions = _current.SaveHistoryAcrossSessions,
                    DisableInFullscreen = _current.DisableInFullscreen,
                    ExcludedProcesses = _current.ExcludedProcesses ?? new List<string>(),
                    DeepLIsPro = _current.DeepLIsPro
                };

                string json = JsonSerializer.Serialize(clone, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);

                // Save secrets into PasswordVault
                SaveVaultSecret("DeepL_ApiKey", _current.DeepLApiKey);
                SaveVaultSecret("Baidu_AppId", _current.BaiduAppId);
                SaveVaultSecret("Baidu_SecretKey", _current.BaiduSecretKey);
                SaveVaultSecret("Papago_ClientId", _current.PapagoClientId);
                SaveVaultSecret("Papago_ClientSecret", _current.PapagoClientSecret);
                SaveVaultSecret("Yandex_ApiKey", _current.YandexApiKey);
                SaveVaultSecret("Youdao_AppKey", _current.YoudaoAppKey);
                SaveVaultSecret("Youdao_AppSecret", _current.YoudaoAppSecret);
            }
            catch { }
        }

        private static void LoadVaultSecret(string key, Action<string> assign)
        {
            try
            {
                var vault = new PasswordVault();
                var cred = vault.Retrieve(VaultResource, key);
                if (cred != null)
                {
                    cred.RetrievePassword();
                    assign(cred.Password);
                }
            }
            catch { }
        }

        private static void SaveVaultSecret(string key, string value)
        {
            try
            {
                var vault = new PasswordVault();
                try
                {
                    var existing = vault.Retrieve(VaultResource, key);
                    if (existing != null) vault.Remove(existing);
                }
                catch { }

                if (!string.IsNullOrWhiteSpace(value))
                {
                    vault.Add(new PasswordCredential(VaultResource, key, value));
                }
            }
            catch { }
        }
    }
}
