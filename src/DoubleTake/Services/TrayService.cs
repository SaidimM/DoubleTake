using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace QuickTranslator
{
    public static class TrayService
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpdata);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, nuint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, nuint uIdSubclass, nuint dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, nuint uIdSubclass);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint RegisterWindowMessage(string lpString);

        private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, nuint dwRefData);

        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x00000010;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
        }

        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_MODIFY = 0x00000001;
        private const uint NIM_DELETE = 0x00000002;

        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;

        private const uint MF_STRING = 0x00000000;
        private const uint MF_CHECKED = 0x00000008;
        private const uint MF_UNCHECKED = 0x00000000;
        private const uint MF_SEPARATOR = 0x00000800;
        private const uint MF_POPUP = 0x00000010;

        private const uint TPM_RETURNCMD = 0x0100;
        private const uint TPM_RIGHTBUTTON = 0x0002;

        private const int IDI_APPLICATION = 32512;

        private const uint WM_TRAYCALLBACK = 0x8001;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_LBUTTONDBLCLK = 0x0203;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_CONTEXTMENU = 0x007B;

        public static uint WmShowInstanceMsg { get; private set; }

        private static IntPtr _hWnd;
        private static NOTIFYICONDATA _nid;
        private static bool _isInitialized = false;
        private static SubclassProc _subclassDelegate;

        public static event Action OnOpenRequested;
        public static event Action OnHistoryRequested;
        public static event Action OnExitRequested;

        public static void Initialize(IntPtr hWnd)
        {
            if (_isInitialized) return;
            _hWnd = hWnd;

            WmShowInstanceMsg = RegisterWindowMessage("DoubleTake_ShowSingleInstance_Msg");

            // Subclass the main window to intercept tray callbacks and single-instance activation
            _subclassDelegate = new SubclassProc(WndProcSubclass);
            SetWindowSubclass(_hWnd, _subclassDelegate, 1001, 0);

            IntPtr hIcon = IntPtr.Zero;
            try
            {
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
                }
            }
            catch { }

            if (hIcon == IntPtr.Zero)
            {
                hIcon = LoadIcon(IntPtr.Zero, (IntPtr)IDI_APPLICATION);
            }

            _nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hWnd,
                uID = 1001,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYCALLBACK,
                hIcon = hIcon,
                szTip = "DoubleTake — Translation Companion"
            };

            Shell_NotifyIcon(NIM_ADD, ref _nid);
            _isInitialized = true;
        }

        private static IntPtr WndProcSubclass(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, nuint dwRefData)
        {
            if (uMsg == WM_TRAYCALLBACK)
            {
                uint msgType = (uint)(lParam.ToInt64() & 0xFFFF);
                if (msgType == WM_RBUTTONUP || msgType == WM_CONTEXTMENU)
                {
                    ShowContextMenu();
                    return IntPtr.Zero;
                }
                else if (msgType == WM_LBUTTONUP || msgType == WM_LBUTTONDBLCLK)
                {
                    OnOpenRequested?.Invoke();
                    return IntPtr.Zero;
                }
            }
            else if (WmShowInstanceMsg != 0 && uMsg == WmShowInstanceMsg)
            {
                OnOpenRequested?.Invoke();
                return IntPtr.Zero;
            }

            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        public static void ShowContextMenu()
        {
            if (_hWnd == IntPtr.Zero) return;

            IntPtr hMenu = CreatePopupMenu();
            IntPtr hEngineSub = CreatePopupMenu();

            var config = SettingsManager.Current;

            // Main Items
            AppendMenu(hMenu, MF_STRING, 1, "Open DoubleTake");
            AppendMenu(hMenu, MF_STRING, 2, "View Translation History");
            AppendMenu(hMenu, (config.IsHotkeyPaused ? MF_CHECKED : MF_UNCHECKED) | MF_STRING, 3, "Pause Double-Ctrl Hotkey");

            AppendMenu(hMenu, MF_SEPARATOR, 0, string.Empty);

            // Engine Submenu
            string[] engines = { "Google", "Bing", "DeepL", "Baidu", "Papago", "Yandex", "Youdao" };
            for (uint i = 0; i < engines.Length; i++)
            {
                uint flags = (config.ActiveEngine == engines[i] ? MF_CHECKED : MF_UNCHECKED) | MF_STRING;
                AppendMenu(hEngineSub, flags, 10 + i, $"{engines[i]} Engine");
            }
            AppendMenu(hMenu, MF_POPUP, (nuint)hEngineSub, "Active Engine");

            AppendMenu(hMenu, MF_SEPARATOR, 0, string.Empty);
            AppendMenu(hMenu, MF_STRING, 99, "Exit DoubleTake");

            GetCursorPos(out POINT pt);
            SetForegroundWindow(_hWnd);

            int cmd = TrackPopupMenu(hMenu, TPM_RETURNCMD | TPM_RIGHTBUTTON, pt.X, pt.Y, 0, _hWnd, IntPtr.Zero);
            DestroyMenu(hEngineSub);
            DestroyMenu(hMenu);

            if (cmd > 0)
            {
                HandleMenuCommand((uint)cmd);
            }
        }

        private static void HandleMenuCommand(uint cmd)
        {
            switch (cmd)
            {
                case 1:
                    OnOpenRequested?.Invoke();
                    break;
                case 2:
                    OnHistoryRequested?.Invoke();
                    break;
                case 3:
                    SettingsManager.Current.IsHotkeyPaused = !SettingsManager.Current.IsHotkeyPaused;
                    SettingsManager.SaveSettings();
                    break;
                case >= 10 and < 20:
                    string[] engines = { "Google", "Bing", "DeepL", "Baidu", "Papago", "Yandex", "Youdao" };
                    int idx = (int)(cmd - 10);
                    if (idx >= 0 && idx < engines.Length)
                    {
                        SettingsManager.Current.ActiveEngine = engines[idx];
                        SettingsManager.SaveSettings();
                    }
                    break;
                case 99:
                    RemoveTrayIcon();
                    OnExitRequested?.Invoke();
                    break;
            }
        }

        public static void RemoveTrayIcon()
        {
            if (_isInitialized)
            {
                if (_subclassDelegate != null && _hWnd != IntPtr.Zero)
                {
                    try { RemoveWindowSubclass(_hWnd, _subclassDelegate, 1001); } catch { }
                }

                Shell_NotifyIcon(NIM_DELETE, ref _nid);
                _isInitialized = false;
            }
        }
    }
}
