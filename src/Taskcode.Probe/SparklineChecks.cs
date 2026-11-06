using System;
using System.Drawing;
using System.IO;

namespace Taskcode.Probe
{
    internal static class SparklineChecks
    {
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        internal static void Run(string directory)
        {
            var now = DateTime.UtcNow;
            var trend = new QuotaTrend();
            Check(trend.Observe(now.AddMinutes(-5), 10, "period") && !trend.Observe(now.AddMinutes(-5), 90, "period"), "Cached observations must never become new Sparkline points.");
            Check(trend.Observe(now.AddMinutes(-4), 11, "period") && trend.Observe(now, 15, "period"), "Fresh fractional quota readings must create history.");
            var bounds = new RectangleF(0, 0, 100, 20); var points = IndicatorView.SparklinePoints(trend.PointsAt(now), bounds);
            Check(points.Length == 3 && Math.Abs(points[1].X - 20) < .001 && points[2].Y < points[0].Y, "Graph time spacing must match actual irregular observation times, and rising use must rise visually.");
            Check(trend.PointsAt(now.AddMinutes(7)).Length == 0, "Stale history must not look live.");
            trend.Observe(now.AddSeconds(1), 0, "new period"); Check(trend.PointsAt(now.AddSeconds(1)).Length == 1, "Rollover must reset the history instead of drawing a false negative trend.");
            trend.Observe(now.AddSeconds(2), 8, "new period"); trend.Observe(now.AddSeconds(3), 6, "new period");
            Check(trend.PointsAt(now.AddSeconds(3)).Length == 1, "A corrected lower quota must start a fresh baseline.");
            trend.Observe(now.AddMinutes(10), 8, "new period"); Check(trend.PointsAt(now.AddMinutes(10)).Length == 1, "Long source outages must break the line instead of inventing intermediate observations.");
            Check(!trend.Observe(now.AddMinutes(11), double.NaN, "new period") && !trend.Observe(now.AddMinutes(11), -1, "new period"), "Invalid quota values must not enter history.");
            trend.Clear();
            for (int index = 0; index < 200; index++) trend.Observe(now.AddSeconds(index * 10), index / 2.0, "bounded");
            Check(trend.PointsAt(now.AddSeconds(1990)).Length <= 120 && trend.PointsAt(now.AddSeconds(1990))[0].At >= now.AddSeconds(1990).AddMinutes(-30), "History must remain bounded in both time and memory.");
            var live = new UsageDemo(); live.BeginLive();
            Check(live.Readings[0].Trend.PointsAt(now).Length == 0, "Live mode must never inherit offline demo history.");
            var snapshot = ClaudeAccountClient.ParseUsage(AccountJson.Parse("{\"five_hour\":{\"utilization\":10.25,\"resets_at\":\"" + now.AddHours(2).ToString("o") + "\"}}"), now);
            live.Apply(0, snapshot); live.Apply(0, snapshot);
            Check(live.Readings[0].Trend.PointsAt(now).Length == 1 && live.Readings[0].Trend.PointsAt(now)[0].Used == 10.25, "Actual quota application must preserve precision without sampling cached results twice.");
            live.Apply(0, new AccountSnapshot { Failed = true, Status = "fixture" }); Check(live.Readings[0].Trend.PointsAt(now).Length == 0, "Failed source reads must clear live history.");
            var optional = ProviderCatalog.Create()[0]; optional.Enabled = true;
            var first = new OptionalSnapshot { ObservedUtc = now.AddMinutes(-5) }; first.Quotas.Add(new OptionalQuota { Name = "Included usage", Used = 10 }); first.Quotas.Add(new OptionalQuota { Name = "API usage", Used = 20 }); optional.Apply(first);
            var second = new OptionalSnapshot { ObservedUtc = now }; second.Quotas.Add(new OptionalQuota { Name = "Included usage", Used = 12 }); second.Quotas.Add(new OptionalQuota { Name = "API usage", Used = 23 }); optional.Apply(second);
            Check(optional.Trends["Included usage"].PointsAt(now).Length == 2 && optional.Trends["API usage"].PointsAt(now)[1].Used == 23, "Optional quota histories must stay separate by window.");
            var model = new UsageDemo { OptionalProviders = new[] { optional } }; string key = IndicatorView.SparklineDataKey(model, now);
            second = new OptionalSnapshot { ObservedUtc = now.AddSeconds(1) }; second.Quotas.Add(new OptionalQuota { Name = "Included usage", Used = 12 }); second.Quotas.Add(new OptionalQuota { Name = "API usage", Used = 24 }); optional.Apply(second);
            Check(key != IndicatorView.SparklineDataKey(model, now.AddSeconds(1)), "Secondary-only trend updates must invalidate taskbar rendering.");
            Render(directory);
        }
        private static void Render(string directory)
        {
            var palette = TaskbarTheme.Create(true);
            using (var board = new Bitmap(700, 490))
            using (var graphics = Graphics.FromImage(board))
            using (var title = new Font("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var ink = new SolidBrush(palette.Text))
            using (var muted = new SolidBrush(palette.Muted))
            {
                graphics.Clear(palette.PreviewBackground); graphics.DrawString("Sparkline / Taskcode", title, ink, 20, 14);
                for (int row = 0; row < 6; row++)
                {
                    bool weekly = row == 1 || row == 3, colors = row == 2 || row == 3;
                    var model = new UsageDemo();
                    if (colors) { model.Readings[0].Period.Remaining = 75; model.Readings[2].Period.Remaining = 15; }
                    var now = DateTime.UtcNow;
                    for (int index = 0; index < 4; index++)
                    {
                        var reading = model.Readings[index]; reading.Trend.Clear();
                        for (int point = row == 4 ? 0 : 8; point >= 0; point--)
                            reading.Trend.Observe(now.AddMinutes(-point * 2), Math.Max(0, 100 - reading.Period.Remaining - point * (index % 2 == 0 ? .5 : .2) - (point % 2) * .2), "fixture");
                    }
                    if (row == 5) model.CoreEnabled[0] = false;
                    string label = row == 0 ? "5h / provider colors" : row == 1 ? "5h + 7d / independent histories" : row == 2 ? "5h / usage colors" : row == 3 ? "5h + 7d / usage colors" : row == 4 ? "First observation / history forming" : "Claude hidden / Codex only";
                    int y = 54 + row * 70; graphics.DrawString(label, font, muted, 20, y);
                    var size = new Size((int)Math.Round(IndicatorView.LogicalWidth(IndicatorStyle.Sparkline, weekly, model) * 1.25), (int)Math.Round(IndicatorView.LogicalHeight * 1.25));
                    using (var bitmap = new Bitmap(size.Width, size.Height))
                    {
                        using (var canvas = Graphics.FromImage(bitmap)) IndicatorView.Draw(canvas, size, IndicatorStyle.Sparkline, palette, weekly, 0, model, 0, -1, colors);
                        Check(bitmap.GetPixel(0, 0).A == 0, "Sparkline must keep transparent capsule corners.");
                        graphics.DrawImageUnscaled(bitmap, 20, y + 17);
                    }
                }
                board.Save(Path.Combine(directory, "sparkline-style-check.png"));
            }
        }
    }
}
