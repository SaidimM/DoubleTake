using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Win32;
using QuickTranslator.Services;

namespace QuickTranslator
{
    public class DiscoveredAppItem
    {
        public string Name { get; set; }
        public string ExeName { get; set; }
        public string FullPath { get; set; }
        public string Source { get; set; } // "Running" or "Installed"
        public bool IsRunning => Source == "Running";
        public bool IsSelected { get; set; }

        public BitmapImage IconImage { get; set; }
        public bool HasIcon => IconImage != null;

        public string DisplayHeader => Name;
        public string DisplaySubtitle => ExeName;
    }

    public class ExcludedAppDisplayItem
    {
        public string ExeName { get; set; }
        public string DisplayName { get; set; }
        public string FullPath { get; set; }
        public BitmapImage IconImage { get; set; }
        public bool HasIcon => IconImage != null;
    }

    public static class AppDiscoveryService
    {
        private static readonly Dictionary<string, (string DisplayName, string Path)> _appMetadataCache = new(StringComparer.OrdinalIgnoreCase);

        public static async Task<List<DiscoveredAppItem>> GetRunningAppsAsync()
        {
            var rawList = await Task.Run(() =>
            {
                var list = new List<DiscoveredAppItem>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    var processes = Process.GetProcesses();
                    foreach (var p in processes)
                    {
                        try
                        {
                            if (p.MainWindowHandle == IntPtr.Zero) continue;
                            if (string.IsNullOrWhiteSpace(p.MainWindowTitle)) continue;
                            if (p.ProcessName.Equals("DoubleTake", StringComparison.OrdinalIgnoreCase)) continue;
                            if (p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase)) continue;

                            string exe = p.ProcessName + ".exe";
                            if (seen.Add(exe))
                            {
                                string fullPath = AppIconHelper.GetProcessFullPath(p.Id);
                                if (!string.IsNullOrEmpty(fullPath))
                                {
                                    AppIconHelper.EnsureIconPngCached(fullPath);
                                }

                                list.Add(new DiscoveredAppItem
                                {
                                    Name = p.MainWindowTitle.Length > 45 ? p.MainWindowTitle.Substring(0, 42) + "..." : p.MainWindowTitle,
                                    ExeName = exe,
                                    FullPath = fullPath,
                                    Source = "Running"
                                });

                                if (!string.IsNullOrEmpty(fullPath))
                                {
                                    lock (_appMetadataCache)
                                    {
                                        _appMetadataCache[exe] = (p.MainWindowTitle, fullPath);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
                catch { }

                return list.OrderBy(x => x.Name).ToList();
            });

            // Hydrate UI BitmapImages on UI thread
            foreach (var item in rawList)
            {
                item.IconImage = AppIconHelper.GetAppIcon(item.FullPath ?? item.ExeName);
            }

            return rawList;
        }

        public static async Task<List<DiscoveredAppItem>> GetInstalledAppsAsync()
        {
            var rawList = await Task.Run(() =>
            {
                var dict = new Dictionary<string, DiscoveredAppItem>(StringComparer.OrdinalIgnoreCase);

                // 1. Registry Scan (HKLM & HKCU Uninstall keys)
                string[] registryKeys = new[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (var baseKey in new[] { Registry.LocalMachine, Registry.CurrentUser })
                {
                    foreach (var subKeyPath in registryKeys)
                    {
                        try
                        {
                            using var key = baseKey.OpenSubKey(subKeyPath);
                            if (key == null) continue;

                            foreach (var subName in key.GetSubKeyNames())
                            {
                                try
                                {
                                    using var appKey = key.OpenSubKey(subName);
                                    if (appKey == null) continue;

                                    string displayName = appKey.GetValue("DisplayName") as string;
                                    if (string.IsNullOrWhiteSpace(displayName)) continue;

                                    // Filter out system components
                                    object systemComponent = appKey.GetValue("SystemComponent");
                                    if (systemComponent != null && (int)systemComponent == 1) continue;

                                    string displayIcon = appKey.GetValue("DisplayIcon") as string;
                                    string exeName = string.Empty;
                                    string fullPath = string.Empty;

                                    if (!string.IsNullOrWhiteSpace(displayIcon))
                                    {
                                        string cleaned = displayIcon.Split(',')[0].Trim('"', ' ');
                                        if (cleaned.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(cleaned))
                                        {
                                            exeName = Path.GetFileName(cleaned);
                                            fullPath = cleaned;
                                        }
                                    }

                                    if (string.IsNullOrWhiteSpace(exeName))
                                    {
                                        string installLocation = appKey.GetValue("InstallLocation") as string;
                                        if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
                                        {
                                            var exeFiles = Directory.GetFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly);
                                            if (exeFiles.Length > 0)
                                            {
                                                var mainExe = exeFiles.FirstOrDefault(x => !x.Contains("unins", StringComparison.OrdinalIgnoreCase) &&
                                                                                           !x.Contains("setup", StringComparison.OrdinalIgnoreCase) &&
                                                                                           !x.Contains("crash", StringComparison.OrdinalIgnoreCase));
                                                if (mainExe != null)
                                                {
                                                    exeName = Path.GetFileName(mainExe);
                                                    fullPath = mainExe;
                                                }
                                            }
                                        }
                                    }

                                    if (!string.IsNullOrWhiteSpace(exeName) && !dict.ContainsKey(exeName))
                                    {
                                        if (!string.IsNullOrEmpty(fullPath))
                                        {
                                            AppIconHelper.EnsureIconPngCached(fullPath);
                                        }

                                        dict[exeName] = new DiscoveredAppItem
                                        {
                                            Name = displayName,
                                            ExeName = exeName,
                                            FullPath = fullPath,
                                            Source = "Installed"
                                        };

                                        if (!string.IsNullOrEmpty(fullPath))
                                        {
                                            lock (_appMetadataCache)
                                            {
                                                _appMetadataCache[exeName] = (displayName, fullPath);
                                            }
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }
                }

                // 2. Start Menu Shortcuts Scan
                try
                {
                    string[] startMenuPaths = new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs")
                    };

                    foreach (var path in startMenuPaths)
                    {
                        if (!Directory.Exists(path)) continue;

                        var lnkFiles = Directory.GetFiles(path, "*.lnk", SearchOption.AllDirectories);
                        foreach (var lnk in lnkFiles)
                        {
                            try
                            {
                                string appName = Path.GetFileNameWithoutExtension(lnk);
                                if (appName.StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase)) continue;

                                string target = ResolveShortcutTarget(lnk);
                                if (!string.IsNullOrWhiteSpace(target) && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    string exeName = Path.GetFileName(target);
                                    if (!dict.ContainsKey(exeName))
                                    {
                                        if (File.Exists(target))
                                        {
                                            AppIconHelper.EnsureIconPngCached(target);
                                        }

                                        dict[exeName] = new DiscoveredAppItem
                                        {
                                            Name = appName,
                                            ExeName = exeName,
                                            FullPath = target,
                                            Source = "Installed"
                                        };

                                        lock (_appMetadataCache)
                                        {
                                            _appMetadataCache[exeName] = (appName, target);
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                return dict.Values.OrderBy(x => x.Name).ToList();
            });

            // Hydrate UI BitmapImages on UI thread
            foreach (var item in rawList)
            {
                item.IconImage = AppIconHelper.GetAppIcon(item.FullPath ?? item.ExeName);
            }

            return rawList;
        }

        public static async Task<ExcludedAppDisplayItem> ResolveExcludedAppItemAsync(string exeOrPath)
        {
            string cleanExe = Path.GetFileName(exeOrPath);
            string displayName = cleanExe;
            string fullPath = exeOrPath;

            lock (_appMetadataCache)
            {
                if (_appMetadataCache.TryGetValue(cleanExe, out var meta))
                {
                    displayName = meta.DisplayName;
                    fullPath = meta.Path;
                }
            }

            var iconImage = await AppIconHelper.GetAppIconAsync(fullPath ?? cleanExe);

            return new ExcludedAppDisplayItem
            {
                ExeName = cleanExe,
                DisplayName = displayName,
                FullPath = fullPath,
                IconImage = iconImage
            };
        }

        private static string ResolveShortcutTarget(string shortcutFilename)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return null;

                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(shortcutFilename);
                string targetPath = shortcut.TargetPath;
                MarshalRelease(shortcut);
                MarshalRelease(shell);
                return targetPath;
            }
            catch
            {
                return null;
            }
        }

        private static void MarshalRelease(object obj)
        {
            try
            {
                if (obj != null)
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(obj);
            }
            catch { }
        }
    }
}
