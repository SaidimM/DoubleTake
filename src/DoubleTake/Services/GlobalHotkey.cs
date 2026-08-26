using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace QuickTranslator
{
    public static class GlobalHotkey
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int VK_CONTROL = 0x11;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;

        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        public static event EventHandler DoubleCtrlPressed;
        private static DateTime _lastCtrlPressTime = DateTime.MinValue;

        public static void Start()
        {
            if (_hookID == IntPtr.Zero)
            {
                _hookID = SetHook(_proc);
                QuickTranslator.Helpers.DebugLog.Write($"GlobalHotkey.Start: _hookID={_hookID}, error={Marshal.GetLastWin32Error()}");
            }
        }

        public static void Stop()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            try
            {
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    IntPtr hMod = GetModuleHandle(curModule.ModuleName);
                    if (hMod == IntPtr.Zero)
                        hMod = GetModuleHandle(null);
                    return SetWindowsHookEx(WH_KEYBOARD_LL, proc, hMod, 0);
                }
            }
            catch (Exception ex)
            {
                QuickTranslator.Helpers.DebugLog.Write($"GlobalHotkey.SetHook error: {ex}");
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, IntPtr.Zero, 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                bool isCtrl = (vkCode == VK_CONTROL || vkCode == VK_LCONTROL || vkCode == VK_RCONTROL);

                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                {
                    if (!isCtrl)
                    {
                        _lastCtrlPressTime = DateTime.MinValue;
                    }
                }
                else if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                {
                    if (isCtrl)
                    {
                        var now = DateTime.UtcNow;
                        double elapsed = (now - _lastCtrlPressTime).TotalMilliseconds;
                        int maxSpeed = SettingsManager.Current?.DoubleTapIntervalMs ?? 550;
                        QuickTranslator.Helpers.DebugLog.Write($"GlobalHotkey: Ctrl released, elapsed={elapsed:F0}ms, maxSpeed={maxSpeed}ms");
                        if (elapsed >= 15 && elapsed <= Math.Max(700, maxSpeed + 100))
                        {
                            _lastCtrlPressTime = DateTime.MinValue;
                            QuickTranslator.Helpers.DebugLog.Write("GlobalHotkey: Firing DoubleCtrlPressed event!");
                            DoubleCtrlPressed?.Invoke(null, EventArgs.Empty);
                            return (IntPtr)1; // Consume the 2nd Ctrl release to avoid double-ctrl conflicts in apps like JetBrains IDEs
                        }
                        else
                        {
                            _lastCtrlPressTime = now;
                        }
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
