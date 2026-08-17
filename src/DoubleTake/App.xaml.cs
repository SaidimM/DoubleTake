using Microsoft.UI.Xaml;
using System;

namespace QuickTranslator
{
    public partial class App : Application
    {
        private MainWindow m_window;
        private QuickPopup m_popup;

        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += (s, e) =>
            {
                try { System.IO.File.WriteAllText(@"C:\Users\Saidi\IdeaProjects\DoubleTake\crash.log", e.Exception.ToString()); }
                catch { }
                e.Handled = true;
            };
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                m_window = new MainWindow();
                m_window.Activate();

                // Setup Tray Service
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(m_window);
                TrayService.Initialize(hWnd);
                TrayService.OnOpenRequested += () =>
                {
                    m_window.DispatcherQueue.TryEnqueue(() =>
                    {
                        m_window.ShowAndActivate();
                    });
                };
                TrayService.OnHistoryRequested += () =>
                {
                    m_window.DispatcherQueue.TryEnqueue(() =>
                    {
                        m_window.NavigateToHistory();
                        m_window.ShowAndActivate();
                    });
                };
                TrayService.OnExitRequested += () =>
                {
                    TrayService.RemoveTrayIcon();
                    GlobalHotkey.Stop();
                    this.Exit();
                };

                // Pre-create popup
                m_popup = new QuickPopup();

                GlobalHotkey.DoubleCtrlPressed += OnDoubleCtrl;
                GlobalHotkey.Start();
            }
            catch (Exception ex)
            {
                try { System.IO.File.WriteAllText(@"C:\Users\Saidi\IdeaProjects\DoubleTake\crash.log", ex.ToString()); }
                catch { }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Double Ctrl → Translate selected text (with Game/Exclusion check)
        // ════════════════════════════════════════════════════════════════════
        private void OnDoubleCtrl(object sender, EventArgs e)
        {
            // Check if active app or game is blacklisted / fullscreen
            if (ExclusionService.IsActiveAppExcluded()) return;

            if (m_window == null) return;
            m_window.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    string text = await ClipboardHelper.GetSelectedTextAsync();
                    if (string.IsNullOrWhiteSpace(text)) return;

                    EnsurePopup();
                    m_popup.ShowAndTranslate(text);
                }
                catch (Exception ex)
                {
                    try { System.IO.File.WriteAllText(@"C:\Users\Saidi\IdeaProjects\DoubleTake\crash.log", ex.ToString()); } catch { }
                }
            });
        }

        private void EnsurePopup()
        {
            if (m_popup == null)
                m_popup = new QuickPopup();
        }
    }
}
