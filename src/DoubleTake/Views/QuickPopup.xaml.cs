using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace QuickTranslator
{
    public sealed partial class QuickPopup : Window
    {
        private readonly TranslationService _translator = new TranslationService();
        private IntPtr _hWnd;
        private bool _isPinned = false;
        private string _lastSourceText = string.Empty;
        private bool _isTranslating = false;
        private POINT _anchorPoint;
        private bool _hasAnchor = false;

        // ── Win32 Interop ──────────────────────────────────────────────────
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        static extern uint GetDpiForWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_SHOWWINDOW = 0x0040;
        const uint SWP_NOACTIVATE = 0x0010;
        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        const uint MONITOR_DEFAULTTONEAREST = 2;

        public QuickPopup()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SystemBackdrop = new MicaBackdrop();

            _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            ConfigurePresenter();

            // Auto-dismiss on click outside (window deactivation) unless pinned
            this.Activated += (sender, args) =>
            {
                if (args.WindowActivationState == WindowActivationState.Deactivated)
                {
                    if (!_isPinned)
                    {
                        HidePopup();
                    }
                }
            };

            SyncActiveEngineCombo();
        }

        private void ConfigurePresenter()
        {
            try
            {
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hWnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                if (appWindow?.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    presenter.IsResizable = false;
                    presenter.IsMaximizable = false;
                    presenter.IsMinimizable = false;
                    presenter.SetBorderAndTitleBar(false, false);
                }
            }
            catch { }
        }

        private void SyncActiveEngineCombo()
        {
            string active = SettingsManager.Current.ActiveEngine;
            for (int i = 0; i < EngineQuickCombo.Items.Count; i++)
            {
                if (EngineQuickCombo.Items[i] is ComboBoxItem item && item.Tag as string == active)
                {
                    EngineQuickCombo.SelectedIndex = i;
                    break;
                }
            }
        }

        private double GetDpiScale()
        {
            if (_hWnd == IntPtr.Zero) return 1.0;
            uint dpi = GetDpiForWindow(_hWnd);
            return (dpi > 0) ? (dpi / 96.0) : 1.0;
        }

        // ── Content-Elastic Dimensions & Screen-Safe Positioning ───────────
        private void ApplyElasticSizingAndPosition()
        {
            try
            {
                if (_hWnd == IntPtr.Zero)
                    _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

                if (!_hasAnchor)
                {
                    GetCursorPos(out _anchorPoint);
                    _hasAnchor = true;
                }

                double dpi = GetDpiScale();

                // 1. Determine natural DIP width based on text volume
                int maxChars = Math.Max(_lastSourceText?.Length ?? 0, TranslatedTextBlock.Text?.Length ?? 0);
                double dipWidth = 400;
                if (maxChars > 120) dipWidth = 520;
                else if (maxChars > 40) dipWidth = 460;
                else dipWidth = 400;

                // 2. Measure actual XAML layout height accurately
                PopupRootCard.Width = dipWidth;
                PopupRootCard.Measure(new Windows.Foundation.Size(dipWidth, double.PositiveInfinity));
                double dipHeight = Math.Clamp(PopupRootCard.DesiredSize.Height + 16, 155, 420);

                // 3. Convert DIPs to physical device pixels for SetWindowPos
                int physWidth = (int)Math.Round(dipWidth * dpi);
                int physHeight = (int)Math.Round(dipHeight * dpi);

                // 4. Query monitor work area bounds (in physical pixels)
                IntPtr hMonitor = MonitorFromPoint(_anchorPoint, MONITOR_DEFAULTTONEAREST);
                MONITORINFO mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                GetMonitorInfo(hMonitor, ref mi);

                int x = _anchorPoint.X - (int)(30 * dpi);
                int y = _anchorPoint.Y + (int)(20 * dpi);

                // Clamp to monitor work area boundaries
                if (x + physWidth > mi.rcWork.Right - 16)
                    x = mi.rcWork.Right - physWidth - 16;
                if (x < mi.rcWork.Left + 16)
                    x = mi.rcWork.Left + 16;

                if (y + physHeight > mi.rcWork.Bottom - 16)
                    y = _anchorPoint.Y - physHeight - (int)(16 * dpi); // Flip above cursor if bottom edge exceeded
                if (y < mi.rcWork.Top + 16)
                    y = mi.rcWork.Top + 16;

                SetWindowPos(_hWnd, HWND_TOPMOST, x, y, physWidth, physHeight, SWP_SHOWWINDOW);
                ShowWindow(_hWnd, SW_SHOW);
                SetForegroundWindow(_hWnd);
            }
            catch { }
        }

        // ── Main Translation Action ──────────────────────────────────────────
        public async void ShowAndTranslate(string text)
        {
            _lastSourceText = text;
            _hasAnchor = false; // Capture fresh cursor anchor point
            GetCursorPos(out _anchorPoint);
            _hasAnchor = true;

            SyncActiveEngineCombo();
            AutoSelectAppropriateTargetLanguage(text);

            SourceTextBlock.Text = text;
            TranslatedTextBlock.Text = "Translating…";
            LoadingRing.IsActive = true;
            LoadingRing.Visibility = Visibility.Visible;
            StatusDot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x38, 0xBD, 0xF8));

            // Immediate initial display near anchor
            ApplyElasticSizingAndPosition();

            await ReTranslateAsync();
        }

        private void AutoSelectAppropriateTargetLanguage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            bool isChinese = Regex.IsMatch(text, @"[\u4e00-\u9fa5]");
            bool isJapanese = Regex.IsMatch(text, @"[\u3040-\u30ff]");
            bool isKorean = Regex.IsMatch(text, @"[\uac00-\ud7af]");

            // If input text is Chinese, select English (Index 1)
            // If input text is English / others, select Chinese (Index 0)
            if (isChinese || isJapanese || isKorean)
            {
                TargetLangCombo.SelectedIndex = 1; // Auto ➔ English
            }
            else
            {
                TargetLangCombo.SelectedIndex = 0; // Auto ➔ 中文
            }
        }

        private async Task ReTranslateAsync()
        {
            if (string.IsNullOrWhiteSpace(_lastSourceText) || _isTranslating) return;

            _isTranslating = true;
            LoadingRing.IsActive = true;
            LoadingRing.Visibility = Visibility.Visible;
            StatusDot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x38, 0xBD, 0xF8));

            string targetLang = (TargetLangCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "zh-CN";
            string engine = (EngineQuickCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? SettingsManager.Current.ActiveEngine;

            var sw = Stopwatch.StartNew();
            try
            {
                string result = await _translator.TranslateAsync(_lastSourceText, targetLang);
                sw.Stop();

                TranslatedTextBlock.Text = result ?? "No translation available.";
                TranslateStatusText.Text = $"{engine} Engine · {sw.ElapsedMilliseconds}ms";
                StatusDot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x4A, 0xDE, 0x80));
            }
            catch (Exception ex)
            {
                sw.Stop();
                TranslatedTextBlock.Text = $"Error: {ex.Message}";
                TranslateStatusText.Text = $"{engine} Engine · Error";
                StatusDot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44));
            }
            finally
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
                _isTranslating = false;

                // Adjust elastic size smoothly at the same anchored location
                ApplyElasticSizingAndPosition();
            }
        }

        // ── Event Handlers ───────────────────────────────────────────────────
        private async void TargetLangCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.Content == null || _isTranslating) return;
            await ReTranslateAsync();
        }

        private async void EngineQuickCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.Content == null || _isTranslating) return;
            if (EngineQuickCombo.SelectedItem is ComboBoxItem item && item.Tag is string engine)
            {
                SettingsManager.Current.ActiveEngine = engine;
                SettingsManager.SaveSettings();
                await ReTranslateAsync();
            }
        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            _isPinned = PinButton.IsChecked == true;
            PinIcon.Glyph = _isPinned ? "\uE840" : "\uE718";
            PinIcon.Foreground = _isPinned
                ? new SolidColorBrush(Color.FromArgb(0xFF, 0x38, 0xBD, 0xF8))
                : new SolidColorBrush(Color.FromArgb(0xFF, 0x94, 0xA3, 0xB8));
        }

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TranslatedTextBlock.Text))
            {
                var pkg = new DataPackage();
                pkg.SetText(TranslatedTextBlock.Text);
                Clipboard.SetContent(pkg);

                // Transient visual feedback: ✓ Copied!
                CopyIcon.Glyph = "\uE73E";
                CopyButtonText.Text = "Copied!";
                CopyButton.Background = new SolidColorBrush(Color.FromArgb(0x2A, 0x4A, 0xDE, 0x80));

                await Task.Delay(1400);

                CopyIcon.Glyph = "\uE8C8";
                CopyButtonText.Text = "Copy";
                CopyButton.ClearValue(Button.BackgroundProperty);
            }
        }

        private async void ReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            string translation = TranslatedTextBlock.Text;
            if (string.IsNullOrWhiteSpace(translation)) return;

            HidePopup();
            await Task.Delay(100);
            await ClipboardHelper.ReplaceSelectedTextAsync(translation);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => HidePopup();

        private void PopupRootCard_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                HidePopup();
                e.Handled = true;
            }
        }

        public void HidePopup()
        {
            try
            {
                _hasAnchor = false;
                if (_hWnd == IntPtr.Zero)
                    _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                ShowWindow(_hWnd, SW_HIDE);
            }
            catch { }
        }
    }
}
