using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace QuickTranslator
{
    public static class ExclusionService
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int GWL_STYLE = -16;
        private const uint WS_MAXIMIZE = 0x01000000;
        private const uint WS_CAPTION = 0x00C00000;

        // Known IDE and developer tools that should NEVER be treated as fullscreen games
        private static readonly string[] NonGameProcessNames = new[]
        {
            "idea64", "idea", "pycharm64", "pycharm", "webstorm64", "webstorm",
            "rider64", "rider", "clion64", "clion", "goland64", "goland",
            "datagrip64", "datagrip", "rubymine64", "rubymine", "phpstorm64", "phpstorm",
            "studio64", "code", "devenv", "sublime_text", "notepad++", "windowsterminal",
            "wt", "cmd", "powershell", "pwsh", "alacritty", "wezterm", "chrome", "msedge", "firefox"
        };

        public static bool IsActiveAppExcluded()
        {
            var config = SettingsManager.Current;
            if (config.IsHotkeyPaused) return true;

            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return false;

            string procName = string.Empty;

            // 1. Check Process Name Exclusion
            try
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid != 0)
                {
                    using var proc = Process.GetProcessById((int)pid);
                    procName = proc.ProcessName; // e.g. "cs2", "idea64"
                    string fullExe = procName + ".exe";

                    if (config.ExcludedProcesses != null && config.ExcludedProcesses.Any(x =>
                    {
                        if (string.IsNullOrWhiteSpace(x)) return false;
                        string clean = System.IO.Path.GetFileName(x).Trim();
                        string cleanNoExt = System.IO.Path.GetFileNameWithoutExtension(x).Trim();
                        return clean.Equals(procName, StringComparison.OrdinalIgnoreCase) ||
                               clean.Equals(fullExe, StringComparison.OrdinalIgnoreCase) ||
                               cleanNoExt.Equals(procName, StringComparison.OrdinalIgnoreCase);
                    }))
                    {
                        return true;
                    }
                }
            }
            catch { }

            // 2. Check Fullscreen Game Mode (if enabled)
            if (config.DisableInFullscreen)
            {
                if (IsWindowFullscreenGame(hWnd, procName))
                    return true;
            }

            return false;
        }

        private static bool IsWindowFullscreenGame(IntPtr hWnd, string procName)
        {
            try
            {
                // Never treat developer tools, IDEs, browsers, or shell windows as fullscreen games
                if (!string.IsNullOrEmpty(procName) && NonGameProcessNames.Contains(procName, StringComparer.OrdinalIgnoreCase))
                    return false;

                var sb = new StringBuilder(256);
                GetClassName(hWnd, sb, 256);
                string className = sb.ToString();
                if (className == "Progman" || className == "WorkerW" || className == "Shell_TrayWnd" || className == "ApplicationFrameWindow")
                    return false;

                int style = GetWindowLong(hWnd, GWL_STYLE);
                
                // If it is a standard maximized desktop window with caption / system titlebar, it's not a fullscreen game
                if ((style & WS_MAXIMIZE) != 0 || (style & WS_CAPTION) == WS_CAPTION)
                    return false;

                if (!GetWindowRect(hWnd, out RECT rect)) return false;

                IntPtr hMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
                MONITORINFO mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (!GetMonitorInfo(hMonitor, ref mi)) return false;

                // Only true if borderless/exclusive fullscreen game covering entire monitor without standard caption
                return rect.Left <= mi.rcMonitor.Left &&
                       rect.Top <= mi.rcMonitor.Top &&
                       rect.Right >= mi.rcMonitor.Right &&
                       rect.Bottom >= mi.rcMonitor.Bottom;
            }
            catch
            {
                return false;
            }
        }
    }
}
