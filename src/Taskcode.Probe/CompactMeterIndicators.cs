using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Taskcode.Probe
{
    internal sealed partial class IndicatorView
    {
        private static int CompactMeterCellWidth(IndicatorStyle style, bool weekly)
        { return style == IndicatorStyle.DotMatrix ? (weekly ? 148 : 134) : (weekly ? 134 : 84); }
        private static Rectangle CompactMeterProviderBounds(int provider, Size size, IndicatorStyle style, bool weekly, UsageDemo usage)
        {
            var providers = DonutProviders(usage); int slot = providers.IndexOf(provider);
            if (slot < 0 || size.Width <= 0 || size.Height <= 0) return Rectangle.Empty;
            int cell = CompactMeterCellWidth(style, weekly);
            float scale = Math.Min(size.Width / (float)(providers.Count * cell + 16), size.Height / 34f);
            int left = slot == 0 ? 0 : (int)Math.Round((8 + slot * cell) * scale);
            int right = slot == providers.Count - 1 ? size.Width : (int)Math.Round((8 + (slot + 1) * cell) * scale);
            return new Rectangle(left, 0, right - left, size.Height);
        }
        internal static void DrawDotMatrixMeter(Graphics graphics, RectangleF bounds, double used, Color accent, Color track, double? elapsed = null, TaskbarTheme palette = null)
        {
            float diameter = Math.Min(4.5f, bounds.Height), step = (bounds.Width - diameter) / 9;
            bool valid = used >= 0 && !double.IsNaN(used) && !double.IsInfinity(used);
            bool trail = palette != null && Marker == TimeMarker.Trail && valid && elapsed.HasValue;
            bool overshoot = trail && PaintOvershoot(palette, (int)Math.Round(used), elapsed.Value);
            using (var background = new SolidBrush(track))
            using (var fill = new SolidBrush(accent))
            using (var lit = new SolidBrush(trail ? TrailColor(palette, (int)Math.Round(used), elapsed.Value) : track))
            using (var ahead = new SolidBrush(palette == null ? accent : palette.Text))
            for (int dot = 0; dot < 10; dot++)
            {
                var circle = new RectangleF(bounds.Left + dot * step, bounds.Top + (bounds.Height - diameter) / 2, diameter, diameter);
                graphics.FillEllipse(background, circle);
                double time = trail ? Math.Max(0, Math.Min(1, elapsed.Value * 10 - dot)) : 0;
                if (time > 0) FillDotPart(graphics, lit, circle, 0, time);
                double amount = valid ? Math.Max(0, Math.Min(1, Math.Min(100, used) / 10 - dot)) : 0;
                if (amount > 0) FillDotPart(graphics, fill, circle, 0, amount);
                if (overshoot && amount > time) FillDotPart(graphics, ahead, circle, time, amount);
            }
        }
        private static void FillDotPart(Graphics graphics, Brush brush, RectangleF circle, double from, double to)
        {
            if (from <= 0 && to >= 1) { graphics.FillEllipse(brush, circle); return; }
            var state = graphics.Save();
            try { graphics.SetClip(new RectangleF(circle.Left + circle.Width * (float)from, circle.Top, circle.Width * (float)(to - from), circle.Height), CombineMode.Intersect); graphics.FillEllipse(brush, circle); }
            finally { graphics.Restore(state); }
        }
        internal static void DrawColorStripMeter(Graphics graphics, RectangleF bounds, double used, Color accent, Color track, double? elapsed = null, TaskbarTheme palette = null)
        {
            bool valid = used >= 0 && !double.IsNaN(used) && !double.IsInfinity(used);
            bool trail = palette != null && Marker == TimeMarker.Trail && valid && elapsed.HasValue;
            using (var shape = UiSurface.Shape(bounds, bounds.Width / 2))
            using (var background = new SolidBrush(track))
            using (var fill = new SolidBrush(accent))
            {
                graphics.FillPath(background, shape);
                if (trail) using (var lit = new SolidBrush(TrailColor(palette, (int)Math.Round(used), elapsed.Value))) FillStripPart(graphics, lit, shape, bounds, 0, elapsed.Value);
                if (used <= 0 || !valid) return;
                FillStripPart(graphics, fill, shape, bounds, 0, Math.Min(1, used / 100));
                if (trail && PaintOvershoot(palette, (int)Math.Round(used), elapsed.Value))
                    using (var ahead = new SolidBrush(palette.Text)) FillStripPart(graphics, ahead, shape, bounds, elapsed.Value, Math.Min(1, used / 100));
            }
        }
        // Fills the strip from the bottom, between two fractions of its height.
        private static void FillStripPart(Graphics graphics, Brush brush, GraphicsPath shape, RectangleF bounds, double from, double to)
        {
            if (to <= from) return;
            var state = graphics.Save();
            try
            {
                float top = bounds.Bottom - bounds.Height * (float)to, bottom = bounds.Bottom - bounds.Height * (float)from;
                graphics.SetClip(new RectangleF(bounds.Left, top, bounds.Width, bottom - top), CombineMode.Intersect);
                graphics.FillPath(brush, shape);
            }
            finally { graphics.Restore(state); }
        }
        private static void DrawCompactQuota(Graphics graphics, int x, int used, string period, int window, IndicatorStyle style, bool weekly, Color accent, TaskbarTheme palette, bool usageColors, Font valueFont, Font captionFont, double? elapsed = null)
        {
            Color fill = UsageBarPalette.Resolve(accent, used, usageColors, palette.HighContrast);
            using (var text = new SolidBrush(palette.Text))
            using (var muted = new SolidBrush(palette.Muted))
            {
                if (style == IndicatorStyle.DotMatrix)
                {
                    if (weekly)
                    {
                        int y = 2 + window * 15;
                        graphics.DrawString(period, captionFont, muted, x + 22, y + 2);
                        var meter = new RectangleF(x + 39, y + 5, 58.5f, 5);
                        DrawDotMatrixMeter(graphics, meter, used, fill, palette.Track, elapsed, palette);
                        DrawBarMarker(graphics, meter.Left, meter.Right, meter.Top, meter.Bottom, used, elapsed, ArrowBelow(true, window), palette);
                        graphics.DrawString(Percentage(used), valueFont, text, x + 104, y);
                    }
                    else
                    {
                        var meter = new RectangleF(x + 25, 14.5f, 58.5f, 5);
                        DrawDotMatrixMeter(graphics, meter, used, fill, palette.Track, elapsed, palette);
                        DrawBarMarker(graphics, meter.Left, meter.Right, meter.Top, meter.Bottom, used, elapsed, ArrowBelow(false, 0), palette);
                        graphics.DrawString(period, captionFont, muted, x + 90, 1);
                        graphics.DrawString(Percentage(used), valueFont, text, x + 90, 11);
                    }
                }
                else
                {
                    int offset = window * 54;
                    var strip = new RectangleF(x + 25 + offset, 5, 8, 24);
                    DrawColorStripMeter(graphics, strip, used, fill, palette.Track, elapsed, palette);
                    DrawStripMarker(graphics, strip, used, elapsed, palette);
                    graphics.DrawString(period, captionFont, muted, x + 39 + offset, 1);
                    graphics.DrawString(Percentage(used), valueFont, text, x + 39 + offset, 13);
                }
            }
        }
        private static void DrawCompactMeters(Graphics graphics, Size size, IndicatorStyle style, TaskbarTheme palette, bool weekly, UsageDemo usage, int activityKey, double activitySeconds, bool usageColors, bool shell)
        {
            var state = graphics.Save();
            try
            {
                palette = TaskbarTheme.AppPalette(palette);
                int width = ContentWidth(style, weekly, usage);
                float scale = Math.Min(size.Width / (float)width, size.Height / 34f);
                graphics.ScaleTransform(scale, scale); graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                if (shell) UiSurface.Draw(graphics, new RectangleF(1, 1, width - 2, 32), palette.PreviewBackground, palette.HighContrast ? palette.Text : Color.FromArgb(68, 73, 80), 16);
                using (var font = new Font("Segoe UI", 11, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var valueFont = new Font("Segoe UI", weekly ? 10 : 11, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var captionFont = new Font("Segoe UI", 7, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    int slot = 0; var now = DateTime.UtcNow;
                    foreach (int provider in DonutProviders(usage))
                    {
                        int x = 12 + slot++ * CompactMeterCellWidth(style, weekly);
                        Color accent = provider == 0 ? palette.Claude : provider == 1 ? palette.Codex : usage.OptionalProviders[provider - 2].Color;
                        string name = provider == 0 ? "Claude" : provider == 1 ? "Codex" : usage.OptionalProviders[provider - 2].Name;
                        if (slot > 1) using (var divider = new Pen(palette.Divider, .6f)) graphics.DrawLine(divider, x - 8, 8, x - 8, 26);
                        using (var mark = new SolidBrush(accent)) DrawMark(graphics, font, mark, name, x);
                        for (int window = 0; window < (weekly ? 2 : 1); window++)
                        {
                            int used; string caption; double? elapsed;
                            if (provider < 2) { used = Value(usage, provider * 2 + window); caption = window == 0 ? "5h" : "7d"; elapsed = Elapsed(usage, provider * 2 + window, now); }
                            else
                            {
                                var optional = usage.OptionalProviders[provider - 2];
                                if (optional.Snapshot.CountText != null)
                                { if (window == 0) using (var text = new SolidBrush(palette.Text)) graphics.DrawString(optional.DisplayText(now), valueFont, text, x + 30, 10); break; }
                                if (window > 0 && window >= optional.Snapshot.Quotas.Count) break;
                                used = DonutQuotaUsed(optional, window, now); caption = DonutQuotaCaption(optional, window); elapsed = OptionalElapsed(optional, window, now);
                            }
                            DrawCompactQuota(graphics, x, used, caption, window, style, weekly, accent, palette, usageColors, valueFont, captionFont, elapsed);
                        }
                    }
                }
            }
            finally { graphics.Restore(state); }
        }
    }
}
