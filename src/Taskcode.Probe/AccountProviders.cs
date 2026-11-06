using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Taskcode.Probe
{
    internal static class AccountJson
    {
        internal static Dictionary<string, object> Parse(string text) { return new JavaScriptSerializer { MaxJsonLength = 1048576 }.Deserialize<Dictionary<string, object>>(text); }
        internal static string Encode(object value) { return new JavaScriptSerializer().Serialize(value); }
        internal static Dictionary<string, object> Map(object value) { return value as Dictionary<string, object> ?? new Dictionary<string, object>(); }
        internal static object Get(object value, string key) { object result; return Map(value).TryGetValue(key, out result) ? result : null; }
        internal static double? Number(object value)
        {
            if (value == null || value is bool || value is string) return null;
            double result; return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out result) && !double.IsNaN(result) && !double.IsInfinity(result) ? (double?)result : null;
        }
        internal static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    internal sealed class AccountSnapshot
    {
        internal readonly QuotaPeriod[] Periods = new QuotaPeriod[2];
        internal readonly double?[] Remaining = new double?[2];
        internal DateTime ObservedUtc;
        internal string Status;
        internal bool Failed = false;
        internal bool SignedIn = false;
        internal string Identity;
        internal static AccountSnapshot Codex(object result, DateTime now)
        {
            var snapshot = new AccountSnapshot { ObservedUtc = now, Status = "Connected via Codex CLI" };
            object limits = AccountJson.Get(result, "rateLimits");
            object byId = AccountJson.Get(result, "rateLimitsByLimitId");
            // Prefer the main Codex pool. Never silently mix model-specific quotas.
            if (AccountJson.Get(byId, "codex") != null) limits = AccountJson.Get(byId, "codex");
            foreach (string name in new[] { "primary", "secondary" })
            {
                object window = AccountJson.Get(limits, name);
                double? minutes = AccountJson.Number(AccountJson.Get(window, "windowDurationMins"));
                if (!minutes.HasValue) continue;
                int index = minutes.Value == 300 ? 0 : minutes.Value == 10080 ? 1 : -1;
                if (index >= 0) snapshot.Set(index, AccountJson.Get(window, "usedPercent"), AccountJson.Get(window, "resetsAt"), minutes.Value);
            }
            return snapshot;
        }
        internal static AccountSnapshot Claude(object input, DateTime now)
        {
            var snapshot = new AccountSnapshot { ObservedUtc = now, Status = "Connected via Claude Code" };
            object limits = AccountJson.Get(input, "rate_limits");
            for (int index = 0; index < 2; index++)
            {
                object window = AccountJson.Get(limits, index == 0 ? "five_hour" : "seven_day");
                snapshot.Set(index, AccountJson.Get(window, "used_percentage"), AccountJson.Get(window, "resets_at"), index == 0 ? 300 : 10080, index == 0);
            }
            return snapshot;
        }
        private void Set(int index, object usedValue, object resetValue, double minutes, bool allowWaiting = false)
        {
            double? used = AccountJson.Number(usedValue), reset = AccountJson.Number(resetValue);
            if (!used.HasValue || used < 0 || used > 100) return;
            if (resetValue != null && (!reset.HasValue || reset < 0 || reset > 253402000000)) return;
            if (allowWaiting && used.Value == 0 && (resetValue == null || AccountJson.Epoch.AddSeconds(reset.Value) <= ObservedUtc))
            {
                Remaining[index] = 100;
                Periods[index] = new QuotaPeriod { Remaining = 100, WaitingForFirstRequest = true };
                return;
            }
            if (!reset.HasValue) return;
            DateTime end = AccountJson.Epoch.AddSeconds(reset.Value), start = end.AddMinutes(-minutes);
            if (ObservedUtc < start || ObservedUtc >= end) return;
            Remaining[index] = 100 - used.Value;
            Periods[index] = new QuotaPeriod { Remaining = (int)Math.Round(Remaining[index].Value), StartUtc = start, EndUtc = end };
        }
    }

    // Only the official local app-server owns authentication. Taskcode never reads auth.json.
    internal sealed class CodexAccountClient : IDisposable
    {
        private Process process;
        private int nextId;
        internal bool UsePrivateProfile = false;
        private bool processPrivateProfile;
        private string loginId;
        internal bool LoginPending = false;
        internal bool LoginFailed = false;
        internal static string FindExecutable()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string bundled = Path.Combine(root, @"npm\node_modules\@openai\codex\node_modules\@openai\codex-win32-x64\vendor\x86_64-pc-windows-msvc\bin\codex.exe");
            if (File.Exists(bundled)) return bundled;
            string direct = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".local\bin\codex.exe");
            if (File.Exists(direct)) return direct;
            foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                try { string path = Path.Combine(directory.Trim('"'), "codex.exe"); if (Path.IsPathRooted(path) && File.Exists(path)) return path; }
                catch (ArgumentException) { }
            }
            return null;
        }
        private void EnsureStarted()
        {
            if (process != null && !process.HasExited && processPrivateProfile == UsePrivateProfile) return;
            Dispose();
            string executable = FindExecutable();
            if (executable == null) throw new InvalidOperationException("Install Codex CLI to connect your ChatGPT account.");
            var info = new ProcessStartInfo(executable, "app-server") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            if (UsePrivateProfile)
            {
                Directory.CreateDirectory(AccountPaths.PrivateHome("codex"));
                info.EnvironmentVariables["CODEX_HOME"] = AccountPaths.PrivateHome("codex");
            }
            processPrivateProfile = UsePrivateProfile;
            process = Process.Start(info);
            process.ErrorDataReceived += delegate { }; // Drain diagnostics without storing credentials or raw service responses.
            process.BeginErrorReadLine();
            Request("initialize", new { clientInfo = new { name = "taskcode", title = "Taskcode", version = AppInfo.VersionText } });
            process.StandardInput.WriteLine(AccountJson.Encode(new { method = "initialized" }));
            process.StandardInput.Flush();
        }
        private object Request(string method, object parameters)
        {
            int id = ++nextId;
            process.StandardInput.WriteLine(AccountJson.Encode(new { id = id, method = method, @params = parameters }));
            process.StandardInput.Flush();
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed.TotalSeconds < 20)
            {
                var reader = process.StandardOutput;
                var line = Task.Factory.StartNew(() => reader.ReadLine());
                if (!line.Wait(Math.Max(1, 20000 - (int)deadline.ElapsedMilliseconds))) { Dispose(); throw new TimeoutException(); }
                if (line.Result == null) { Dispose(); throw new IOException("Codex CLI stopped."); }
                var message = AccountJson.Parse(line.Result);
                if ((AccountJson.Get(message, "method") as string) == "account/login/completed")
                {
                    object notification = AccountJson.Get(message, "params");
                    if ((AccountJson.Get(notification, "loginId") as string) == loginId)
                    { LoginPending = false; LoginFailed = !object.Equals(AccountJson.Get(notification, "success"), true); }
                }
                if (AccountJson.Number(AccountJson.Get(message, "id")) != id) continue;
                if (AccountJson.Get(message, "error") != null) throw new InvalidOperationException("Codex could not read this account's subscription limits. Check sign-in in Codex CLI.");
                return AccountJson.Get(message, "result");
            }
            Dispose(); throw new TimeoutException();
        }
        internal AccountSnapshot Read()
        {
            EnsureStarted();
            object account = AccountJson.Get(Request("account/read", new { refreshToken = false }), "account");
            if (account == null) return new AccountSnapshot { Status = "Not signed in. Click Connect Codex." };
            if ((AccountJson.Get(account, "type") as string) == "apiKey") return new AccountSnapshot { Status = "API-key account: ChatGPT subscription quota is unavailable." };
            var snapshot = AccountSnapshot.Codex(Request("account/rateLimits/read", null), DateTime.UtcNow);
            snapshot.SignedIn = true;
            snapshot.Identity = AccountJson.Get(account, "email") as string;
            return snapshot;
        }
        internal string BeginLogin()
        {
            EnsureStarted();
            object result = Request("account/login/start", new { type = "chatgpt" });
            loginId = AccountJson.Get(result, "loginId") as string;
            string url = AccountJson.Get(result, "authUrl") as string;
            LoginPending = true; LoginFailed = false;
            Uri parsed;
            if (!Uri.TryCreate(url, UriKind.Absolute, out parsed) || parsed.Scheme != "https" || !(parsed.Host == "auth.openai.com" || parsed.Host == "chatgpt.com")) throw new InvalidOperationException("Codex returned an unexpected sign-in address.");
            return url;
        }
        internal void CancelLogin() { if (process != null && !process.HasExited && loginId != null) Request("account/login/cancel", new { loginId = loginId }); loginId = null; LoginPending = false; }
        public void Dispose()
        {
            var owned = process; process = null;
            LoginPending = false; loginId = null;
            if (owned == null) return;
            try { if (!owned.HasExited) { owned.StandardInput.Close(); if (!owned.WaitForExit(500)) owned.Kill(); } }
            catch (InvalidOperationException) { }
            catch (IOException) { } // A disconnected app-server pipe must not escape recovery cleanup.
            catch (System.ComponentModel.Win32Exception) { }
            finally { owned.Dispose(); }
        }
    }
}
