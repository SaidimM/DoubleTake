using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;

namespace QuickTranslator.Services
{
    public static class AppIconHelper
    {
        private static readonly string IconsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".doubletake", "icons");

        private static readonly ConcurrentDictionary<string, BitmapImage> _loadedImages = new(StringComparer.OrdinalIgnoreCase);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        static AppIconHelper()
        {
            try
            {
                Directory.CreateDirectory(IconsDir);
            }
            catch { }
        }

        public static string GetProcessFullPath(int processId)
        {
            try
            {
                IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
                if (hProcess == IntPtr.Zero) return null;

                try
                {
                    var sb = new StringBuilder(1024);
                    int size = sb.Capacity;
                    if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
                    {
                        return sb.ToString();
                    }
                }
                finally
                {
                    CloseHandle(hProcess);
                }
            }
            catch { }
            return null;
        }

        public static string EnsureIconPngCached(string fullPathOrExe)
        {
            if (string.IsNullOrWhiteSpace(fullPathOrExe)) return null;

            string cleanName = Path.GetFileNameWithoutExtension(fullPathOrExe).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(cleanName)) return null;

            string targetPng = Path.Combine(IconsDir, cleanName + ".png");
            if (File.Exists(targetPng))
                return targetPng;

            string sourceExe = fullPathOrExe;
            if (!File.Exists(sourceExe))
            {
                sourceExe = FindExecutablePath(fullPathOrExe);
            }

            if (string.IsNullOrEmpty(sourceExe) || !File.Exists(sourceExe))
                return null;

            try
            {
                using var icon = Icon.ExtractAssociatedIcon(sourceExe);
                if (icon != null)
                {
                    using var bmp = icon.ToBitmap();
                    bmp.Save(targetPng, ImageFormat.Png);
                    return targetPng;
                }
            }
            catch { }

            return null;
        }

        public static BitmapImage GetAppIcon(string fullPathOrExe)
        {
            if (string.IsNullOrWhiteSpace(fullPathOrExe)) return null;

            if (_loadedImages.TryGetValue(fullPathOrExe, out var cached))
                return cached;

            string pngPath = EnsureIconPngCached(fullPathOrExe);
            if (!string.IsNullOrEmpty(pngPath) && File.Exists(pngPath))
            {
                try
                {
                    var img = new BitmapImage(new Uri(pngPath));
                    _loadedImages[fullPathOrExe] = img;
                    return img;
                }
                catch { }
            }

            return null;
        }

        public static async Task<BitmapImage> GetAppIconAsync(string fullPathOrExe)
        {
            if (string.IsNullOrWhiteSpace(fullPathOrExe)) return null;

            if (_loadedImages.TryGetValue(fullPathOrExe, out var cached))
                return cached;

            string pngPath = await Task.Run(() => EnsureIconPngCached(fullPathOrExe));
            if (!string.IsNullOrEmpty(pngPath) && File.Exists(pngPath))
            {
                try
                {
                    var img = new BitmapImage(new Uri(pngPath));
                    _loadedImages[fullPathOrExe] = img;
                    return img;
                }
                catch { }
            }

            return null;
        }

        private static string FindExecutablePath(string exeName)
        {
            try
            {
                string[] paths = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")
                };

                foreach (var dir in paths)
                {
                    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;

                    string candidate = Path.Combine(dir, exeName);
                    if (File.Exists(candidate)) return candidate;

                    // Check 1 level deep (e.g. Program Files\App\app.exe)
                    try
                    {
                        var matches = Directory.GetFiles(dir, exeName, SearchOption.AllDirectories);
                        if (matches.Length > 0) return matches[0];
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }
    }
}
