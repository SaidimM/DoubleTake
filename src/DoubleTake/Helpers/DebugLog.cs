using System;
using System.IO;

namespace QuickTranslator.Helpers
{
    public static class DebugLog
    {
        private static readonly string LogFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".doubletake", "debug.log");

        private static readonly object _lock = new object();

        public static void Write(string message)
        {
            try
            {
                lock (_lock)
                {
                    string dir = Path.GetDirectoryName(LogFile);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.AppendAllText(LogFile, $"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}{Environment.NewLine}");
                }
            }
            catch { }
        }
    }
}
