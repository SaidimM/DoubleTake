using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
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
        private bool _isVisible = false;
        private CancellationTokenSource _cts;
        private string _lastSourceText = string.Empty;
        private bool _isTranslating = false;
        private POINT _anchorPoint;
        private bool _hasAnchor = false;
        private DateTime _lastShownTime = DateTime.MinValue;

        public event Action<string, string, string> OnOpenInWorkspaceRequested;

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

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

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
        const uint WM_NCLBUTTONDOWN = 0x00A1;
        const int HTCAPTION = 0x0002;
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
                QuickTranslator.Helpers.DebugLog.Write($"QuickPopup.Activated: state={args.WindowActivationState}, isPinned={_isPinned}, isVisible={_isVisible}, elapsedSinceShown={(DateTime.UtcNow - _lastShownTime).TotalMilliseconds:F0}ms");
                if (args.WindowActivationState == WindowActivationState.Deactivated)
                {
                    if (!_isPinned && _isVisible)
                    {
                        // Ignore immediate deactivation within 250ms of display (grace period for window show transition)
                        if ((DateTime.UtcNow - _lastShownTime).TotalMilliseconds < 250)
                        {
                            QuickTranslator.Helpers.DebugLog.Write("QuickPopup.Activated: Deactivation ignored due to grace period.");
                            return;
                        }

                        QuickTranslator.Helpers.DebugLog.Write("QuickPopup.Activated: Hiding popup due to deactivation.");
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

        private void PopulateTargetLanguages(string selectCode)
        {
            TargetLangCombo.SelectionChanged -= TargetLangCombo_SelectionChanged;
            TargetLangCombo.Items.Clear();

            var recentList = SettingsManager.Current.RecentLanguages ?? new System.Collections.Generic.List<string>();
            int selectedIndex = 0;
            int currentIndex = 0;

            foreach (var code in recentList)
            {
                var match = SettingsManager.LanguageCatalog.FirstOrDefault(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
                string name = match.DisplayName ?? code;
                string shortName = name.Contains('·') ? name.Split('·')[1].Trim() : name;

                var item = new ComboBoxItem
                {
                    Content = $"Auto ➔ {shortName}",
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
            TargetLangCombo.SelectionChanged += TargetLangCombo_SelectionChanged;
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
                if (!_isVisible) return;

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
                double dipWidth = 430;
                if (maxChars > 160) dipWidth = 560;
                else if (maxChars > 50) dipWidth = 490;
                else dipWidth = 430;

                // 2. Measure actual XAML layout height accurately (generous Zero-Scroll First ceiling)
                PopupRootCard.Width = dipWidth;
                PopupRootCard.Measure(new Windows.Foundation.Size(dipWidth, double.PositiveInfinity));
                double dipHeight = Math.Clamp(PopupRootCard.DesiredSize.Height + 26, 155, 520);

                // 3. Convert DIPs to physical device pixels for SetWindowPos (including window shadow/outer padding)
                int physWidth = (int)Math.Ceiling((dipWidth + 14) * dpi);
                int physHeight = (int)Math.Ceiling((dipHeight + 14) * dpi);

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
            _isVisible = true;
            _lastShownTime = DateTime.UtcNow;
            _lastSourceText = text;
            _hasAnchor = false; // Capture fresh cursor anchor point
            GetCursorPos(out _anchorPoint);
            _hasAnchor = true;

            // Cancel any ongoing translation request
            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
            }
            catch { }
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            SyncActiveEngineCombo();

            string defaultTarget = ResolveTargetCodeForText(text);
            PopulateTargetLanguages(defaultTarget);

            string initialEngine = (EngineQuickCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? SettingsManager.Current.ActiveEngine;
            TranslateStatusText.Text = $"{initialEngine} Engine · Translating…";

            SourceTextBlock.Text = text;
            TranslatedTextBlock.Text = "Translating…";
            LoadingRing.IsActive = true;
            LoadingRing.Visibility = Visibility.Visible;
            StatusDot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x38, 0xBD, 0xF8));

            ContentScrollViewer?.ChangeView(null, 0, null, true);

            // Immediate initial display near anchor
            ApplyElasticSizingAndPosition();

            await ReTranslateAsync(ct);
        }

        private string ResolveTargetCodeForText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "zh-CN";

            bool isChinese = Regex.IsMatch(text, @"[\u4e00-\u9fa5]");
            bool isJapanese = Regex.IsMatch(text, @"[\u3040-\u30ff]");
            bool isKorean = Regex.IsMatch(text, @"[\uac00-\ud7af]");

            // If input text is Chinese/Japanese/Korean, target English
            if (isChinese || isJapanese || isKorean)
            {
                return "en";
            }
            return "zh-CN";
        }

        private async Task ReTranslateAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_lastSourceText) || _isTranslating || !_isVisible) return;

            _isTranslating = true;
            LoadingRing.IsActive = true;
            LoadingRing.Visibility = Visibility.Visible;
            StatusDot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x38, 0xBD, 0xF8));

            string targetLang = (TargetLangCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "zh-CN";
            string engine = (EngineQuickCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? SettingsManager.Current.ActiveEngine;
            TranslateStatusText.Text = $"{engine} Engine · Translating…";

            var sw = Stopwatch.StartNew();
            try
            {
                string result = await _translator.TranslateAsync(_lastSourceText, targetLang, null, engine, ct);
                sw.Stop();

                if (ct.IsCancellationRequested || !_isVisible)
                {
                    QuickTranslator.Helpers.DebugLog.Write("QuickPopup.ReTranslateAsync: Request cancelled or popup dismissed. Suppressing UI update.");
                    return;
                }

                TranslatedTextBlock.Text = result ?? "No translation available.";
                TranslateStatusText.Text = $"{engine} Engine · {sw.ElapsedMilliseconds}ms";
                StatusDot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x4A, 0xDE, 0x80));
            }
            catch (OperationCanceledException)
            {
                QuickTranslator.Helpers.DebugLog.Write("QuickPopup.ReTranslateAsync: Operation canceled.");
                return;
            }
            catch (Exception ex)
            {
                sw.Stop();
                if (ct.IsCancellationRequested || !_isVisible)
                {
                    QuickTranslator.Helpers.DebugLog.Write($"QuickPopup.ReTranslateAsync: Errored after dismiss. Suppressing popup re-open. Error={ex.Message}");
                    return;
                }

                TranslatedTextBlock.Text = $"Error: {ex.Message}";
                TranslateStatusText.Text = $"{engine} Engine · Error";
                StatusDot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44));
            }
            finally
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
                _isTranslating = false;

                // Adjust elastic size only if popup is still visible and request was not cancelled
                if (_isVisible && !ct.IsCancellationRequested)
                {
                    ApplyElasticSizingAndPosition();
                    AutoScrollToTranslation();
                }
            }
        }

        private async void AutoScrollToTranslation()
        {
            try
            {
                await Task.Delay(50);
                if (ContentScrollViewer != null && ContentScrollViewer.ScrollableHeight > 0)
                {
                    TranslatedTextBlock.StartBringIntoView(new BringIntoViewOptions
                    {
                        AnimationDesired = true,
                        VerticalAlignmentRatio = 0.0f
                    });
                }
            }
            catch { }
        }

        // ── Event Handlers ───────────────────────────────────────────────────
        private async void TargetLangCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.Content == null || _isTranslating || !_isVisible) return;

            if (TargetLangCombo.SelectedItem is ComboBoxItem item && item.Tag is string langCode)
            {
                SettingsManager.RecordLanguageUsed(langCode);
                try { _cts?.Cancel(); _cts?.Dispose(); } catch { }
                _cts = new CancellationTokenSource();
                await ReTranslateAsync(_cts.Token);
            }
        }

        private async void EngineQuickCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.Content == null || _isTranslating || !_isVisible) return;
            if (EngineQuickCombo.SelectedItem is ComboBoxItem item && item.Tag is string engine)
            {
                SettingsManager.Current.ActiveEngine = engine;
                SettingsManager.SaveSettings();
                try { _cts?.Cancel(); _cts?.Dispose(); } catch { }
                _cts = new CancellationTokenSource();
                await ReTranslateAsync(_cts.Token);
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

        private bool _isDragging = false;
        private POINT _dragStartCursor;
        private RECT _dragStartWindowRect;

        private void HeaderGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var props = e.GetCurrentPoint(sender as UIElement).Properties;
            if (props.IsLeftButtonPressed)
            {
                _isDragging = true;
                GetCursorPos(out _dragStartCursor);
                GetWindowRect(_hWnd, out _dragStartWindowRect);
                HeaderGrid.CapturePointer(e.Pointer);
                e.Handled = true;
            }
        }

        private void HeaderGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                GetCursorPos(out POINT currentCursor);
                int deltaX = currentCursor.X - _dragStartCursor.X;
                int deltaY = currentCursor.Y - _dragStartCursor.Y;

                int newX = _dragStartWindowRect.Left + deltaX;
                int newY = _dragStartWindowRect.Top + deltaY;

                const uint SWP_NOSIZE = 0x0001;
                const uint SWP_NOZORDER = 0x0004;
                const uint SWP_NOACTIVATE = 0x0010;

                SetWindowPos(_hWnd, IntPtr.Zero, newX, newY, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

                double dpi = GetDpiScale();
                _anchorPoint = new POINT
                {
                    X = newX + (int)(30 * dpi),
                    Y = newY - (int)(20 * dpi)
                };
                _hasAnchor = true;
                e.Handled = true;
            }
        }

        private void HeaderGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                HeaderGrid.ReleasePointerCapture(e.Pointer);
                e.Handled = true;
            }
        }

        private void HeaderGrid_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _isDragging = false;
        }

        private void OpenWorkspaceButton_Click(object sender, RoutedEventArgs e)
        {
            string source = SourceTextBlock.Text;
            string target = TranslatedTextBlock.Text;
            string targetLang = (TargetLangCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "zh-CN";

            HidePopup();
            OnOpenInWorkspaceRequested?.Invoke(source, target, targetLang);
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
                _isVisible = false;
                _hasAnchor = false;

                try
                {
                    _cts?.Cancel();
                    _cts?.Dispose();
                    _cts = null;
                }
                catch { }

                if (_hWnd == IntPtr.Zero)
                    _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                ShowWindow(_hWnd, SW_HIDE);
            }
            catch { }
        }
    }
}
