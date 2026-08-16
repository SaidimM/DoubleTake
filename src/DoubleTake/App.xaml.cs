using Microsoft.UI.Xaml;
using System;

namespace QuickTranslator
{
    public partial class App : Application
    {
        private Window m_window;
        private QuickPopup m_popup;

        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += (s, e) =>
            {
                try { System.IO.File.WriteAllText(@"C:\Users\Saidi\IdeaProjects\QuickTranslator\crash.log", e.Exception.ToString()); }
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

                // Pre-create the popup overlay once (avoids cold-start delay)
                m_popup = new QuickPopup();

                GlobalHotkey.DoubleCtrlPressed += OnDoubleCtrl;
                GlobalHotkey.Start();
            }
            catch (Exception ex)
            {
                try { System.IO.File.WriteAllText(@"C:\Users\Saidi\IdeaProjects\QuickTranslator\crash.log", ex.ToString()); }
                catch { }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Double Ctrl → Translate selected text
        // ════════════════════════════════════════════════════════════════════
        private void OnDoubleCtrl(object sender, EventArgs e)
        {
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
                    try { System.IO.File.WriteAllText(@"C:\Users\Saidi\IdeaProjects\QuickTranslator\crash.log", ex.ToString()); } catch { }
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
