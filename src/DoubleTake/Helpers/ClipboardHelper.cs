using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using QuickTranslator.Helpers;

namespace QuickTranslator
{
    public static class ClipboardHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

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

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GlobalUnlock(IntPtr hMem);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public UIntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT { public uint uMsg; public ushort wParamL, wParamH; }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_C = 0x43;
        private const ushort VK_V = 0x56;

        private const uint CF_UNICODETEXT = 13;

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
            uint seqBefore = GetClipboardSequenceNumber();

            // Wait a brief moment for user to release physical Ctrl key if still held down
            for (int i = 0; i < 6; i++)
            {
                if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) == 0) break;
                await Task.Delay(10);
            }

            await Task.Delay(20);

            // Synthesize atomic Ctrl+C using SendInput
            var inputs = new INPUT[]
            {
                new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = VK_CONTROL, dwFlags = 0 } } },
                new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = VK_C, dwFlags = 0 } } },
                new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = VK_C, dwFlags = KEYEVENTF_KEYUP } } },
                new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = VK_CONTROL, dwFlags = KEYEVENTF_KEYUP } } }
            };

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            DebugLog.Write($"ClipboardHelper: Sent SendInput(Ctrl+C), seqBefore={seqBefore}");

            // Poll for clipboard sequence change (up to 450ms for heavy IDEs like IntelliJ/PyCharm)
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(15);
                uint seqAfter = GetClipboardSequenceNumber();
                if (seqAfter != seqBefore)
                {
                    string text = GetCurrentClipboardText();
                    DebugLog.Write($"ClipboardHelper: Seq changed to {seqAfter}! Text length={text?.Length ?? 0}, Preview='{text}'");
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }
                    return string.Empty;
                }
            }

            DebugLog.Write($"ClipboardHelper: Seq did not change ({seqBefore}). No text copied.");
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
                await Task.Delay(80);

                var inputs = new INPUT[]
                {
                    new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = VK_CONTROL, dwFlags = 0 } } },
                    new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = VK_V, dwFlags = 0 } } },
                    new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = VK_V, dwFlags = KEYEVENTF_KEYUP } } },
                    new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = VK_CONTROL, dwFlags = KEYEVENTF_KEYUP } } }
                };

                SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            }
            catch { }
        }
    }
}
