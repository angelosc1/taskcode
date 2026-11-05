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
            usage.Apply(1, new AccountSnapshot { Status = "Temporary error", Failed = true });
            Check(usage.DisplayValue(2, now) == 76 && usage.DisplayValue(2, now.AddMinutes(3)) == -1, "Transient errors must preserve recent data, then expire it.");
            usage.Tick(now.AddSeconds(7)); usage.Tick(now.AddSeconds(14));
            Check(usage.Readings[2].Speed.EstimateAt(now.AddSeconds(14)).State == SpeedState.Calculating, "UI ticks must never invent live speed observations.");
            usage.Apply(1, single);
            Check(usage.DisplayValue(2, now) == -1 && usage.DisplayValue(3, now) == 90, "A missing live window must clear its previous value.");
            usage.ClearProvider(1, "Disconnected");
            Check(usage.DisplayValue(3, now) == -1 && usage.Readings[3].PulseUntil == DateTime.MinValue, "Disconnect must clear values and speed hints.");
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
                    report += "\nClaude: " + ClaudeBridge.Read().Status;
                    using (var connections = new AccountConnections())
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
    }
}
