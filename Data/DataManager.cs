using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using PeriodicAccessTool.Models;

namespace PeriodicAccessTool.Data
{
    public class DataManager
    {
        private readonly string _dataDir;
        private readonly string _settingsFile;
        private readonly string _entriesFile;
        private readonly string _logFile;

        private AppSettings _settings;
        private List<UrlEntry> _entries;
        private readonly object _lock = new object();

        public DataManager()
        {
            _dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "定期アクセス支援ツール");
            Directory.CreateDirectory(_dataDir);

            _settingsFile = Path.Combine(_dataDir, "settings.json");
            _entriesFile = Path.Combine(_dataDir, "entries.json");
            _logFile = Path.Combine(_dataDir, "logs.csv");

            _settings = LoadSettings();
            _entries = LoadEntries();
        }

        public string DataDir => _dataDir;
        public string LogFile => _logFile;

        // ---- Settings ----

        public AppSettings GetSettings()
        {
            lock (_lock) return _settings;
        }

        public void SaveSettings(AppSettings settings)
        {
            lock (_lock)
            {
                _settings = settings;
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFile, json, Encoding.UTF8);
            }
        }

        private AppSettings LoadSettings()
        {
            if (!File.Exists(_settingsFile)) return new AppSettings();
            try
            {
                var json = File.ReadAllText(_settingsFile, Encoding.UTF8);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch { return new AppSettings(); }
        }

        // ---- URL Entries ----

        public List<UrlEntry> GetEntries()
        {
            lock (_lock) return new List<UrlEntry>(_entries);
        }

        public void AddEntry(UrlEntry entry)
        {
            lock (_lock)
            {
                entry.SortOrder = _entries.Count;
                _entries.Add(entry);
                SaveEntriesLocked();
            }
        }

        public void UpdateEntry(UrlEntry entry)
        {
            lock (_lock)
            {
                int idx = _entries.FindIndex(e => e.Id == entry.Id);
                if (idx >= 0)
                {
                    _entries[idx] = entry;
                    SaveEntriesLocked();
                }
            }
        }

        public void DeleteEntry(string id)
        {
            lock (_lock)
            {
                _entries.RemoveAll(e => e.Id == id);
                SaveEntriesLocked();
            }
        }

        private void SaveEntriesLocked()
        {
            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_entriesFile, json, Encoding.UTF8);
        }

        private List<UrlEntry> LoadEntries()
        {
            if (!File.Exists(_entriesFile)) return new List<UrlEntry>();
            try
            {
                var json = File.ReadAllText(_entriesFile, Encoding.UTF8);
                return JsonSerializer.Deserialize<List<UrlEntry>>(json) ?? new List<UrlEntry>();
            }
            catch { return new List<UrlEntry>(); }
        }

        // ---- Logs ----

        public void AppendLog(ExecutionLog log)
        {
            var line = string.Join(",",
                log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                CsvEscape(log.EntryName),
                CsvEscape(log.Url),
                log.Status,
                log.ChromeLaunched ? "1" : "0",
                log.OpenSuccess ? "1" : "0",
                log.CloseSuccess ? "1" : "0",
                CsvEscape(log.ErrorMessage));
            lock (_lock)
            {
                File.AppendAllText(_logFile, line + Environment.NewLine, Encoding.UTF8);
            }
        }

        public List<ExecutionLog> GetLogs()
        {
            var logs = new List<ExecutionLog>();
            string[] lines;
            lock (_lock)
            {
                if (!File.Exists(_logFile)) return logs;
                lines = File.ReadAllLines(_logFile, Encoding.UTF8);
            }

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = SplitCsvLine(line);
                if (parts.Length < 8) continue;
                try
                {
                    logs.Add(new ExecutionLog
                    {
                        Timestamp = DateTime.Parse(parts[0]),
                        EntryName = parts[1],
                        Url = parts[2],
                        ChromeLaunched = parts[4] == "1",
                        OpenSuccess = parts[5] == "1",
                        CloseSuccess = parts[6] == "1",
                        ErrorMessage = parts[7]
                    });
                }
                catch { }
            }
            return logs;
        }

        public void PruneLogs(int retentionDays)
        {
            lock (_lock)
            {
                if (!File.Exists(_logFile)) return;
                var cutoff = DateTime.Now.AddDays(-retentionDays);
                var lines = File.ReadAllLines(_logFile, Encoding.UTF8);
                var kept = new List<string>();
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = SplitCsvLine(line);
                    if (parts.Length < 1) continue;
                    if (DateTime.TryParse(parts[0], out var dt) && dt >= cutoff)
                        kept.Add(line);
                }
                File.WriteAllLines(_logFile, kept, Encoding.UTF8);
            }
        }

        private static string CsvEscape(string s)
        {
            s ??= "";
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        private static string[] SplitCsvLine(string line)
        {
            // シンプルなCSVパーサー（ダブルクォート対応）
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQuote = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuote)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuote = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    if (c == '"') { inQuote = true; }
                    else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                    else { sb.Append(c); }
                }
            }
            fields.Add(sb.ToString());
            return fields.ToArray();
        }
    }
}
