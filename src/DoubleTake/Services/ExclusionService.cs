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

        public static bool IsActiveAppExcluded()
        {
            var config = SettingsManager.Current;
            if (config.IsHotkeyPaused) return true;

            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return false;

            // 1. Check Process Name Exclusion
            try
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid != 0)
                {
                    using var proc = Process.GetProcessById((int)pid);
                    string procName = proc.ProcessName; // e.g. "cs2", "VALORANT-Win64-Shipping"
                    string fullExe = procName + ".exe";

                    if (config.ExcludedProcesses != null && config.ExcludedProcesses.Any(x =>
                        x.Equals(procName, StringComparison.OrdinalIgnoreCase) ||
                        x.Equals(fullExe, StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }
            }
            catch { }

            // 2. Check Fullscreen Mode (if enabled)
            if (config.DisableInFullscreen)
            {
                if (IsWindowFullscreen(hWnd))
                    return true;
            }

            return false;
        }

        private static bool IsWindowFullscreen(IntPtr hWnd)
        {
            try
            {
                // Exclude desktop / shell tray windows
                var sb = new StringBuilder(256);
                GetClassName(hWnd, sb, 256);
                string className = sb.ToString();
                if (className == "Progman" || className == "WorkerW" || className == "Shell_TrayWnd")
                    return false;

                if (!GetWindowRect(hWnd, out RECT rect)) return false;

                IntPtr hMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
                MONITORINFO mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (!GetMonitorInfo(hMonitor, ref mi)) return false;

                // If window covers the full monitor area
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
