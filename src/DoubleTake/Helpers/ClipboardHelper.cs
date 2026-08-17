using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace QuickTranslator
{
    public static class ClipboardHelper
    {
        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("user32.dll")]
        static extern uint GetClipboardSequenceNumber();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GlobalUnlock(IntPtr hMem);

        private const uint CF_UNICODETEXT = 13;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_C = 0x43;
        private const byte VK_V = 0x56;

        public static string GetCurrentClipboardText()
        {
            string text = string.Empty;
            try
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    if (IsClipboardFormatAvailable(CF_UNICODETEXT))
                    {
                        IntPtr hGlobal = GetClipboardData(CF_UNICODETEXT);
                        if (hGlobal != IntPtr.Zero)
                        {
                            IntPtr lpwcstr = GlobalLock(hGlobal);
                            if (lpwcstr != IntPtr.Zero)
                            {
                                text = Marshal.PtrToStringUni(lpwcstr) ?? string.Empty;
                                GlobalUnlock(hGlobal);
                            }
                        }
                    }
                    CloseClipboard();
                }
            }
            catch { }
            return text;
        }

        public static async Task<string> GetSelectedTextAsync()
        {
            // Record clipboard sequence number before triggering copy
            uint seqBefore = GetClipboardSequenceNumber();

            await Task.Delay(25);

            // Synthesize Ctrl+C
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_C, 0, 0, UIntPtr.Zero);
            await Task.Delay(20);
            keybd_event(VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            // Wait up to 150ms for clipboard sequence number to change
            for (int i = 0; i < 15; i++)
            {
                await Task.Delay(10);
                uint seqAfter = GetClipboardSequenceNumber();
                if (seqAfter != seqBefore)
                {
                    // New text was genuinely copied into the clipboard!
                    string text = GetCurrentClipboardText();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }
                }
            }

            // Sequence number never changed -> NOTHING was selected/highlighted.
            return string.Empty;
        }

        public static async Task ReplaceSelectedTextAsync(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                var pkg = new DataPackage();
                pkg.SetText(text);
                Clipboard.SetContent(pkg);
                await Task.Delay(100);

                // Simulate Ctrl+V to replace selection in active app
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                keybd_event(VK_V, 0, 0, UIntPtr.Zero);
                await Task.Delay(30);
                keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch { }
        }
    }
}
