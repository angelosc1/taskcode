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
        internal static void DrawDotMatrixMeter(Graphics graphics, RectangleF bounds, double used, Color accent, Color track)
        {
            float diameter = Math.Min(4.5f, bounds.Height), step = (bounds.Width - diameter) / 9;
            bool valid = used >= 0 && !double.IsNaN(used) && !double.IsInfinity(used);
            using (var background = new SolidBrush(track))
            using (var fill = new SolidBrush(accent))
            for (int dot = 0; dot < 10; dot++)
            {
                var circle = new RectangleF(bounds.Left + dot * step, bounds.Top + (bounds.Height - diameter) / 2, diameter, diameter);
                graphics.FillEllipse(background, circle);
                double amount = valid ? Math.Max(0, Math.Min(1, Math.Min(100, used) / 10 - dot)) : 0;
                if (amount <= 0) continue;
                if (amount >= 1) { graphics.FillEllipse(fill, circle); continue; }
                var state = graphics.Save();
                try { graphics.SetClip(new RectangleF(circle.Left, circle.Top, circle.Width * (float)amount, circle.Height), CombineMode.Intersect); graphics.FillEllipse(fill, circle); }
                finally { graphics.Restore(state); }
            }
        }
        internal static void DrawColorStripMeter(Graphics graphics, RectangleF bounds, double used, Color accent, Color track)
        {
            using (var shape = UiSurface.Shape(bounds, bounds.Width / 2))
            using (var background = new SolidBrush(track))
            using (var fill = new SolidBrush(accent))
            {
                graphics.FillPath(background, shape);
                if (used <= 0 || double.IsNaN(used) || double.IsInfinity(used)) return;
                var state = graphics.Save();
                try
                {
                    float height = bounds.Height * (float)Math.Min(1, used / 100);
                    graphics.SetClip(new RectangleF(bounds.Left, bounds.Bottom - height, bounds.Width, height), CombineMode.Intersect);
                    graphics.FillPath(fill, shape);
                }
                finally { graphics.Restore(state); }
            }
        }
        private static void DrawCompactQuota(Graphics graphics, int x, int used, string period, int window, IndicatorStyle style, bool weekly, Color accent, TaskbarTheme palette, bool usageColors, Font valueFont, Font captionFont)
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
                        DrawDotMatrixMeter(graphics, new RectangleF(x + 39, y + 5, 58.5f, 5), used, fill, palette.Track);
                        graphics.DrawString(Percentage(used), valueFont, text, x + 104, y);
                    }
                    else
                    {
                        DrawDotMatrixMeter(graphics, new RectangleF(x + 25, 14.5f, 58.5f, 5), used, fill, palette.Track);
                        graphics.DrawString(period, captionFont, muted, x + 90, 1);
                        graphics.DrawString(Percentage(used), valueFont, text, x + 90, 11);
                    }
                }
                else
                {
                    int offset = window * 54;
                    DrawColorStripMeter(graphics, new RectangleF(x + 25 + offset, 5, 8, 24), used, fill, palette.Track);
                    graphics.DrawString(period, captionFont, muted, x + 39 + offset, 1);
                    graphics.DrawString(Percentage(used), valueFont, text, x + 39 + offset, 13);
                }
            }
        }
        private static void DrawCompactMeters(Graphics graphics, Size size, IndicatorStyle style, TaskbarTheme palette, bool weekly, UsageDemo usage, int activityKey, double activitySeconds, bool usageColors)
        {
            var state = graphics.Save();
            try
            {
                palette = TaskbarTheme.AppPalette(palette);
                int width = ContentWidth(style, weekly, usage);
                float scale = Math.Min(size.Width / (float)width, size.Height / 34f);
                graphics.ScaleTransform(scale, scale); graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                UiSurface.Draw(graphics, new RectangleF(1, 1, width - 2, 32), palette.PreviewBackground, palette.HighContrast ? palette.Text : Color.FromArgb(68, 73, 80), 16);
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
                            int used; string caption;
                            if (provider < 2) { used = Value(usage, provider * 2 + window); caption = window == 0 ? "5h" : "7d"; }
                            else
                            {
                                var optional = usage.OptionalProviders[provider - 2];
                                if (optional.Snapshot.CountText != null)
                                { if (window == 0) using (var text = new SolidBrush(palette.Text)) graphics.DrawString(optional.DisplayText(now), valueFont, text, x + 30, 10); break; }
                                if (window > 0 && window >= optional.Snapshot.Quotas.Count) break;
                                used = DonutQuotaUsed(optional, window, now); caption = DonutQuotaCaption(optional, window);
                            }
                            DrawCompactQuota(graphics, x, used, caption, window, style, weekly, accent, palette, usageColors, valueFont, captionFont);
                        }
                        if (provider < 2 && (activityKey & (1 << provider)) != 0) ActivityRipple.Draw(graphics, accent, MarkCenter(x), activitySeconds);
                    }
                }
            }
            finally { graphics.Restore(state); }
        }
    }
}
