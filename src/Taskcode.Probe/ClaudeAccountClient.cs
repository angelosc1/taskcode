using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;

namespace Taskcode.Probe
{
    // Protocol behavior adapted from Codenotch (MIT); credentials stay in the official CLI store.
    internal sealed class ClaudeAccountClient
    {
        // Codenotch behavior: renew through the official CLI shortly before expiry, never send an
        // expired token (the endpoint answers those with a long 429), and keep the last reading on errors.
        private static readonly TimeSpan RenewMargin = TimeSpan.FromMinutes(4), RenewCooldown = TimeSpan.FromMinutes(10), RenewRetryCap = TimeSpan.FromHours(1);
        private DateTime nextRead, penaltyUntil, lastAttemptUtc, nextIdleProbe;
        private AccountSnapshot last, lastError;
        private string lastHome, lastToken;
        private int penalties, renewFailures;
        private ClaudeCredential credential;
        private DateTime credentialWrite;
        private DateTime renewNotBefore;
        private volatile bool renewing;
        internal bool UsePrivateProfile;
        private bool sessionActive, activityStarted;
        internal bool SessionActive
        {
            get { return sessionActive; }
            set { if (value && !sessionActive) activityStarted = true; sessionActive = value; }
        }
        internal void Refresh() { nextRead = DateTime.MinValue; }
        internal AccountSnapshot Read()
        {
            string home = AccountPaths.ClaudeHome(UsePrivateProfile);
            if (home != lastHome) { last = lastError = null; credential = null; lastToken = null; credentialWrite = DateTime.MinValue; nextRead = lastAttemptUtc = nextIdleProbe = penaltyUntil = renewNotBefore = DateTime.MinValue; penalties = renewFailures = 0; lastHome = home; }
            var now = DateTime.UtcNow;
            var current = LoadCredential(home);
            if (current != null && current.Token != lastToken)
            {
                // A token renewed by Claude Code clears any cooldown earned by the previous one.
                if (lastToken != null) { penaltyUntil = DateTime.MinValue; penalties = 0; nextRead = DateTime.MinValue; lastError = null; }
                lastToken = current.Token;
            }
            Renew(home, current, now);
            if (now < penaltyUntil) return Hold("Claude is rate limiting usage checks. Retrying after the server cooldown.");
            if (!ShouldRefresh(now, nextRead, lastAttemptUtc, last, activityStarted && SessionActive, nextIdleProbe))
            {
                if (!UsePrivateProfile)
                {
                    var bridge = ClaudeBridge.Read();
                    if ((bridge.Periods[0] != null || bridge.Periods[1] != null) && (last == null || bridge.ObservedUtc > last.ObservedUtc))
                    { bridge.Identity = last == null ? "" : last.Identity; bridge.SignedIn = true; last = bridge; lastError = null; return bridge; }
                }
                if (lastError != null) return lastError;
                if (last == null) return new AccountSnapshot { Status = "Waiting for the next Claude quota check.", Failed = true };
                return last;
            }
            lastAttemptUtc = now; nextRead = now.AddMinutes(5);
            activityStarted = false;
            if (SessionActive) nextIdleProbe = now.AddSeconds(60);
            if (current == null)
            {
                if (!UsePrivateProfile) return ClaudeBridge.Read();
                return new AccountSnapshot { Status = "Not signed in. Open Connect Claude and choose Sign in." };
            }
            if (current.Expired(now))
            {
                nextRead = now.AddSeconds(30);
                lastError = Hold(AccountPaths.FindClaude() == null ? "Claude session expired. Run claude once in a terminal to renew it." : "Claude session expired. Renewing it with Claude Code...");
                return lastError;
            }
            try { return Fetch(current.Token, now); }
            catch (WebException error)
            {
                var response = error.Response as HttpWebResponse;
                int code = response == null ? 0 : (int)response.StatusCode;
                string retry = response == null ? null : response.Headers["Retry-After"];
                if (response != null) response.Dispose();
                if (code == 401)
                {
                    // The CLI may have renewed the token in the meantime: retry once with the new one.
                    credentialWrite = DateTime.MinValue;
                    var fresh = LoadCredential(home);
                    if (fresh != null && fresh.Token != current.Token && !fresh.Expired(now))
                    {
                        lastToken = fresh.Token;
                        try { return Fetch(fresh.Token, now); }
                        catch (WebException second) { var again = second.Response as HttpWebResponse; code = again == null ? 0 : (int)again.StatusCode; retry = again == null ? null : again.Headers["Retry-After"]; if (again != null) again.Dispose(); }
                    }
                }
                if (code == 429)
                {
                    double wait = Math.Min(900, 60 * Math.Pow(2, Math.Min(4, penalties++)));
                    double seconds; DateTime date;
                    if (double.TryParse(retry, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds)) wait = Math.Max(wait, seconds);
                    else if (DateTime.TryParse(retry, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out date)) wait = Math.Max(wait, (date.ToUniversalTime() - now).TotalSeconds);
                    penaltyUntil = now.AddSeconds(Math.Max(1, Math.Min(wait, 86400))); nextRead = penaltyUntil;
                }
                else nextRead = now.AddSeconds(60);
                nextIdleProbe = nextRead;
                lastError = Hold(code == 401 ? "Claude session expired. Sign in again." : code == 403 ? "Claude denied access to usage data for this session." : code == 429 ? "Claude usage checks are rate limited. Waiting before retrying." : "Claude connection: " + error.Status + ". Retrying automatically.");
                return lastError;
            }
        }
        private AccountSnapshot Fetch(string token, DateTime now)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create("https://api.anthropic.com/api/oauth/usage");
            request.Method = "GET"; request.AllowAutoRedirect = false; request.Timeout = request.ReadWriteTimeout = 15000;
            request.Headers[HttpRequestHeader.Authorization] = "Bearer " + token;
            request.Headers["anthropic-beta"] = "oauth-2025-04-20";
            request.UserAgent = "Taskcode/" + AppInfo.VersionText;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                char[] buffer = new char[1048577]; int count = reader.ReadBlock(buffer, 0, buffer.Length);
                if (count > 1048576) throw new InvalidDataException();
                last = ParseUsage(AccountJson.Parse(new string(buffer, 0, count)), now);
                string metadata = UsePrivateProfile || Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") != null ? Path.Combine(lastHome, ".claude.json") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json");
                if (File.Exists(metadata))
                    try { last.Identity = AccountJson.Get(AccountJson.Get(AccountJson.Parse(File.ReadAllText(metadata)), "oauthAccount"), "emailAddress") as string; } catch (ArgumentException) { } catch (IOException) { } catch (UnauthorizedAccessException) { }
                penalties = 0; lastError = null; return last;
            }
        }
        // Keep showing the last good reading while a check fails; only the status message changes.
        private AccountSnapshot Hold(string message)
        {
            if (last != null && !last.Failed && last.SignedIn) { last.Status = message; return last; }
            return new AccountSnapshot { Failed = true, Status = message };
        }
        private ClaudeCredential LoadCredential(string home)
        {
            foreach (string name in new[] { ".credentials.json", "credentials.json" })
            {
                string path = Path.Combine(home, name);
                if (!File.Exists(path)) continue;
                try
                {
                    var write = File.GetLastWriteTimeUtc(path);
                    if (credential != null && write == credentialWrite) return credential;
                    var parsed = ClaudeCredential.Parse(File.ReadAllText(path));
                    if (parsed == null) continue;
                    credential = parsed; credentialWrite = write; return parsed;
                }
                catch (IOException) { return credential; } // The CLI may be rewriting the file; keep the previous read.
                catch (UnauthorizedAccessException) { return credential; }
                catch (ArgumentException) { return credential; }
            }
            return null;
        }
        private void Renew(string home, ClaudeCredential current, DateTime now)
        {
            if (renewing || current == null || !current.ExpiresUtc.HasValue || now < renewNotBefore || current.ExpiresUtc.Value - now > RenewMargin) return;
            string executable = AccountPaths.FindClaude();
            if (executable == null) return;
            renewing = true;
            DateTime before = current.ExpiresUtc.Value; bool ownProfile = UsePrivateProfile;
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool renewed = false;
                try
                {
                    RunRenewal(executable, home, ownProfile);
                    foreach (string name in new[] { ".credentials.json", "credentials.json" })
                    {
                        string path = Path.Combine(home, name);
                        if (!File.Exists(path)) continue;
                        var after = ClaudeCredential.Parse(File.ReadAllText(path));
                        if (after != null) { renewed = after.ExpiresUtc.HasValue && after.ExpiresUtc.Value > before; break; }
                    }
                }
                catch (Exception) { }
                finally
                {
                    if (renewed) { renewFailures = 0; renewNotBefore = DateTime.UtcNow + RenewCooldown; nextRead = DateTime.MinValue; }
                    else renewNotBefore = DateTime.UtcNow.AddTicks(Math.Min(RenewRetryCap.Ticks, RenewCooldown.Ticks << Math.Min(3, renewFailures++)));
                    credentialWrite = DateTime.MinValue; renewing = false;
                }
            });
        }
        // Starting the official CLI lets it refresh its own credential file; no prompt is sent.
        private static void RunRenewal(string executable, string home, bool ownProfile)
        {
            var info = new ProcessStartInfo(executable, "-p") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) };
            foreach (string key in new System.Collections.Generic.List<string>(info.EnvironmentVariables.Keys.CastStrings()))
                if (key == "CLAUDECODE" || key.StartsWith("CLAUDE_CODE_", StringComparison.OrdinalIgnoreCase) || key == "ANTHROPIC_API_KEY" || key == "ANTHROPIC_AUTH_TOKEN") info.EnvironmentVariables.Remove(key);
            if (ownProfile) info.EnvironmentVariables["CLAUDE_CONFIG_DIR"] = home;
            using (var process = Process.Start(info))
            using (var job = new ProviderProcessJob(process))
            {
                process.StandardInput.Close();
                process.OutputDataReceived += delegate { }; process.ErrorDataReceived += delegate { };
                process.BeginOutputReadLine(); process.BeginErrorReadLine();
                if (!process.WaitForExit(30000)) { try { process.Kill(); } catch (InvalidOperationException) { } }
            }
        }
        internal static bool ShouldRefresh(DateTime now, DateTime nextRead, DateTime lastAttempt, AccountSnapshot snapshot, bool active, DateTime nextIdleProbe)
        {
            if (now >= nextRead) return true;
            if (snapshot == null) return false;
            foreach (var period in snapshot.Periods)
                if (period != null && !period.WaitingForFirstRequest && period.EndUtc > lastAttempt && period.EndUtc <= now) return true;
            return active && snapshot.Periods[0] != null && snapshot.Periods[0].WaitingForFirstRequest && now >= nextIdleProbe;
        }
        private static object NormalizeWindow(object used, object reset)
        {
            DateTime date;
            string text = reset as string;
            object normalized = text != null && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out date) ? (object)(date - AccountJson.Epoch).TotalSeconds : reset;
            return new System.Collections.Generic.Dictionary<string, object> { { "used_percentage", used }, { "resets_at", normalized } };
        }
        internal static AccountSnapshot ParseUsage(object data, DateTime now)
        {
            var limits = new System.Collections.Generic.Dictionary<string, object>();
            foreach (string key in new[] { "five_hour", "seven_day" })
            {
                object window = AccountJson.Get(data, key);
                if (window != null) limits[key] = NormalizeWindow(AccountJson.Get(window, "utilization"), AccountJson.Get(window, "resets_at"));
            }
            // Canonical windows win when valid; named fields still cover the idle
            // session that disappears from limits immediately after rollover.
            foreach (object window in AccountJson.Get(data, "limits") as System.Collections.IEnumerable ?? new object[0])
            {
                string kind = AccountJson.Get(window, "kind") as string;
                string key = kind == "session" || kind == "five_hour" ? "five_hour" :
                    kind == "weekly_all" || kind == "weekly" || kind == "seven_day" ? "seven_day" : null;
                if (key == null) continue;
                var candidate = NormalizeWindow(AccountJson.Get(window, "percent"), AccountJson.Get(window, "resets_at"));
                var single = new System.Collections.Generic.Dictionary<string, object> { { key, candidate } };
                var check = AccountSnapshot.Claude(new System.Collections.Generic.Dictionary<string, object> { { "rate_limits", single } }, now);
                if (check.Periods[key == "five_hour" ? 0 : 1] != null) limits[key] = candidate;
            }
            var snapshot = AccountSnapshot.Claude(new System.Collections.Generic.Dictionary<string, object> { { "rate_limits", limits } }, now);
            snapshot.Status = "Connected to Claude / Live subscription quota"; snapshot.SignedIn = true;
            return snapshot;
        }
    }
    internal sealed class ClaudeCredential
    {
        internal string Token;
        internal DateTime? ExpiresUtc;
        internal bool Expired(DateTime now) { return ExpiresUtc.HasValue && ExpiresUtc.Value <= now; }
        internal static ClaudeCredential Parse(string text)
        {
            object root = AccountJson.Parse(text);
            object oauth = AccountJson.Get(root, "claudeAiOauth") ?? root;
            string token = AccountJson.Get(oauth, "accessToken") as string;
            if (string.IsNullOrWhiteSpace(token)) return null;
            double? expires = AccountJson.Number(AccountJson.Get(oauth, "expiresAt"));
            return new ClaudeCredential { Token = token, ExpiresUtc = expires.HasValue && expires > 0 && expires < 4102444800000 ? (DateTime?)AccountJson.Epoch.AddMilliseconds(expires.Value) : null };
        }
    }
}
