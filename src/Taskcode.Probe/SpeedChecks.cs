using System;

namespace Taskcode.Probe
{
    internal static class SpeedChecks
    {
        internal static void Run()
        {
            var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var start = now.AddHours(-3);
            var end = now.AddHours(2);
            var empty = new UsageSpeed();
            Check(empty.EstimateAt(now).State == SpeedState.Calculating, "No history must show Calculating.");
            empty.Observe(now.AddSeconds(-3), 68, start, end);
            empty.Observe(now, 68, start, end);
            Check(empty.EstimateAt(now).State == SpeedState.Calculating, "Two samples must not invent a projection.");
            var safe = Seed(now, 68, start, end, 1.8);
            Check(safe.EstimateAt(now).Level == 1, "Comfortable projection must have one arrow.");
            var near = Seed(now, 68, start, end, 1);
            Check(near.EstimateAt(now).Level == 2, "Projection near reset must have two arrows.");
            var fast = Seed(now, 68, start, end, 0.45);
            Check(fast.EstimateAt(now).Level == 3, "Early exhaustion must have three arrows.");
            Check(Math.Abs(fast.EstimateAt(now).TimeToExhaustion.Value.TotalHours - 0.9) < 0.000001, "Forecast must divide remaining quota by recent rate.");
            Check(fast.EstimateAt(now.AddSeconds(31)).State == SpeedState.Stale, "Stale samples must not present a live forecast.");
            Check(!fast.Observe(now.AddSeconds(-1), 67, start, end), "Out-of-order samples must be ignored.");
            Check(!fast.Observe(now.AddSeconds(1), double.NaN, start, end), "Invalid quota readings must be ignored.");
            Check(fast.EstimateAt(end).State == SpeedState.Expired, "An expired period must stop forecasting.");
            fast.Observe(now.AddSeconds(2), 100, now.AddSeconds(1), now.AddHours(5));
            Check(fast.EstimateAt(now.AddSeconds(2)).State == SpeedState.Calculating, "Reset must discard the previous period's rate.");
            var correction = Seed(now, 68, start, end, 1);
            correction.Observe(now.AddSeconds(1), 69, start, end);
            Check(correction.EstimateAt(now.AddSeconds(1)).State == SpeedState.Calculating, "Quota increases must reset history rather than become negative usage.");
            var idle = new UsageSpeed();
            for (int second = 15; second >= 0; second--) idle.Observe(now.AddSeconds(-second), 68, start, end);
            Check(idle.EstimateAt(now).State == SpeedState.Idle, "No recent consumption must show Idle without arrows.");
            idle.Observe(now.AddSeconds(1), 67, start, end);
            Check(idle.EstimateAt(now.AddSeconds(1)).State == SpeedState.Calculating, "Resuming from Idle must wait for confirmation.");
            idle.Observe(now.AddSeconds(2), 66, start, end);
            Check(idle.EstimateAt(now.AddSeconds(2)).State == SpeedState.Calculating, "A second resumed sample must still wait for confirmation.");
            idle.Observe(now.AddSeconds(3), 65, start, end);
            Check(idle.EstimateAt(now.AddSeconds(3)).Level == 3, "Three resumed samples must activate the new level.");
            var interpolation = new UsageSpeed();
            interpolation.Observe(now.AddSeconds(-20), 100, start, end);
            interpolation.Observe(now.AddSeconds(-10), 90, start, end);
            interpolation.Observe(now, 80, start, end);
            Check(Math.Abs(interpolation.EstimateAt(now).PointsPerHour - 3600) < 0.0001, "Window boundary must interpolate a partial sample interval.");
            var stable = Seed(now, 80, start, end, 20);
            stable.Observe(now.AddSeconds(1), 79, start, end);
            Check(stable.EstimateAt(now.AddSeconds(1)).Level == 1, "One fast reading must not change a stable level.");
            stable.Observe(now.AddSeconds(2), 78, start, end);
            Check(stable.EstimateAt(now.AddSeconds(2)).Level == 1, "Two fast readings must not change a stable level.");
            stable.Observe(now.AddSeconds(3), 77, start, end);
            Check(stable.EstimateAt(now.AddSeconds(3)).Level == 3, "Three confirmed readings must change the level.");
            stable.Observe(now.AddSeconds(40), 77, start, end);
            Check(stable.EstimateAt(now.AddSeconds(40)).State == SpeedState.Calculating, "A long sampling gap must restart calibration.");
            Check(SpeedArrows.OpacityAt(0, 0) > 0.99, "The first filled arrow must start at full brightness.");
            Check(SpeedArrows.OpacityAt(0.2, 1) > SpeedArrows.OpacityAt(0.2, 0), "Brightness must progress from the first arrow to the second.");
            Check(SpeedArrows.OpacityAt(0.4, 2) > SpeedArrows.OpacityAt(0.4, 1), "Brightness must progress from the second arrow to the third.");
            Check(Math.Abs(SpeedArrows.OpacityAt(0.625, 0) - 0.2) < 0.000001, "Fade must keep a faint arrow visible for counting.");
            Check(Math.Abs(SpeedArrows.OpacityAt(1.25, 0) - SpeedArrows.OpacityAt(0, 0)) < 0.000001, "Animation must loop without a brightness discontinuity.");
            var codex = new UsageSpeed(TimeSpan.FromSeconds(10));
            for (int sample = 0; sample <= 4; sample++) codex.Observe(now.AddSeconds(sample * 10), 80 - sample, start, end);
            Check(codex.EstimateAt(now.AddSeconds(40)).State == SpeedState.Active && codex.EstimateAt(now.AddSeconds(40)).WindowSeconds == 20,
                "Ten-second Codex updates must retain enough real samples for a rolling estimate.");
            var claude = new UsageSpeed(TimeSpan.FromMinutes(5));
            claude.Observe(now, 80, start, end);
            Check(claude.EstimateAt(now.AddSeconds(31)).State == SpeedState.Calculating, "Claude must not become stale before its scheduled refresh.");
            claude.Observe(now.AddMinutes(5), 79, start, end);
            Check(claude.EstimateAt(now.AddMinutes(5)).State == SpeedState.Active && Math.Abs(claude.EstimateAt(now.AddMinutes(5)).PointsPerHour - 12) < 0.0001,
                "Sparse Claude readings must estimate measured consumption across their real interval.");
            Check(claude.EstimateAt(now.AddMinutes(10)).State == SpeedState.Active, "A Claude estimate must remain valid through the next scheduled poll.");
            Check(claude.EstimateAt(now.AddMinutes(11).AddSeconds(1)).State == SpeedState.Stale, "Missed Claude refreshes must still expire estimates.");
            Check(!claude.Observe(now.AddMinutes(5), 78, start, end), "Cached Claude data must never count as another speed observation.");
            claude.Observe(now.AddMinutes(12), 77, start, end);
            Check(claude.EstimateAt(now.AddMinutes(12)).State == SpeedState.Calculating, "An actual outage must reset sparse history on recovery.");
        }
        private static UsageSpeed Seed(DateTime now, double remaining, DateTime start, DateTime end, double ratio)
        {
            var result = new UsageSpeed();
            double rate = remaining / ((end - now).TotalHours * ratio);
            for (int second = 15; second >= 0; second--)
                result.Observe(now.AddSeconds(-second), remaining + rate * second / 3600, start, end);
            return result;
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
