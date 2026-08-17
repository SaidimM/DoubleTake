using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Xaml;

namespace QuickTranslator
{
    public partial class App : Application
    {
        private MainWindow m_window;
        private QuickPopup m_popup;
        private static Mutex _singleInstanceMutex;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int SW_RESTORE = 9;
        private const uint HWND_BROADCAST = 0xFFFF;

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
                // Single Instance Check
                _singleInstanceMutex = new Mutex(true, "DoubleTake_SingleInstance_App_Mutex", out bool createdNew);
                if (!createdNew)
                {
                    uint showMsg = RegisterWindowMessage("DoubleTake_ShowSingleInstance_Msg");
                    PostMessage((IntPtr)HWND_BROADCAST, showMsg, IntPtr.Zero, IntPtr.Zero);

                    IntPtr existingHwnd = FindWindow(null, "DoubleTake");
                    if (existingHwnd != IntPtr.Zero)
                    {
                        ShowWindow(existingHwnd, SW_RESTORE);
                        SetForegroundWindow(existingHwnd);
                    }

                    Environment.Exit(0);
                    return;
                }

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
                    m_window.DispatcherQueue.TryEnqueue(() =>
                    {
                        TrayService.RemoveTrayIcon();
                        GlobalHotkey.Stop();
                        Environment.Exit(0);
                    });
                };

                // Pre-create popup
                m_popup = new QuickPopup();
                m_popup.OnOpenInWorkspaceRequested += (source, target, targetLang) =>
                {
                    m_window.DispatcherQueue.TryEnqueue(() =>
                    {
                        m_window.PopulateAndShowWorkspace(source, target, targetLang);
                    });
                };

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
            QuickTranslator.Helpers.DebugLog.Write("App.OnDoubleCtrl: Received hotkey event!");

            // Check if active app or game is blacklisted / fullscreen
            if (ExclusionService.IsActiveAppExcluded())
            {
                QuickTranslator.Helpers.DebugLog.Write("App.OnDoubleCtrl: ExclusionService rejected hotkey.");
                return;
            }

            if (m_window == null)
            {
                QuickTranslator.Helpers.DebugLog.Write("App.OnDoubleCtrl: m_window is NULL!");
                return;
            }

            m_window.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    QuickTranslator.Helpers.DebugLog.Write("App.OnDoubleCtrl: Enqueued task running on UI thread.");
                    string text = await ClipboardHelper.GetSelectedTextAsync();
                    QuickTranslator.Helpers.DebugLog.Write($"App.OnDoubleCtrl: Retrieved selected text: length={text?.Length ?? 0}");

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        QuickTranslator.Helpers.DebugLog.Write("App.OnDoubleCtrl: Selected text is empty. Suppressing popup.");
                        return;
                    }

                    EnsurePopup();
                    QuickTranslator.Helpers.DebugLog.Write("App.OnDoubleCtrl: Calling m_popup.ShowAndTranslate()");
                    m_popup.ShowAndTranslate(text);
                }
                catch (Exception ex)
                {
                    QuickTranslator.Helpers.DebugLog.Write($"App.OnDoubleCtrl: Exception: {ex}");
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
