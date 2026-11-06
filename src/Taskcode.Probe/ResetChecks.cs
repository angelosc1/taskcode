using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Taskcode.Probe
{
    internal static class ResetChecks
    {
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static AccountSnapshot Snapshot(DateTime observed, object used, object reset)
        {
            return ClaudeAccountClient.ParseUsage(AccountJson.Parse(AccountJson.Encode(new {
                five_hour = new { utilization = used, resets_at = reset },
                seven_day = new { utilization = 50, resets_at = observed.AddDays(5).ToString("o") }
            })), observed);
        }
        internal static void Run(string directory)
        {
            var now = new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
            var waiting = Snapshot(now, 0, null);
            Check(waiting.Periods[0] != null && waiting.Periods[0].WaitingForFirstRequest && waiting.Remaining[0] == 100,
                "An explicit zero-use Claude session with no reset must be available, not unknown.");
            Check(waiting.Periods[0].StartUtc == DateTime.MinValue && waiting.Periods[0].EndUtc == DateTime.MinValue &&
                waiting.Periods[0].ElapsedAt(now.AddHours(3)) == 0 && waiting.Periods[0].ResetCaptionAt(now.AddHours(3)) == "5h available",
                "Waiting must never synthesize a five-hour countdown or advance elapsed time.");
            Check(!waiting.Periods[1].WaitingForFirstRequest && waiting.Periods[1].EndUtc == now.AddDays(5), "Weekly timing must remain provider-anchored.");
            var expiredZero = Snapshot(now, 0, now.AddSeconds(-1).ToString("o"));
            Check(expiredZero.Periods[0].WaitingForFirstRequest, "A fresh zero-use reading with the retired reset also means waiting for the next request.");
            Check(Snapshot(now, 20, null).Periods[0] == null && Snapshot(now, 0, "invalid").Periods[0] == null,
                "Missing or malformed timing must not invent a zero-use session from nonzero or invalid readings.");
            Check(ClaudeAccountClient.ParseUsage(AccountJson.Parse("{\"five_hour\":null,\"seven_day\":null}"), now).Periods[0] == null,
                "An absent window is distinct from an explicitly reported idle window.");
            var roundTrip = AccountSnapshot.Claude(AccountJson.Parse(ClaudeBridge.SerializeSnapshot(waiting)), now);
            Check(roundTrip.Periods[0].WaitingForFirstRequest, "The local bridge must preserve idle windows without a fake reset timestamp.");
            var usage = new UsageDemo(); usage.BeginLive(); usage.Apply(0, waiting);
            Check(usage.DisplayUsed(0, now) == 0 && usage.DetailSpeedAt(0, now).State == SpeedState.Waiting, "Details must show the waiting state without forecasting arrows.");
            usage.Apply(0, Snapshot(now.AddMinutes(30), 0, null));
            Check(usage.DisplayUsed(0, now.AddMinutes(30)) == 0 && usage.Readings[0].Period.ElapsedAt(now.AddMinutes(30)) == 0,
                "Fresh idle polling must keep all five hours available indefinitely until the provider starts a session.");
            var startedAt = now.AddMinutes(31);
            var started = Snapshot(startedAt, 0, startedAt.AddHours(5).AddMinutes(-6).ToString("o"));
            usage.Apply(0, started);
            Check(!usage.Readings[0].Period.WaitingForFirstRequest && Math.Abs(usage.Readings[0].Period.ElapsedAt(startedAt).Value - .02) < .0001,
                "The first timed response must start the clock from its actual reset, even with zero rounded usage.");
            var boundary = now.AddMinutes(1);
            var old = Snapshot(now, 54, boundary.ToString("o"));
            usage.Apply(0, old);
            Check(usage.ResetPending(0, boundary) && usage.DetailSpeedAt(0, boundary).State == SpeedState.ResetPending && usage.DisplayUsed(0, boundary) == -1,
                "Cached expiry must request confirmation, never manufacture zero use or a new countdown.");
            Check(ClaudeAccountClient.ShouldRefresh(boundary, now.AddMinutes(5), now, old, false, DateTime.MinValue), "Crossing a known reset must bypass the ordinary cache interval once.");
            Check(!ClaudeAccountClient.ShouldRefresh(boundary.AddSeconds(1), boundary.AddMinutes(1), boundary, old, false, DateTime.MinValue),
                "A failed boundary request must honor retry delay, not retry each UI tick.");
            Check(ClaudeAccountClient.ShouldRefresh(now.AddSeconds(10), now.AddMinutes(5), now, waiting, true, DateTime.MinValue) &&
                !ClaudeAccountClient.ShouldRefresh(now.AddSeconds(10), now.AddMinutes(5), now, waiting, true, now.AddMinutes(1)),
                "Local activity may recheck an idle session, with bounded retry cadence.");
            var canonical = ClaudeAccountClient.ParseUsage(AccountJson.Parse(AccountJson.Encode(new {
                five_hour = new { utilization = 0, resets_at = (string)null },
                limits = new[] { new { kind = "weekly_all", percent = 50, resets_at = now.AddDays(5).ToString("o") }, new { kind = "weekly_scoped", percent = 80, resets_at = now.AddDays(4).ToString("o") } }
            })), now);
            Check(canonical.Periods[0].WaitingForFirstRequest && canonical.Periods[1].Remaining == 50,
                "Canonical weekly data must retain the idle named session fallback and ignore scoped model limits.");
            Render(directory);
            usage.Apply(0, Snapshot(now, 100, now.AddHours(1).ToString("o")));
            foreach (int activity in new[] { 0, 1 })
            {
                usage.ActivityKey = activity;
                var exhausted = usage.DetailSpeedAt(0, now);
                using (var readout = new SpeedReadout { Estimate = exhausted })
                    Check(exhausted.State == SpeedState.Exhausted && exhausted.Summary == "Quota exhausted" && !readout.HasArrows,
                        "Exhausted quotas must show a stable status without forecasting arrows, even while execution is active.");
            }
        }
        private static void Render(string directory)
        {
            var now = DateTime.UtcNow;
            var snapshots = new[] { Snapshot(now.AddSeconds(-10), 54, now.AddSeconds(-1).ToString("o")), Snapshot(now, 0, null), Snapshot(now, 0, now.AddHours(5).AddMinutes(-6).ToString("o")) };
            using (var board = new Bitmap(900, 300))
            using (var graphics = Graphics.FromImage(board))
            using (var ink = new SolidBrush(TaskbarTheme.Create(true).Text))
            using (var font = new Font("Segoe UI", 9f))
            {
                graphics.Clear(TaskbarTheme.Create(true).PreviewBackground);
                for (int index = 0; index < snapshots.Length; index++)
                {
                    var model = new UsageDemo(); model.BeginLive(); model.Apply(0, snapshots[index]);
                    using (var card = new DetailWindow(model) { Location = new Point(-30000, -30000) })
                    using (var surface = new Bitmap(card.Width, card.Height))
                    {
                        card.Show(); card.RefreshUsage(); card.DrawToBitmap(surface, card.ClientRectangle);
                        graphics.DrawImageUnscaled(surface, index * 300, 24);
                        graphics.DrawString(new[] { "Reset: refreshing", "Ready: no request yet", "After first request" }[index], font, ink, index * 300 + 12, 2);
                    }
                }
                board.Save(Path.Combine(directory, "reset-check.png"), ImageFormat.Png);
            }
        }
    }
}
