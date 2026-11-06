using System;
using System.IO;

namespace Taskcode.Probe
{
    internal static class AccountChecks
    {
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        internal static void Run()
        {
            DateTime now = DateTime.UtcNow;
            double reset = (now.AddHours(2) - AccountJson.Epoch).TotalSeconds;
            object codex = new { rateLimits = new { primary = new { usedPercent = 23.5, windowDurationMins = 300, resetsAt = reset }, secondary = new { usedPercent = 71, windowDurationMins = 10080, resetsAt = reset } } };
            var snapshot = AccountSnapshot.Codex(AccountJson.Parse(AccountJson.Encode(codex)), now);
            Check(snapshot.Periods[0].Remaining == 76 && snapshot.Remaining[0] == 76.5 && snapshot.Periods[1].Remaining == 29, "Quota conversion must preserve fractional observations and convert used to remaining.");
            Check(snapshot.Periods[0].EndUtc - snapshot.Periods[0].StartUtc == TimeSpan.FromHours(5), "Codex reset timestamp must anchor the time marker.");
            object weekly = new { rateLimits = new { primary = new { usedPercent = 10, windowDurationMins = 10080, resetsAt = reset } } };
            var single = AccountSnapshot.Codex(AccountJson.Parse(AccountJson.Encode(weekly)), now);
            Check(single.Periods[0] == null && single.Periods[1].Remaining == 90, "A primary weekly window must never be mislabeled as five hours.");
            var invalid = AccountSnapshot.Codex(AccountJson.Parse("{\"rateLimits\":{\"primary\":{\"usedPercent\":-1,\"windowDurationMins\":300,\"resetsAt\":0}}}"), now);
            Check(invalid.Periods[0] == null, "Invalid or expired windows must remain unavailable.");
            var empty = AccountSnapshot.Claude(AccountJson.Parse("{\"context_window\":{\"used_percentage\":80},\"secret\":\"never-cache-this\"}"), now);
            Check(empty.Periods[0] == null && empty.Periods[1] == null, "Context usage must never be confused with subscription usage.");
            var claude = AccountSnapshot.Claude(AccountJson.Parse(AccountJson.Encode(new { secret = "never-cache-this", rate_limits = new { five_hour = new { used_percentage = 40.25, resets_at = reset } } })), now);
            string cache = ClaudeBridge.SerializeSnapshot(claude);
            Check(!cache.Contains("secret") && !cache.Contains("never-cache-this") && cache.Contains("used_percentage"), "Claude cache must whitelist quota fields only.");
            var roundTrip = AccountSnapshot.Claude(AccountJson.Parse(cache), now);
            Check(roundTrip.Remaining[0] == 59.75 && roundTrip.Periods[1] == null, "Claude cache must retain fractional quota and missing windows.");
            var usage = new UsageDemo(); usage.BeginLive();
            Check(usage.DisplayValue(0, now) == -1 && usage.DisplayValue(2, now) == -1, "Live mode must begin with unavailable values, never demo values.");
            usage.Apply(1, snapshot);
            Check(usage.DisplayValue(2, now) == 76 && usage.DisplayValue(0, now) == -1, "Each provider must update independently.");
            usage.ActivityKey = 2;
            Check(usage.DetailSpeedAt(2, now).State == SpeedState.Active && usage.DetailSpeedAt(2, now).PeriodAverage,
                "A running provider may display its initial period-average estimate.");
            usage.ActivityKey = 0;
            Check(usage.DetailSpeedAt(2, now).State == SpeedState.Paused && usage.DetailSpeedAt(2, now).Level == 0 && !usage.DetailSpeedAt(2, now).TimeToExhaustion.HasValue,
                "Stopping a provider must hide forecast arrows and time without waiting for a new quota poll.");
            usage.ActivityKey = 1;
            Check(usage.DetailSpeedAt(2, now).State == SpeedState.Paused, "Claude activity must not animate Codex forecasts.");
            usage.ActivityKey = 2;
            Check(usage.DetailSpeedAt(2, now).State == SpeedState.Active && usage.Readings[2].Speed.EstimateAt(now).State == SpeedState.Calculating,
                "Resuming must restore presentation without adding or discarding consumption samples.");
            usage.Apply(1, new AccountSnapshot { Status = "Temporary error", Failed = true });
            Check(usage.DisplayValue(2, now) == 76 && usage.DisplayValue(2, now.AddMinutes(3)) == -1, "Transient errors must preserve recent data, then expire it.");
            usage.Tick(now.AddSeconds(7)); usage.Tick(now.AddSeconds(14));
            Check(usage.Readings[2].Speed.EstimateAt(now.AddSeconds(14)).State == SpeedState.Calculating, "UI ticks must never invent live speed observations.");
            usage.Apply(1, single);
            Check(usage.DisplayValue(2, now) == -1 && usage.DisplayValue(3, now) == 90, "A missing live window must clear its previous value.");
            usage.ClearProvider(1, "Disconnected");
            Check(usage.DisplayValue(3, now) == -1 && usage.Readings[3].PulseUntil == DateTime.MinValue, "Disconnect must clear values and speed hints.");
            var api = ClaudeAccountClient.ParseUsage(AccountJson.Parse(AccountJson.Encode(new { five_hour = new { utilization = 25.5, resets_at = now.AddHours(2).ToString("o") }, seven_day = new { utilization = 60, resets_at = now.AddDays(4).ToString("o") } })), now);
            Check(api.Remaining[0] == 74.5 && api.Periods[1].Remaining == 40 && api.SignedIn, "Claude API usage must convert utilization and ISO reset times independently.");
            Check(ClaudeAccountClient.ParseUsage(AccountJson.Parse("{\"five_hour\":null,\"seven_day\":null}"), now).Periods[0] == null, "Missing Claude API windows must remain unavailable.");
            var login = ClaudeSignIn.LoginInfo(@"C:\fixture with spaces\O'Brien\claude.exe", @"C:\private account");
            Check(!login.Arguments.Contains("O'Brien") && login.EnvironmentVariables["TASKCODE_AUTH_CLI"].Contains("O'Brien"), "CLI paths must be data, never interpolated shell code.");
            Check(login.EnvironmentVariables["CLAUDE_CONFIG_DIR"] == @"C:\private account" && !login.CreateNoWindow, "Claude sign-in must use the selected profile and a visible interactive official login.");
            Check(AccountPaths.PrivateHome("codex") != Environment.GetEnvironmentVariable("CODEX_HOME"), "Taskcode sign-in must have its own Codex credential directory.");
            Check(!AccountJson.Encode(api).Contains("Bearer"), "Snapshots must not retain authentication headers.");
        }
        internal static int Probe(bool installClaude)
        {
            string report;
            try
            {
                if (installClaude) { ClaudeBridge.Install(); report = "Claude bridge connected. Waiting for Claude Code quota events."; }
                else
                {
                    using (var client = new CodexAccountClient())
                    {
                        var snapshot = client.Read();
                        report = "Codex: " + snapshot.Status;
                        for (int index = 0; index < 2; index++) report += "\n" + (index == 0 ? "5h" : "Weekly") + ": " + (snapshot.Periods[index] == null ? "unavailable" : snapshot.Periods[index].Remaining + "% remaining");
                    }
                    var claude = new ClaudeAccountClient().Read();
                    report += "\nClaude: " + claude.Status;
                    for (int index = 0; index < 2; index++) report += "\nClaude " + (index == 0 ? "5h" : "Weekly") + ": " + (claude.Periods[index] == null ? "unavailable" : claude.Periods[index].Remaining + "% remaining");
                    using (var connections = new AccountConnections { ClaudeEnabled = false })
                    {
                        var usage = new UsageDemo(); usage.BeginLive();
                        var deadline = System.Diagnostics.Stopwatch.StartNew();
                        while (usage.DisplayValue(2, DateTime.UtcNow) < 0 && deadline.Elapsed.TotalSeconds < 22)
                        { connections.Tick(usage); System.Threading.Thread.Sleep(100); }
                        connections.Tick(usage);
                        report += "\nBackground refresh: " + (usage.DisplayValue(2, DateTime.UtcNow) >= 0 ? "live Codex quota confirmed" : usage.AccountStatus[1]);
                        // Offscreen render with live/unknown values; no desktop capture or session data.
                        using (var bitmap = new System.Drawing.Bitmap(420, 100))
                        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                        {
                            graphics.Clear(System.Drawing.Color.FromArgb(32, 35, 40));
                            IndicatorView.Draw(graphics, new System.Drawing.Size(420, 100), IndicatorStyle.Blocks, TaskbarTheme.Create(true), true, 0, usage);
                            bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "live-indicator-check.png"), System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                }
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "accounts-check.txt"), report); return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "accounts-check.txt"), "Account check failed: " + ex.GetType().Name + ". Check CLI installation, sign-in, and connectivity."); return 1;
            }
        }
        internal static int ProbeActivity()
        {
            try
            {
                int key = new LocalActivity().Read(DateTime.UtcNow, true, true);
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "activity-check.txt"), "Local execution signals: Claude=" + ((key & 1) != 0 ? "working" : "idle/unavailable") + "; Codex=" + ((key & 2) != 0 ? "working" : "idle/unavailable"));
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "activity-check.txt"), "Activity unavailable: " + ex.GetType().Name); return 1; }
        }
        internal static int ProbeLoginFlow()
        {
            try
            {
                using (var client = new CodexAccountClient { UsePrivateProfile = true })
                {
                    string url = client.BeginLogin();
                    Check(new Uri(url).Scheme == "https", "The Codex sign-in flow must return an HTTPS browser URL.");
                    client.CancelLogin();
                }
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "login-flow-check.txt"), "PASS: official Codex browser sign-in started and cancelled using Taskcode's isolated profile; no browser opened and no account authenticated.\nClaude CLI: " + (AccountPaths.FindClaude() == null ? "missing" : "available")); return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "login-flow-check.txt"), "FAIL: " + ex.GetType().Name + ". Browser login could not start."); return 1; }
        }
    }
}
