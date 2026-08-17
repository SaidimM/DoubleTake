using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace QuickTranslator
{
    public sealed partial class MainWindow : Window
    {
        private readonly TranslationService _translator;
        private QuickPopup _popup;
        private bool _isInitializing = true;
        private ObservableCollection<string> _blacklistProcesses = new ObservableCollection<string>();

        public MainWindow()
        {
            this.InitializeComponent();
            _translator = new TranslationService();
            this.SystemBackdrop = new MicaBackdrop();
            this.ExtendsContentIntoTitleBar = true;

            // Setup minimize to tray on closing
            this.AppWindow.Closing += (s, e) =>
            {
                if (MinimizeToTrayToggle.IsOn)
                {
                    e.Cancel = true;
                    this.AppWindow.Hide();
                }
            };

            LoadSettingsIntoUI();
            RefreshHistoryView();
        }

        public void ShowAndActivate()
        {
            this.AppWindow.Show();
            this.Activate();
        }

        public void NavigateToHistory()
        {
            foreach (var item in NavView.MenuItems)
            {
                if (item is NavigationViewItem navItem && navItem.Tag as string == "History")
                {
                    NavView.SelectedItem = navItem;
                    break;
                }
            }
            TranslateView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Collapsed;
            HistoryView.Visibility = Visibility.Visible;
            RefreshHistoryView();
        }

        private async void LoadSettingsIntoUI()
        {
            var config = SettingsManager.Current;

            // Engine Selector
            for (int i = 0; i < EngineSelectorCombo.Items.Count; i++)
            {
                if (EngineSelectorCombo.Items[i] is ComboBoxItem item && item.Tag as string == config.ActiveEngine)
                {
                    EngineSelectorCombo.SelectedIndex = i;
                    break;
                }
            }

            // Hydrate Key Inputs
            DeepLApiKeyInput.Password = config.DeepLApiKey ?? string.Empty;
            if (config.DeepLIsPro) DeepLProRadio.IsChecked = true; else DeepLFreeRadio.IsChecked = true;

            BaiduAppIdInput.Text = config.BaiduAppId ?? string.Empty;
            BaiduSecretKeyInput.Password = config.BaiduSecretKey ?? string.Empty;

            PapagoClientIdInput.Text = config.PapagoClientId ?? string.Empty;
            PapagoClientSecretInput.Password = config.PapagoClientSecret ?? string.Empty;

            YandexApiKeyInput.Password = config.YandexApiKey ?? string.Empty;

            YoudaoAppKeyInput.Text = config.YoudaoAppKey ?? string.Empty;
            YoudaoAppSecretInput.Password = config.YoudaoAppSecret ?? string.Empty;

            AutoFallbackToggle.IsOn = config.AutoFallback;
            SpeedSlider.Value = config.SpeedWindowMs;
            SmartBiDirectionalToggle.IsOn = config.SmartBiDirectional;
            FullscreenExclusionToggle.IsOn = config.DisableInFullscreen;

            // Startup state
            bool isStartup = await StartupService.IsStartupEnabledAsync();
            StartupToggle.IsOn = isStartup;

            // Blacklist
            _blacklistProcesses = new ObservableCollection<string>(config.ExcludedProcesses ?? new List<string>());
            BlacklistItemsControl.ItemsSource = _blacklistProcesses;

            PopulateWorkspaceTargetLanguages(config.DefaultTargetLang);

            UpdateEngineDrawer(config.ActiveEngine);
            UpdateProviderStatusBadge(config.ActiveEngine);

            _isInitializing = false;
        }

        private void PopulateWorkspaceTargetLanguages(string selectCode = "zh-CN")
        {
            TargetLangCombo.Items.Clear();

            var recentList = SettingsManager.Current.RecentLanguages ?? new List<string>();
            int selectedIndex = 0;
            int currentIndex = 0;

            foreach (var code in recentList)
            {
                var match = SettingsManager.LanguageCatalog.FirstOrDefault(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
                string name = match.DisplayName ?? code;

                var item = new ComboBoxItem
                {
                    Content = name,
                    Tag = code
                };

                if (code.Equals(selectCode, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = currentIndex;
                }

                TargetLangCombo.Items.Add(item);
                currentIndex++;
            }

            TargetLangCombo.SelectedIndex = selectedIndex;
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                TranslateView.Visibility = Visibility.Collapsed;
                HistoryView.Visibility = Visibility.Collapsed;
                SettingsView.Visibility = Visibility.Visible;
                return;
            }

            var item = args.SelectedItem as NavigationViewItem;
            if (item == null) return;

            string tag = item.Tag as string;
            TranslateView.Visibility = tag == "Translate" ? Visibility.Visible : Visibility.Collapsed;
            HistoryView.Visibility = tag == "History" ? Visibility.Visible : Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Collapsed;

            if (tag == "History")
            {
                RefreshHistoryView();
            }
        }

        // ── History Screen Logic ─────────────────────────────────────────────
        private void RefreshHistoryView(string query = null)
        {
            var items = string.IsNullOrWhiteSpace(query) ? HistoryService.GetAll() : HistoryService.Search(query);
            HistoryListView.ItemsSource = items;
        }

        private void HistorySearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshHistoryView(HistorySearchBox.Text);
        }

        private void HistoryCopy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string text)
            {
                var pkg = new DataPackage();
                pkg.SetText(text);
                Clipboard.SetContent(pkg);
            }
        }

        private void HistoryDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string id)
            {
                HistoryService.DeleteEntry(id);
                RefreshHistoryView(HistorySearchBox.Text);
            }
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            HistoryService.ClearAll();
            RefreshHistoryView();
        }

        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string csv = HistoryService.ExportToCsv();
                string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                string file = Path.Combine(downloads, $"DoubleTake_History_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                File.WriteAllText(file, csv, System.Text.Encoding.UTF8);

                HistoryInfoBar.Title = "CSV Exported Successfully!";
                HistoryInfoBar.Message = $"Saved to: {file}";
                HistoryInfoBar.IsOpen = true;
            }
            catch (Exception ex)
            {
                HistoryInfoBar.Title = "Export Failed";
                HistoryInfoBar.Message = ex.Message;
                HistoryInfoBar.Severity = InfoBarSeverity.Error;
                HistoryInfoBar.IsOpen = true;
            }
        }

        private void ExportMdButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string md = HistoryService.ExportToMarkdown();
                string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                string file = Path.Combine(downloads, $"DoubleTake_History_{DateTime.Now:yyyyMMdd_HHmmss}.md");
                File.WriteAllText(file, md, System.Text.Encoding.UTF8);

                HistoryInfoBar.Title = "Markdown Exported Successfully!";
                HistoryInfoBar.Message = $"Saved to: {file}";
                HistoryInfoBar.IsOpen = true;
            }
            catch (Exception ex)
            {
                HistoryInfoBar.Title = "Export Failed";
                HistoryInfoBar.Message = ex.Message;
                HistoryInfoBar.Severity = InfoBarSeverity.Error;
                HistoryInfoBar.IsOpen = true;
            }
        }

        // ── Blacklist / Exclusion Logic ──────────────────────────────────────
        private void AddBlacklistProcess_Click(object sender, RoutedEventArgs e)
        {
            string name = NewBlacklistProcessInput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;

            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name += ".exe";

            if (!_blacklistProcesses.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                _blacklistProcesses.Add(name);
                SettingsManager.Current.ExcludedProcesses = _blacklistProcesses.ToList();
                SettingsManager.SaveSettings();
            }
            NewBlacklistProcessInput.Text = string.Empty;
        }

        private void RemoveBlacklistProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string name)
            {
                _blacklistProcesses.Remove(name);
                SettingsManager.Current.ExcludedProcesses = _blacklistProcesses.ToList();
                SettingsManager.SaveSettings();
            }
        }

        private void FullscreenExclusionToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitializing)
            {
                SettingsManager.Current.DisableInFullscreen = FullscreenExclusionToggle.IsOn;
                SettingsManager.SaveSettings();
            }
        }

        private void SmartBiDirectionalToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitializing)
            {
                SettingsManager.Current.SmartBiDirectional = SmartBiDirectionalToggle.IsOn;
                SettingsManager.SaveSettings();
            }
        }

        private async void StartupToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitializing)
            {
                bool success = await StartupService.SetStartupAsync(StartupToggle.IsOn);
                SettingsManager.Current.LaunchAtStartup = StartupToggle.IsOn;
                SettingsManager.SaveSettings();
            }
        }

        // ── Provider Drawer Logic ────────────────────────────────────────────
        private void EngineSelectorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EngineSelectorCombo.SelectedItem is ComboBoxItem item && item.Tag is string engine)
            {
                UpdateEngineDrawer(engine);
                UpdateProviderStatusBadge(engine);

                if (!_isInitializing)
                {
                    SettingsManager.Current.ActiveEngine = engine;
                    SettingsManager.SaveSettings();
                    ActiveEngineTag.Text = $"{engine} Engine";
                }
            }
        }

        private void UpdateEngineDrawer(string engine)
        {
            GoogleConfigPanel.Visibility = engine == "Google" ? Visibility.Visible : Visibility.Collapsed;
            BingConfigPanel.Visibility   = engine == "Bing"   ? Visibility.Visible : Visibility.Collapsed;
            DeepLConfigPanel.Visibility  = engine == "DeepL"  ? Visibility.Visible : Visibility.Collapsed;
            BaiduConfigPanel.Visibility  = engine == "Baidu"  ? Visibility.Visible : Visibility.Collapsed;
            PapagoConfigPanel.Visibility = engine == "Papago" ? Visibility.Visible : Visibility.Collapsed;
            YandexConfigPanel.Visibility = engine == "Yandex" ? Visibility.Visible : Visibility.Collapsed;
            YoudaoConfigPanel.Visibility = engine == "Youdao" ? Visibility.Visible : Visibility.Collapsed;

            TestResultStatusText.Text = "Click Test Connection to verify credentials & check latency.";
        }

        private void UpdateProviderStatusBadge(string engine)
        {
            var config = SettingsManager.Current;
            bool isBuiltIn = (engine == "Google" || engine == "Bing");
            bool hasKey = engine switch
            {
                "DeepL"  => !string.IsNullOrWhiteSpace(config.DeepLApiKey),
                "Baidu"  => !string.IsNullOrWhiteSpace(config.BaiduAppId) && !string.IsNullOrWhiteSpace(config.BaiduSecretKey),
                "Papago" => !string.IsNullOrWhiteSpace(config.PapagoClientId) && !string.IsNullOrWhiteSpace(config.PapagoClientSecret),
                "Yandex" => !string.IsNullOrWhiteSpace(config.YandexApiKey),
                "Youdao" => !string.IsNullOrWhiteSpace(config.YoudaoAppKey) && !string.IsNullOrWhiteSpace(config.YoudaoAppSecret),
                _ => true
            };

            if (isBuiltIn)
            {
                ProviderStatusBadge.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0x4A, 0xDE, 0x80));
                ProviderStatusBadgeText.Text = "✓ Ready (Built-in)";
                ProviderStatusBadgeText.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x4A, 0xDE, 0x80));
            }
            else if (hasKey)
            {
                ProviderStatusBadge.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0x38, 0xBD, 0xF8));
                ProviderStatusBadgeText.Text = "✓ Keys Configured";
                ProviderStatusBadgeText.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x38, 0xBD, 0xF8));
            }
            else
            {
                ProviderStatusBadge.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xF5, 0x9E, 0x0B));
                ProviderStatusBadgeText.Text = "⚠️ Keys Required";
                ProviderStatusBadgeText.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B));
            }
        }

        private void OnKeyInputChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            SyncKeysToConfig();
        }

        private void SyncKeysToConfig()
        {
            var config = SettingsManager.Current;
            config.DeepLApiKey = DeepLApiKeyInput.Password?.Trim();
            config.DeepLIsPro = DeepLProRadio.IsChecked == true;

            config.BaiduAppId = BaiduAppIdInput.Text?.Trim();
            config.BaiduSecretKey = BaiduSecretKeyInput.Password?.Trim();

            config.PapagoClientId = PapagoClientIdInput.Text?.Trim();
            config.PapagoClientSecret = PapagoClientSecretInput.Password?.Trim();

            config.YandexApiKey = YandexApiKeyInput.Password?.Trim();

            config.YoudaoAppKey = YoudaoAppKeyInput.Text?.Trim();
            config.YoudaoAppSecret = YoudaoAppSecretInput.Password?.Trim();
        }

        private void SaveKeysButton_Click(object sender, RoutedEventArgs e)
        {
            SyncKeysToConfig();
            SettingsManager.SaveSettings();
            string engine = (EngineSelectorCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "Google";
            UpdateProviderStatusBadge(engine);
            TestResultStatusText.Text = "✓ Credentials saved securely into Windows Credential Vault.";
        }

        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            SyncKeysToConfig();
            SettingsManager.SaveSettings();

            string engine = (EngineSelectorCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "Google";

            TestConnectionButton.IsEnabled = false;
            TestProgressRing.Visibility = Visibility.Visible;
            TestProgressRing.IsActive = true;
            TestResultStatusText.Text = $"Testing {engine} connection…";

            try
            {
                var result = await _translator.TestProviderAsync(engine);
                if (result.Success)
                {
                    TestResultStatusText.Text = $"✓ Connected ({result.LatencyMs}ms) · Output: \"{result.TranslatedText}\"";
                    UpdateProviderStatusBadge(engine);
                }
                else
                {
                    TestResultStatusText.Text = $"✕ Connection Failed: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                TestResultStatusText.Text = $"✕ Error: {ex.Message}";
            }
            finally
            {
                TestProgressRing.IsActive = false;
                TestProgressRing.Visibility = Visibility.Collapsed;
                TestConnectionButton.IsEnabled = true;
            }
        }

        private void DeepLRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitializing)
            {
                SettingsManager.Current.DeepLIsPro = DeepLProRadio.IsChecked == true;
                SettingsManager.SaveSettings();
            }
        }

        private void AutoFallbackToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitializing)
            {
                SettingsManager.Current.AutoFallback = AutoFallbackToggle.IsOn;
                SettingsManager.SaveSettings();
            }
        }

        private void SpeedSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitializing)
            {
                SettingsManager.Current.SpeedWindowMs = (int)e.NewValue;
                SettingsManager.SaveSettings();
            }
        }

        // ── Main Translation Screen Handlers ──────────────────────────────────
        private async void TranslateButton_Click(object sender, RoutedEventArgs e)
        {
            string source = SourceTextBox.Text;
            if (string.IsNullOrWhiteSpace(source)) return;

            LoadingRing.Visibility = Visibility.Visible;
            LoadingRing.IsActive = true;
            TranslateButton.IsEnabled = false;

            try
            {
                string sourceLang = (SourceLangCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "auto";
                string targetLang = (TargetLangCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "zh-CN";

                SettingsManager.RecordLanguageUsed(targetLang);

                string result = await _translator.TranslateAsync(source, targetLang, sourceLang);
                TargetTextBox.Text = result;
            }
            catch (Exception ex)
            {
                TargetTextBox.Text = $"Error: {ex.Message}";
            }
            finally
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
                TranslateButton.IsEnabled = true;
            }
        }

        private void SourceTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            int count = SourceTextBox.Text?.Length ?? 0;
            CharCountText.Text = $"{count} character{(count == 1 ? "" : "s")}";
        }

        private void SwapLangButton_Click(object sender, RoutedEventArgs e)
        {
            int sourceIdx = SourceLangCombo.SelectedIndex;
            int targetIdx = TargetLangCombo.SelectedIndex;

            if (sourceIdx > 0)
            {
                SourceLangCombo.SelectedIndex = targetIdx + 1;
                TargetLangCombo.SelectedIndex = Math.Max(0, sourceIdx - 1);
            }
        }

        private async void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            var dataPackageView = Clipboard.GetContent();
            if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                SourceTextBox.Text = await dataPackageView.GetTextAsync();
            }
        }

        private void ClearSourceButton_Click(object sender, RoutedEventArgs e)
        {
            SourceTextBox.Text = string.Empty;
            TargetTextBox.Text = string.Empty;
        }

        private void CopyTargetButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TargetTextBox.Text))
            {
                var package = new DataPackage();
                package.SetText(TargetTextBox.Text);
                Clipboard.SetContent(package);
            }
        }

        private void TestPopup_Click(object sender, RoutedEventArgs e)
        {
            _popup ??= new QuickPopup();
            _popup.ShowAndTranslate("Hello! This is a test of DoubleTake quick translation popup.");
        }
    }
}
