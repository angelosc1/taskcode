using System;
using System.Globalization;
using System.IO;
using System.Net;

namespace Taskcode.Probe
{
    // Protocol behavior adapted from Codenotch (MIT); credentials stay in the official CLI store.
    internal sealed class ClaudeAccountClient
    {
        private DateTime nextRead, penaltyUntil, lastAttemptUtc, nextIdleProbe;
        private AccountSnapshot last, lastError;
        private string lastHome;
        private int penalties;
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
            if (home != lastHome) { last = lastError = null; nextRead = lastAttemptUtc = nextIdleProbe = DateTime.MinValue; lastHome = home; }
            var now = DateTime.UtcNow;
            if (now < penaltyUntil) return new AccountSnapshot { Failed = true, Status = "Claude is rate limiting usage checks. Retrying after the server cooldown." };
            if (!ShouldRefresh(now, nextRead, lastAttemptUtc, last, activityStarted && SessionActive, nextIdleProbe))
            {
                if (!UsePrivateProfile)
                {
                    var bridge = ClaudeBridge.Read();
                    if ((bridge.Periods[0] != null || bridge.Periods[1] != null) && (last == null || bridge.ObservedUtc > last.ObservedUtc))
                    { bridge.Identity = last == null ? "" : last.Identity; bridge.SignedIn = true; last = bridge; return bridge; }
                }
                if (lastError != null) return lastError;
                if (last == null) return new AccountSnapshot { Status = "Waiting for the next Claude quota check.", Failed = true };
                return last;
            }
            lastAttemptUtc = now; nextRead = now.AddMinutes(5);
            activityStarted = false;
            if (SessionActive) nextIdleProbe = now.AddSeconds(60);
            string token = null;
            foreach (string name in new[] { ".credentials.json", "credentials.json" })
            {
                string path = Path.Combine(home, name);
                if (!File.Exists(path)) continue;
                object root = AccountJson.Parse(File.ReadAllText(path));
                object oauth = AccountJson.Get(root, "claudeAiOauth") ?? root;
                token = AccountJson.Get(oauth, "accessToken") as string;
                if (!string.IsNullOrWhiteSpace(token)) break;
            }
            if (string.IsNullOrWhiteSpace(token))
            {
                if (!UsePrivateProfile) return ClaudeBridge.Read();
                return new AccountSnapshot { Status = "Not signed in. Open Connect Claude and choose Sign in." };
            }
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create("https://api.anthropic.com/api/oauth/usage");
            request.Method = "GET"; request.AllowAutoRedirect = false; request.Timeout = request.ReadWriteTimeout = 15000;
            request.Headers[HttpRequestHeader.Authorization] = "Bearer " + token;
            request.Headers["anthropic-beta"] = "oauth-2025-04-20";
            request.UserAgent = "Taskcode/" + AppInfo.VersionText;
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    char[] buffer = new char[1048577]; int count = reader.ReadBlock(buffer, 0, buffer.Length);
                    if (count > 1048576) throw new InvalidDataException();
                    last = ParseUsage(AccountJson.Parse(new string(buffer, 0, count)), now);
                    string metadata = UsePrivateProfile || Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") != null ? Path.Combine(home, ".claude.json") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json");
                    if (File.Exists(metadata))
                        try { last.Identity = AccountJson.Get(AccountJson.Get(AccountJson.Parse(File.ReadAllText(metadata)), "oauthAccount"), "emailAddress") as string; } catch (ArgumentException) { } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    penalties = 0; lastError = null; return last;
                }
            }
            catch (WebException error)
            {
                var response = error.Response as HttpWebResponse;
                int code = response == null ? 0 : (int)response.StatusCode;
                if (code == 429)
                {
                    double wait = Math.Min(900, 60 * Math.Pow(2, penalties++));
                    double seconds; DateTime date;
                    string retry = response.Headers["Retry-After"];
                    if (double.TryParse(retry, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds)) wait = Math.Max(wait, seconds);
                    else if (DateTime.TryParse(retry, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out date)) wait = Math.Max(wait, (date.ToUniversalTime() - now).TotalSeconds);
                    penaltyUntil = now.AddSeconds(Math.Max(1, Math.Min(wait, 86400))); nextRead = penaltyUntil;
                }
                else nextRead = now.AddSeconds(60);
                if (response != null) response.Dispose();
                nextIdleProbe = nextRead;
                lastError = new AccountSnapshot { Failed = true, Status = code == 401 ? "Claude session expired. Sign in again." : code == 403 ? "Claude denied access to usage data for this session." : code == 429 ? "Claude usage checks are rate limited. Waiting before retrying." : "Claude connection: " + error.Status + ". Retrying automatically." };
                return lastError;
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
}
