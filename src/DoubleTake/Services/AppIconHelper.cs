using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace QuickTranslator.Services
{
    public static class AppIconHelper
    {
        private static readonly ConcurrentDictionary<string, byte[]> _iconBytesCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, BitmapImage> _uiImageCache = new(StringComparer.OrdinalIgnoreCase);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, System.Text.StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        public static string GetProcessFullPath(int processId)
        {
            IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (hProcess == IntPtr.Zero) return null;

            try
            {
                var sb = new System.Text.StringBuilder(1024);
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
            return null;
        }

        public static byte[] GetIconPngBytes(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;

            if (_iconBytesCache.TryGetValue(filePath, out var cached))
                return cached;

            try
            {
                using var icon = Icon.ExtractAssociatedIcon(filePath);
                if (icon != null)
                {
                    using var bmp = icon.ToBitmap();
                    using var ms = new MemoryStream();
                    bmp.Save(ms, ImageFormat.Png);
                    byte[] bytes = ms.ToArray();
                    _iconBytesCache[filePath] = bytes;
                    return bytes;
                }
            }
            catch { }

            return null;
        }

        public static async Task<BitmapImage> GetBitmapImageFromBytesAsync(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;

            try
            {
                using var ms = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(ms))
                {
                    writer.WriteBytes(bytes);
                    await writer.StoreAsync();
                    await writer.FlushAsync();
                }
                ms.Seek(0);

                var img = new BitmapImage();
                await img.SetSourceAsync(ms);
                return img;
            }
            catch
            {
                return null;
            }
        }

        public static async Task<BitmapImage> GetAppIconAsync(string filePathOrExe)
        {
            if (string.IsNullOrWhiteSpace(filePathOrExe)) return null;

            if (_uiImageCache.TryGetValue(filePathOrExe, out var cachedImg))
                return cachedImg;

            string resolvedPath = filePathOrExe;
            if (!File.Exists(resolvedPath))
            {
                resolvedPath = FindExecutablePath(filePathOrExe);
            }

            if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
                return null;

            byte[] bytes = await Task.Run(() => GetIconPngBytes(resolvedPath));
            if (bytes != null)
            {
                var img = await GetBitmapImageFromBytesAsync(bytes);
                if (img != null)
                {
                    _uiImageCache[filePathOrExe] = img;
                    _uiImageCache[resolvedPath] = img;
                    return img;
                }
            }

            return null;
        }

        private static string FindExecutablePath(string exeName)
        {
            try
            {
                // Check PATH and standard system folders
                string[] paths = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                };

                foreach (var dir in paths)
                {
                    if (string.IsNullOrEmpty(dir)) continue;
                    string candidate = Path.Combine(dir, exeName);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch { }
            return null;
        }
    }
}
