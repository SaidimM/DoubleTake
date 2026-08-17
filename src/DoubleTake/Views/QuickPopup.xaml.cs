using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
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

            // Set default engine selection in popup
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

        // ── Safe Multi-Monitor Screen Clamping ──────────────────────────────
        private void PositionAndShow(int width, int height)
        {
            try
            {
                if (_hWnd == IntPtr.Zero)
                    _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

                GetCursorPos(out POINT pt);

                // Query monitor bounds
                IntPtr hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
                MONITORINFO mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                GetMonitorInfo(hMonitor, ref mi);

                int x = pt.X - 40;
                int y = pt.Y + 20;

                // Screen boundary clamping
                if (x + width > mi.rcWork.Right - 12)
                    x = mi.rcWork.Right - width - 12;
                if (x < mi.rcWork.Left + 12)
                    x = mi.rcWork.Left + 12;

                if (y + height > mi.rcWork.Bottom - 12)
                    y = pt.Y - height - 16; // Flip above cursor if bottom edge exceeded
                if (y < mi.rcWork.Top + 12)
                    y = mi.rcWork.Top + 12;

                SetWindowPos(_hWnd, HWND_TOPMOST, x, y, width, height, SWP_SHOWWINDOW);
                ShowWindow(_hWnd, SW_SHOW);
                SetForegroundWindow(_hWnd);
            }
            catch { }
        }

        // ── Main Translation Action ──────────────────────────────────────────
        public async void ShowAndTranslate(string text)
        {
            _lastSourceText = text;
            SyncActiveEngineCombo();
            PositionAndShow(480, 280);

            SourceTextBlock.Text = text;
            await ReTranslateAsync();
        }

        private async Task ReTranslateAsync()
        {
            if (string.IsNullOrWhiteSpace(_lastSourceText) || _isTranslating) return;

            _isTranslating = true;
            TranslatedTextBlock.Text = "Translating…";
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
            }
        }

        // ── Event Handlers ───────────────────────────────────────────────────
        private async void TargetLangCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.Content == null) return;
            await ReTranslateAsync();
        }

        private async void EngineQuickCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.Content == null) return;
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
                if (_hWnd == IntPtr.Zero)
                    _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                ShowWindow(_hWnd, SW_HIDE);
            }
            catch { }
        }
    }
}
