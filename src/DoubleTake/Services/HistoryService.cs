using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace QuickTranslator
{
    public class HistoryEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SourceText { get; set; }
        public string TranslatedText { get; set; }
        public string SourceLang { get; set; } = "auto";
        public string TargetLang { get; set; } = "zh-CN";
        public string Engine { get; set; } = "Google";
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public string FormattedTime => Timestamp.ToString("MMM dd, HH:mm");
    }

    public static class HistoryService
    {
        private static readonly string HistoryFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DoubleTake");
        private static readonly string HistoryFile = Path.Combine(HistoryFolder, "history.json");

        private static List<HistoryEntry> _items = new List<HistoryEntry>();
        private static readonly object _lock = new object();

        static HistoryService()
        {
            Load();
        }

        public static void Load()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(HistoryFile))
                    {
                        string json = File.ReadAllText(HistoryFile);
                        _items = JsonSerializer.Deserialize<List<HistoryEntry>>(json) ?? new List<HistoryEntry>();
                    }
                    else
                    {
                        _items = new List<HistoryEntry>();
                    }
                }
                catch
                {
                    _items = new List<HistoryEntry>();
                }
            }
        }

        public static void AddEntry(string source, string translated, string targetLang, string engine, string sourceLang = "auto")
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(translated)) return;

            lock (_lock)
            {
                var entry = new HistoryEntry
                {
                    SourceText = source.Trim(),
                    TranslatedText = translated.Trim(),
                    SourceLang = sourceLang,
                    TargetLang = targetLang,
                    Engine = engine,
                    Timestamp = DateTime.Now
                };

                // Remove duplicate if same text was translated recently
                _items.RemoveAll(x => x.SourceText == entry.SourceText && (DateTime.Now - x.Timestamp).TotalSeconds < 30);
                _items.Insert(0, entry);

                // Cap to last 500 entries
                if (_items.Count > 500)
                    _items = _items.Take(500).ToList();

                Save();
            }
        }

        public static List<HistoryEntry> GetAll()
        {
            lock (_lock)
            {
                return _items.ToList();
            }
        }

        public static List<HistoryEntry> Search(string query)
        {
            lock (_lock)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return _items.ToList();

                return _items.Where(x =>
                    (x.SourceText != null && x.SourceText.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                    (x.TranslatedText != null && x.TranslatedText.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Engine != null && x.Engine.Contains(query, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }
        }

        public static void DeleteEntry(string id)
        {
            lock (_lock)
            {
                _items.RemoveAll(x => x.Id == id);
                Save();
            }
        }

        public static void ClearAll()
        {
            lock (_lock)
            {
                _items.Clear();
                Save();
            }
        }

        private static void Save()
        {
            try
            {
                if (SettingsManager.Current.SaveHistoryAcrossSessions)
                {
                    Directory.CreateDirectory(HistoryFolder);
                    string json = JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(HistoryFile, json);
                }
                else
                {
                    if (File.Exists(HistoryFile))
                    {
                        File.Delete(HistoryFile);
                    }
                }
            }
            catch { }
        }

        // ── Export to Markdown ───────────────────────────────────────────────
        public static string ExportToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# DoubleTake — Translation History");
            sb.AppendLine($"*Exported on {DateTime.Now:yyyy-MM-dd HH:mm:ss}*");
            sb.AppendLine();
            sb.AppendLine("| Time | Engine | Languages | Original Text | Translation |");
            sb.AppendLine("|---|---|---|---|---|");

            lock (_lock)
            {
                foreach (var item in _items)
                {
                    string safeSrc = EscapeMarkdown(item.SourceText);
                    string safeTr = EscapeMarkdown(item.TranslatedText);
                    sb.AppendLine($"| {item.FormattedTime} | {item.Engine} | {item.SourceLang} ➔ {item.TargetLang} | {safeSrc} | {safeTr} |");
                }
            }
            return sb.ToString();
        }

        // ── Export to CSV ────────────────────────────────────────────────────
        public static string ExportToCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Engine,SourceLanguage,TargetLanguage,OriginalText,TranslatedText");

            lock (_lock)
            {
                foreach (var item in _items)
                {
                    sb.AppendLine($"\"{item.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{EscapeCsv(item.Engine)}\",\"{EscapeCsv(item.SourceLang)}\",\"{EscapeCsv(item.TargetLang)}\",\"{EscapeCsv(item.SourceText)}\",\"{EscapeCsv(item.TranslatedText)}\"");
                }
            }
            return sb.ToString();
        }

        private static string EscapeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\r", " ").Replace("\n", " ").Replace("|", "\\|");
        }

        private static string EscapeCsv(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
