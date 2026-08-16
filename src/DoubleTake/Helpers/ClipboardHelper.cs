using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GlobalUnlock(IntPtr hMem);

        private const uint CF_UNICODETEXT = 13;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_C = 0x43;

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

        /// <summary>
        /// Simulate Ctrl+C and return any newly selected text.
        /// Returns empty string if nothing was selected.
        /// </summary>
        public static async Task<string> GetSelectedTextAsync()
        {
            await Task.Delay(30);
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_C, 0, 0, UIntPtr.Zero);
            await Task.Delay(20);
            keybd_event(VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            await Task.Delay(180);
            return GetCurrentClipboardText();
        }

        /// <summary>
        /// Quick check: returns the text currently selected in the foreground app,
        /// or empty if nothing is selected.
        /// </summary>
        public static async Task<string> TryGetSelectionAsync()
        {
            string before = GetCurrentClipboardText();
            await Task.Delay(30);
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_C, 0, 0, UIntPtr.Zero);
            await Task.Delay(20);
            keybd_event(VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            await Task.Delay(180);
            string after = GetCurrentClipboardText();

            // If clipboard content changed, something was selected
            if (!string.IsNullOrWhiteSpace(after) && after != before)
                return after;

            return string.Empty;
        }
    }
}
