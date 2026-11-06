using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace Taskcode.Probe
{
    internal sealed partial class IndicatorView
    {
        private const int SparklineCellWidth = 146;
        private static Rectangle SparklineProviderBounds(int provider, Size size, UsageDemo usage)
        {
            var providers = DonutProviders(usage); int slot = providers.IndexOf(provider);
            if (slot < 0 || size.Width <= 0 || size.Height <= 0) return Rectangle.Empty;
            float scale = Math.Min(size.Width / (float)(providers.Count * SparklineCellWidth + 16), size.Height / 34f);
            int left = slot == 0 ? 0 : (int)Math.Round((8 + slot * SparklineCellWidth) * scale);
            int right = slot == providers.Count - 1 ? size.Width : (int)Math.Round((8 + (slot + 1) * SparklineCellWidth) * scale);
            return new Rectangle(left, 0, right - left, size.Height);
        }
        internal static string SparklineDataKey(UsageDemo usage, DateTime now)
        {
            string key = DonutDataKey(usage, now);
            foreach (int provider in DonutProviders(usage))
                if (provider < 2)
                    for (int window = 0; window < 2; window++) key += "|" + usage.Readings[provider * 2 + window].Trend.Version + ":" + usage.Readings[provider * 2 + window].Trend.PointsAt(now).Length;
                else
                {
                    var optional = usage.OptionalProviders[provider - 2];
                    foreach (var quota in optional.Snapshot.Quotas)
                    { QuotaTrend trend; if (optional.Trends.TryGetValue(quota.Name, out trend)) key += "|" + quota.Name + ":" + trend.Version + ":" + trend.PointsAt(now).Length; }
                }
            return key;
        }
        internal static PointF[] SparklinePoints(TrendPoint[] samples, RectangleF bounds)
        {
            if (samples.Length == 0) return new PointF[0];
            double minimum = 100, maximum = 0;
            foreach (var sample in samples) { minimum = Math.Min(minimum, sample.Used); maximum = Math.Max(maximum, sample.Used); }
            // An adaptive trend scale with a 10-point minimum keeps tiny fluctuations subtle.
            double range = Math.Max(10, maximum - minimum), low = Math.Max(0, Math.Min(100 - range, (minimum + maximum - range) / 2));
            double seconds = (samples[samples.Length - 1].At - samples[0].At).TotalSeconds;
            var points = new PointF[samples.Length];
            for (int index = 0; index < samples.Length; index++)
                points[index] = new PointF(bounds.Left + (float)(seconds > 0 ? (samples[index].At - samples[0].At).TotalSeconds / seconds : 1) * bounds.Width,
                    bounds.Bottom - (float)((samples[index].Used - low) / range) * bounds.Height);
            return points;
        }
        private static void DrawTrend(Graphics graphics, RectangleF bounds, TrendPoint[] samples, Color accent, TaskbarTheme palette)
        {
            using (var guide = new Pen(palette.Track, .65f) { DashStyle = DashStyle.Dot }) graphics.DrawLine(guide, bounds.Left, bounds.Top + bounds.Height / 2, bounds.Right, bounds.Top + bounds.Height / 2);
            var points = SparklinePoints(samples, bounds);
            if (points.Length == 0) return;
            if (points.Length == 1) { using (var dot = new SolidBrush(accent)) graphics.FillEllipse(dot, points[0].X - 1, points[0].Y - 1, 2, 2); return; }
            var polygon = new PointF[points.Length + 2];
            Array.Copy(points, polygon, points.Length); polygon[points.Length] = new PointF(points[points.Length - 1].X, bounds.Bottom); polygon[points.Length + 1] = new PointF(points[0].X, bounds.Bottom);
            using (var fill = new SolidBrush(Color.FromArgb(24, accent))) graphics.FillPolygon(fill, polygon);
            using (var line = new Pen(accent, 1.35f) { LineJoin = LineJoin.Round }) graphics.DrawLines(line, points);
        }
        private static string TrendDelta(TrendPoint[] samples)
        {
            if (samples.Length < 2) return "...";
            double delta = samples[samples.Length - 1].Used - samples[0].Used;
            return delta.ToString("+0.#;-0.#;0", CultureInfo.CurrentCulture) + " pp";
        }
        private static void DrawSparklineRow(Graphics graphics, int x, int y, int used, string period, QuotaTrend trend, bool weekly, Color accent, TaskbarTheme palette, bool usageColors, Font valueFont, Font smallFont, DateTime now)
        {
            var samples = used < 0 || trend == null ? new TrendPoint[0] : trend.PointsAt(now);
            Color color = UsageBarPalette.Resolve(accent, used, usageColors, palette.HighContrast);
            using (var ink = new SolidBrush(palette.Text))
            using (var muted = new SolidBrush(palette.Muted))
            using (var variation = new SolidBrush(samples.Length < 2 ? palette.Muted : color))
            {
                graphics.DrawString(period, smallFont, muted, x + 22, weekly ? y + 2 : 1);
                graphics.DrawString(Percentage(used), valueFont, ink, x + (weekly ? 36 : 23), weekly ? y : 10);
                graphics.DrawString(used < 0 ? "--" : TrendDelta(samples), smallFont, variation, x + (weekly ? 70 : 58), weekly ? y + 2 : 12);
                DrawTrend(graphics, new RectangleF(x + (weekly ? 99 : 94), weekly ? y + 3 : 7, weekly ? 35 : 40, weekly ? 9 : 20), samples, color, palette);
            }
        }
        private static void DrawSparklines(Graphics graphics, Size size, TaskbarTheme palette, bool weekly, UsageDemo usage, int activityKey, double activitySeconds, bool usageColors)
        {
            var state = graphics.Save();
            try
            {
                palette = TaskbarTheme.AppPalette(palette);
                int width = ContentWidth(IndicatorStyle.Sparkline, weekly, usage);
                float scale = Math.Min(size.Width / (float)width, size.Height / 34f);
                graphics.ScaleTransform(scale, scale); graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                UiSurface.Draw(graphics, new RectangleF(1, 1, width - 2, 32), palette.PreviewBackground, palette.HighContrast ? palette.Text : Color.FromArgb(68, 73, 80), 16);
                var model = usage ?? new UsageDemo();
                using (var font = new Font("Segoe UI", 11, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var valueFont = new Font("Segoe UI", weekly ? 10 : 11, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var smallFont = new Font("Segoe UI", weekly ? 6.7f : 7.5f, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    int slot = 0; var now = DateTime.UtcNow;
                    foreach (int provider in DonutProviders(usage))
                    {
                        int x = 12 + slot++ * SparklineCellWidth;
                        Color accent = provider == 0 ? palette.Claude : provider == 1 ? palette.Codex : model.OptionalProviders[provider - 2].Color;
                        string name = provider == 0 ? "Claude" : provider == 1 ? "Codex" : model.OptionalProviders[provider - 2].Name;
                        if (slot > 1) using (var divider = new Pen(palette.Divider, .6f)) graphics.DrawLine(divider, x - 8, 8, x - 8, 26);
                        using (var mark = new SolidBrush(accent)) DrawMark(graphics, font, mark, name, x);
                        for (int window = 0; window < (weekly ? 2 : 1); window++)
                        {
                            int used; string caption; QuotaTrend trend = null;
                            if (provider < 2)
                            { int index = provider * 2 + window; used = Value(model, index); caption = window == 0 ? "5h" : "7d"; trend = model.Readings[index].Trend; }
                            else
                            {
                                var optional = model.OptionalProviders[provider - 2];
                                if (optional.Snapshot.CountText != null)
                                { if (window == 0) using (var text = new SolidBrush(palette.Text)) graphics.DrawString(optional.DisplayText(now), valueFont, text, x + 25, 10); break; }
                                if (window > 0 && window >= optional.Snapshot.Quotas.Count) break;
                                used = DonutQuotaUsed(optional, window, now); caption = DonutQuotaCaption(optional, window);
                                if (window < optional.Snapshot.Quotas.Count) optional.Trends.TryGetValue(optional.Snapshot.Quotas[window].Name, out trend);
                            }
                            DrawSparklineRow(graphics, x, window == 0 ? 2 : 16, used, caption, trend, weekly, accent, palette, usageColors, valueFont, smallFont, now);
                        }
                        if (provider < 2 && (activityKey & (1 << provider)) != 0) ActivityRipple.Draw(graphics, accent, MarkCenter(x), activitySeconds);
                    }
                }
            }
            finally { graphics.Restore(state); }
        }
    }
}
