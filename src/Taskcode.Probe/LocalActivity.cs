using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Taskcode.Probe
{
    internal sealed class ActivityState
    {
        internal bool Working;
        internal DateTime At;
        internal void Observe(string kind, DateTime at)
        {
            if (at < At) return;
            if (kind == "task_started" || kind == "turn_started" || kind == "UserPromptSubmit" || kind == "PreToolUse" || kind == "PostToolUse" || kind == "PostToolUseFailure") { Working = true; At = at; }
            else if (kind == "task_complete" || kind == "turn_complete" || kind == "turn_aborted" || kind == "shutdown_complete" || kind == "Stop" || kind == "StopFailure" || kind == "SessionEnd" || kind == "PermissionRequest") { Working = false; At = at; }
        }
        internal bool IsActive(DateTime now) { return Working && now >= At && now - At < TimeSpan.FromMinutes(30); }
    }

    // Read only lifecycle metadata; no prompts, tool results, or transcripts are retained.
    internal sealed class LocalActivity
    {
        private sealed class Cursor
        {
            internal long Offset;
            internal string Pending = "";
            internal readonly ActivityState State = new ActivityState();
        }
        private readonly Dictionary<string, Cursor> files = new Dictionary<string, Cursor>(StringComparer.OrdinalIgnoreCase);
        private DateTime nextDiscovery;
        private string[] candidates = new string[0];
        internal int Read(DateTime now, bool claudeEnabled, bool codexEnabled)
        {
            int result = 0;
            if (claudeEnabled)
            {
                string folder = Path.Combine(ClaudeBridge.DataDirectory, "activity");
                if (Directory.Exists(folder))
                    foreach (string file in Directory.EnumerateFiles(folder, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(128))
                    {
                        try
                        {
                            var data = AccountJson.Parse(File.ReadAllText(file));
                            double? at = AccountJson.Number(AccountJson.Get(data, "at"));
                            if (at.HasValue && at > 0 && at <= (now - AccountJson.Epoch).TotalSeconds)
                            {
                                var state = new ActivityState();
                                state.Observe(AccountJson.Get(data, "event") as string, AccountJson.Epoch.AddSeconds(at.Value));
                                if (state.IsActive(now)) result |= 1;
                            }
                        }
                        catch (IOException) { } catch (UnauthorizedAccessException) { } catch (ArgumentException) { }
                    }
            }
            if (!codexEnabled) { files.Clear(); candidates = new string[0]; nextDiscovery = DateTime.MinValue; return result; }
            if (now >= nextDiscovery)
            {
                nextDiscovery = now.AddSeconds(3);
                string home = Environment.GetEnvironmentVariable("CODEX_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
                var paths = new List<string>();
                for (int day = 0; day < 2; day++)
                {
                    string directory = Path.Combine(home, "sessions", now.AddDays(-day).ToString("yyyy/MM/dd", CultureInfo.InvariantCulture));
                    if (Directory.Exists(directory)) paths.AddRange(Directory.EnumerateFiles(directory, "*.jsonl"));
                }
                candidates = paths.OrderByDescending(File.GetLastWriteTimeUtc).Take(32).ToArray();
                foreach (string old in files.Keys.Where(path => !candidates.Contains(path)).ToArray()) files.Remove(old);
            }
            foreach (string path in candidates)
            {
                try
                {
                    Cursor cursor;
                    if (!files.TryGetValue(path, out cursor)) { cursor = new Cursor(); files[path] = cursor; }
                    ReadEvents(path, cursor, now);
                    if (cursor.State.IsActive(now)) result |= 2;
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            return result;
        }
        private static void ReadEvents(string path, Cursor cursor, DateTime now)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length < cursor.Offset) { cursor.Offset = 0; cursor.Pending = ""; cursor.State.Working = false; cursor.State.At = DateTime.MinValue; }
                bool discardFirst = false;
                if (cursor.Offset == 0 && stream.Length > 1048576) { cursor.Offset = stream.Length - 1048576; discardFirst = true; }
                stream.Position = cursor.Offset;
                byte[] bytes = new byte[(int)Math.Min(1048576, stream.Length - cursor.Offset)];
                int count = stream.Read(bytes, 0, bytes.Length); cursor.Offset += count;
                string text = cursor.Pending + Encoding.UTF8.GetString(bytes, 0, count);
                int end = text.LastIndexOf('\n');
                if (end < 0) { cursor.Pending = text.Length < 1048576 ? text : ""; return; }
                cursor.Pending = text.Substring(end + 1);
                string[] lines = text.Substring(0, end).Split('\n');
                for (int i = discardFirst ? 1 : 0; i < lines.Length; i++) ObserveCodexLine(cursor.State, lines[i], now);
            }
        }
        internal static void ObserveCodexLine(ActivityState state, string line, DateTime now)
        {
            if (!(line.Contains("task_started") || line.Contains("turn_started") || line.Contains("task_complete") || line.Contains("turn_complete") || line.Contains("turn_aborted") || line.Contains("shutdown_complete"))) return;
            try
            {
                var data = AccountJson.Parse(line);
                if ((AccountJson.Get(data, "type") as string) != "event_msg") return;
                DateTime at;
                if (!DateTime.TryParse(AccountJson.Get(data, "timestamp") as string, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out at) || at > now) return;
                state.Observe(AccountJson.Get(AccountJson.Get(data, "payload"), "type") as string, at);
            }
            catch (ArgumentException) { } catch (InvalidOperationException) { }
        }
    }
}
