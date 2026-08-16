using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;

namespace QuickTranslator
{
    public sealed partial class QuickPopup : Window
    {
        private readonly TranslationService _translator = new TranslationService();
        private IntPtr _hWnd;

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_SHOWWINDOW = 0x0040;
        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        public QuickPopup()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
            _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        }

        private void PositionAndShow(int width, int height)
        {
            try
            {
                if (_hWnd == IntPtr.Zero)
                    _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

                GetCursorPos(out POINT pt);
                int x = Math.Max(20, pt.X - 40);
                int y = Math.Max(20, pt.Y + 20);
                SetWindowPos(_hWnd, HWND_TOPMOST, x, y, width, height, SWP_SHOWWINDOW);
                ShowWindow(_hWnd, SW_SHOW);
                SetForegroundWindow(_hWnd);
            }
            catch { }
        }

        public async void ShowAndTranslate(string text)
        {
            PositionAndShow(460, 270);

            SourceTextBlock.Text = text;
            TranslateStatusText.Text = $"{SettingsManager.Current.ActiveEngine} Engine";
            TranslatedTextBlock.Text = "Translating…";
            LoadingRing.IsActive = true;
            LoadingRing.Visibility = Visibility.Visible;

            try
            {
                string result = await _translator.TranslateAsync(text);
                TranslatedTextBlock.Text = result ?? "No translation available.";
            }
            catch (Exception ex)
            {
                TranslatedTextBlock.Text = $"Error: {ex.Message}";
            }
            finally
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TranslatedTextBlock.Text))
            {
                var pkg = new DataPackage();
                pkg.SetText(TranslatedTextBlock.Text);
                Clipboard.SetContent(pkg);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => HidePopup();

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
