using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace QuickTranslator
{
    public static class StartupService
    {
        private const string TaskId = "DoubleTakeStartup";
        private const string RegKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "DoubleTake";

        public static async Task<bool> IsStartupEnabledAsync()
        {
            try
            {
                var task = await StartupTask.GetAsync(TaskId);
                if (task != null)
                {
                    return task.State == StartupTaskState.Enabled || task.State == StartupTaskState.EnabledByPolicy;
                }
            }
            catch { }

            // Registry Fallback
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> SetStartupAsync(bool enable)
        {
            try
            {
                var task = await StartupTask.GetAsync(TaskId);
                if (task != null)
                {
                    if (enable)
                    {
                        var state = await task.RequestEnableAsync();
                        return state == StartupTaskState.Enabled;
                    }
                    else
                    {
                        task.Disable();
                        return true;
                    }
                }
            }
            catch { }

            // Registry Fallback
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, true);
                if (key != null)
                {
                    if (enable)
                    {
                        string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                            key.SetValue(AppName, $"\"{exePath}\" --minimized");
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                    return true;
                }
            }
            catch { }

            return false;
        }
    }
}
