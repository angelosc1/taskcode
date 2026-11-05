using System;
using System.Globalization;
using System.IO;
using System.Net;

namespace Taskcode.Probe
{
    // Protocol behavior adapted from Codenotch (MIT); credentials stay in the official CLI store.
    internal sealed class ClaudeAccountClient
    {
        private DateTime nextRead, penaltyUntil;
        private AccountSnapshot last;
        private string lastHome;
        private int penalties;
        internal bool UsePrivateProfile;
        internal void Refresh() { nextRead = DateTime.MinValue; }
        internal AccountSnapshot Read()
        {
            string home = AccountPaths.ClaudeHome(UsePrivateProfile);
            if (home != lastHome) { last = null; nextRead = DateTime.MinValue; lastHome = home; }
            var now = DateTime.UtcNow;
            if (now < penaltyUntil) return new AccountSnapshot { Failed = true, Status = "Claude is rate limiting usage checks. Retrying after the server cooldown." };
            if (now < nextRead && last != null)
            {
                if (!UsePrivateProfile)
                {
                    var bridge = ClaudeBridge.Read();
                    if (bridge.ObservedUtc > last.ObservedUtc) { bridge.Identity = last.Identity; bridge.SignedIn = true; return bridge; }
                }
                return last;
            }
            nextRead = now.AddMinutes(5);
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
            request.UserAgent = "Taskcode/0.3.0";
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
                    penalties = 0; return last;
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
                return new AccountSnapshot { Failed = true, Status = code == 401 ? "Claude session expired. Sign in again." : code == 403 ? "Claude denied access to usage data for this session." : code == 429 ? "Claude usage checks are rate limited. Waiting before retrying." : "Claude connection: " + error.Status + ". Retrying automatically." };
            }
        }
        internal static AccountSnapshot ParseUsage(object data, DateTime now)
        {
            var limits = new System.Collections.Generic.Dictionary<string, object>();
            foreach (string key in new[] { "five_hour", "seven_day" })
            {
                object window = AccountJson.Get(data, key);
                DateTime reset;
                string value = AccountJson.Get(window, "resets_at") as string;
                if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out reset))
                    limits[key] = new { used_percentage = AccountJson.Get(window, "utilization"), resets_at = (reset - AccountJson.Epoch).TotalSeconds };
            }
            var snapshot = AccountSnapshot.Claude(AccountJson.Parse(AccountJson.Encode(new { rate_limits = limits })), now);
            snapshot.Status = "Connected to Claude / Live subscription quota"; snapshot.SignedIn = true;
            return snapshot;
        }
    }
}
