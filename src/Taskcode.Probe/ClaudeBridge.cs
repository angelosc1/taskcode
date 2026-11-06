using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Taskcode.Probe
{
    internal static class ClaudeBridge
    {
        internal static readonly string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Taskcode");
        internal static readonly string CachePath = Path.Combine(DataDirectory, "claude-usage.json");
        private static string ClaudeDirectory { get { return Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude"); } }
        private static string SettingsPath { get { return Path.Combine(ClaudeDirectory, "settings.json"); } }
        private static string BridgePath { get { return Path.Combine(DataDirectory, "Taskcode.ClaudeBridge.exe"); } }
        private static string BackupPath { get { return Path.Combine(DataDirectory, "claude-statusline-original.json"); } }
        internal static string Command
        {
            get { return CommandFor(""); }
        }
        private static string CommandFor(string argument)
        {
            string suffix = argument.Length == 0 ? "" : " " + argument;
            if (File.Exists(GitBashPath())) return "\"" + BridgePath.Replace('\\', '/') + "\"" + suffix;
            string invocation = "& '" + BridgePath.Replace("'", "''") + "'" + suffix;
            return "powershell.exe -NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(invocation));
        }
        private static readonly string[] ActivityEvents = { "UserPromptSubmit", "PreToolUse", "PostToolUse", "PostToolUseFailure", "PermissionRequest", "Stop", "StopFailure", "SessionEnd" };
        internal static void ConfigureActivityHooks(Dictionary<string, object> settings, bool install)
        {
            var hooks = AccountJson.Map(AccountJson.Get(settings, "hooks"));
            foreach (string name in ActivityEvents)
            {
                string command = CommandFor("--activity-" + name);
                var groups = new List<object>();
                var existing = AccountJson.Get(hooks, name) as System.Collections.IEnumerable;
                if (existing != null) foreach (object item in existing)
                {
                    var group = AccountJson.Map(item);
                    var members = AccountJson.Get(group, "hooks") as System.Collections.IEnumerable;
                    if (members == null) { groups.Add(item); continue; }
                    var kept = new List<object>(); bool removed = false;
                    foreach (object member in members)
                        if ((AccountJson.Get(member, "command") as string) == command) removed = true; else kept.Add(member);
                    if (!removed) groups.Add(item);
                    else if (kept.Count > 0) { var copy = new Dictionary<string, object>(group); copy["hooks"] = kept; groups.Add(copy); }
                }
                if (install) groups.Add(new Dictionary<string, object> { { "hooks", new object[] { new Dictionary<string, object> { { "type", "command" }, { "command", command }, { "timeout", 2 } } } } });
                if (groups.Count == 0) hooks.Remove(name); else hooks[name] = groups;
            }
            if (hooks.Count == 0) settings.Remove("hooks"); else settings["hooks"] = hooks;
        }
        private static string GitBashPath()
        {
            string bash = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Git\bin\bash.exe");
            return File.Exists(bash) ? bash : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Git\bin\bash.exe");
        }
        internal static bool IsInstalled()
        {
            if (!File.Exists(SettingsPath)) return false;
            return (AccountJson.Get(AccountJson.Get(AccountJson.Parse(File.ReadAllText(SettingsPath)), "statusLine"), "command") as string) == Command;
        }
        internal static void Install()
        {
            Directory.CreateDirectory(DataDirectory);
            string source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Taskcode.ClaudeBridge.exe");
            if (!File.Exists(source)) throw new IOException("The Claude bridge executable is missing. Rebuild Taskcode.");
            File.Copy(source, BridgePath, true);
            var settings = File.Exists(SettingsPath) ? AccountJson.Parse(File.ReadAllText(SettingsPath)) : new Dictionary<string, object>();
            object original = AccountJson.Get(settings, "statusLine");
            if ((AccountJson.Get(original, "command") as string) != Command)
            {
                // Refuse to lose an earlier backup after a user manually replaced our integration.
                if (File.Exists(BackupPath)) throw new InvalidOperationException("A previous Claude status-line backup exists. Disconnect Claude before reconnecting.");
                AtomicWrite(BackupPath, AccountJson.Encode(new { statusLine = original }));
            }
            settings["statusLine"] = new { type = "command", command = Command };
            ConfigureActivityHooks(settings, true);
            Directory.CreateDirectory(ClaudeDirectory);
            AtomicWrite(SettingsPath, AccountJson.Encode(settings));
        }
        internal static void Uninstall()
        {
            if (File.Exists(SettingsPath))
            {
                var cleaned = AccountJson.Parse(File.ReadAllText(SettingsPath));
                ConfigureActivityHooks(cleaned, false);
                AtomicWrite(SettingsPath, AccountJson.Encode(cleaned));
            }
            if (IsInstalled())
            {
                var settings = AccountJson.Parse(File.ReadAllText(SettingsPath));
                if (!File.Exists(BackupPath)) throw new IOException("The original status-line backup is missing. Claude settings were left unchanged.");
                object original = AccountJson.Get(AccountJson.Parse(File.ReadAllText(BackupPath)), "statusLine");
                if (original == null) settings.Remove("statusLine"); else settings["statusLine"] = original;
                AtomicWrite(SettingsPath, AccountJson.Encode(settings));
            }
            if (File.Exists(BackupPath)) File.Delete(BackupPath);
            if (File.Exists(CachePath)) File.Delete(CachePath);
            string activityDirectory = Path.Combine(DataDirectory, "activity");
            if (Directory.Exists(activityDirectory)) foreach (string file in Directory.EnumerateFiles(activityDirectory, "*.json")) File.Delete(file);
        }
        internal static void AtomicWrite(string path, string text)
        {
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, text, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        internal static AccountSnapshot Read()
        {
            if (!IsInstalled()) return new AccountSnapshot { Status = "Not connected. Click Connect Claude." };
            if (!File.Exists(CachePath)) return new AccountSnapshot { Status = "Connected. Use Claude Code to receive quota data." };
            object data = AccountJson.Parse(File.ReadAllText(CachePath));
            double? seconds = AccountJson.Number(AccountJson.Get(data, "observed_at"));
            if (!seconds.HasValue || seconds < 0 || seconds > (DateTime.UtcNow - AccountJson.Epoch).TotalSeconds + 60) throw new InvalidDataException();
            var result = AccountSnapshot.Claude(data, AccountJson.Epoch.AddSeconds(seconds.Value));
            if (result.Periods[0] == null && result.Periods[1] == null) result.Status = "Claude Code has not supplied subscription quota yet.";
            return result;
        }
        private static object Window(AccountSnapshot snapshot, int index)
        {
            var period = snapshot.Periods[index];
            return period == null ? null : new { used_percentage = 100 - snapshot.Remaining[index].Value, resets_at = period.WaitingForFirstRequest ? (double?)null : (period.EndUtc - AccountJson.Epoch).TotalSeconds };
        }
        internal static int Main(string[] args)
        {
            string input = Console.In.ReadToEnd();
            try
            {
                // Save only whitelisted quota fields. Never save the incoming session payload.
                if (input.Length > 1048576) return 1;
                if (args.Length == 1 && args[0].StartsWith("--activity-", StringComparison.Ordinal))
                {
                    string kind = args[0].Substring("--activity-".Length);
                    if (Array.IndexOf(ActivityEvents, kind) < 0) return 0;
                    string session = AccountJson.Get(AccountJson.Parse(input), "session_id") as string;
                    if (string.IsNullOrWhiteSpace(session)) return 0;
                    string filename;
                    using (var hash = System.Security.Cryptography.SHA256.Create()) filename = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(session))).Replace("-", "");
                    string directory = Path.Combine(DataDirectory, "activity"); Directory.CreateDirectory(directory);
                    AtomicWrite(Path.Combine(directory, filename + ".json"), AccountJson.Encode(new { @event = kind, at = (DateTime.UtcNow - AccountJson.Epoch).TotalSeconds }));
                    return 0;
                }
                var snapshot = AccountSnapshot.Claude(AccountJson.Parse(input), DateTime.UtcNow);
                Directory.CreateDirectory(DataDirectory);
                if (snapshot.Periods[0] != null || snapshot.Periods[1] != null)
                    AtomicWrite(CachePath, SerializeSnapshot(snapshot));
                string original = File.Exists(BackupPath) ? AccountJson.Get(AccountJson.Get(AccountJson.Parse(File.ReadAllText(BackupPath)), "statusLine"), "command") as string : null;
                if (!string.IsNullOrWhiteSpace(original)) return RunOriginal(original, input);
                Console.WriteLine("Taskcode" + (snapshot.Remaining[0].HasValue ? " | 5h: " + snapshot.Remaining[0].Value.ToString("0") + "% left" : "") + (snapshot.Remaining[1].HasValue ? " | 7d: " + snapshot.Remaining[1].Value.ToString("0") + "% left" : ""));
                return 0;
            }
            catch (Exception) { return 1; } // Never emit raw session data or credentials to terminal diagnostics.
        }
        internal static string SerializeSnapshot(AccountSnapshot snapshot)
        { return AccountJson.Encode(new { observed_at = (snapshot.ObservedUtc - AccountJson.Epoch).TotalSeconds, rate_limits = new { five_hour = Window(snapshot, 0), seven_day = Window(snapshot, 1) } }); }
        private static int RunOriginal(string command, string input)
        {
            string bash = GitBashPath();
            bool useBash = File.Exists(bash);
            string shell = useBash ? bash : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
            // Match Claude Code's Windows shell choice and pass the original command unchanged.
            var info = new ProcessStartInfo(shell, (useBash ? "-c " : "-NoProfile -NonInteractive -Command ") + QuoteArgument(command)) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = Process.Start(info))
            {
                process.ErrorDataReceived += delegate { }; process.BeginErrorReadLine();
                var output = process.StandardOutput.ReadToEndAsync();
                process.StandardInput.Write(input); process.StandardInput.Close();
                if (!process.WaitForExit(3000)) { process.Kill(); return 1; }
                Console.Write(output.Result); return process.ExitCode;
            }
        }
        private static string QuoteArgument(string value)
        {
            var result = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes); result.Append(c); slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
    }
}
