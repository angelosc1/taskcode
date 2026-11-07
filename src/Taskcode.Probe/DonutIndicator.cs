using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Taskcode.Probe
{
    internal sealed partial class IndicatorView
    {
        private static int DonutCellWidth(bool weekly) { return weekly ? 162 : 104; }
        private static List<int> DonutProviders(UsageDemo usage)
        { return usage == null ? new List<int> { 0, 1 } : VisibleProviders(usage); }
        private static Rectangle DonutProviderBounds(int provider, Size size, bool weekly, UsageDemo usage)
        {
            var providers = DonutProviders(usage); int slot = providers.IndexOf(provider);
            if (slot < 0 || size.Width <= 0 || size.Height <= 0) return Rectangle.Empty;
            int content = ContentWidth(IndicatorStyle.Donuts, weekly, usage);
            float scale = Math.Min(size.Width / (float)content, size.Height / 34f);
            int left = slot == 0 ? 0 : (int)Math.Round((8 + slot * DonutCellWidth(weekly)) * scale);
            int right = slot == providers.Count - 1 ? size.Width : (int)Math.Round((8 + (slot + 1) * DonutCellWidth(weekly)) * scale);
            return new Rectangle(left, 0, right - left, size.Height);
        }
        internal static void DrawDonutMeter(Graphics graphics, RectangleF bounds, double used, Color color, Color track)
        {
            using (var background = new Pen(track, 3.2f)) graphics.DrawEllipse(background, bounds);
            if (used <= 0 || double.IsNaN(used) || double.IsInfinity(used)) return;
            using (var fill = new Pen(color, 3.2f))
            {
                if (used >= 100) graphics.DrawEllipse(fill, bounds);
                else { fill.StartCap = fill.EndCap = LineCap.Round; graphics.DrawArc(fill, bounds, -90, (float)(used * 3.6)); }
            }
        }
        private static void DrawDonutValue(Graphics graphics, int x, int used, string caption, Color accent, TaskbarTheme palette, Font valueFont, Font captionFont, bool usageColors)
        {
            var bounds = new RectangleF(x, 6, 22, 22);
            DrawDonutMeter(graphics, bounds, used, UsageBarPalette.Resolve(accent, used, usageColors, palette.HighContrast), palette.Track);
            using (var center = new SolidBrush(used < 0 ? palette.Muted : palette.Text))
            using (var text = new SolidBrush(palette.Text))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                graphics.DrawString(caption, captionFont, center, bounds, format);
                graphics.DrawString(Percentage(used), valueFont, text, x + 29, 10, StringFormat.GenericTypographic);
            }
        }
        private static int DonutQuotaUsed(OptionalProvider provider, int index, DateTime now)
        {
            var snapshot = provider.Snapshot;
            if (snapshot.Failed || now - snapshot.ObservedUtc > TimeSpan.FromMinutes(6) || index >= snapshot.Quotas.Count) return -1;
            var quota = snapshot.Quotas[index];
            return quota.ResetUtc.HasValue && quota.ResetUtc.Value <= now ? -1 : (int)Math.Round(quota.Used);
        }
        internal static string DonutDataKey(UsageDemo usage, DateTime now)
        {
            string key = "";
            foreach (var provider in usage.OptionalProviders)
                if (provider.Enabled) for (int index = 0; index < Math.Min(2, provider.Snapshot.Quotas.Count); index++)
                    key += "|" + provider.Index + ":" + DonutQuotaUsed(provider, index, now) + ":" + DonutQuotaCaption(provider, index);
            return key;
        }
        private static string DonutQuotaCaption(OptionalProvider provider, int index)
        {
            if (index >= provider.Snapshot.Quotas.Count) return "--";
            string name = provider.Snapshot.Quotas[index].Name.ToLowerInvariant();
            if (name.Contains("5 hours")) return "5h";
            if (name.Contains("weekly")) return "7d";
            if (name.Contains("monthly") || name.Contains("month")) return "mo";
            if (name.Contains("api")) return "api";
            if (name.Contains("included")) return "inc";
            return "%";
        }
        private static float DonutContentWidth(Graphics graphics, Font font, int provider, UsageDemo usage, bool weekly, DateTime now)
        {
            string value; int offset;
            if (provider < 2)
            { value = Percentage(Value(usage, provider * 2 + (weekly ? 1 : 0))); offset = weekly ? 116 : 54; }
            else
            {
                var optional = usage.OptionalProviders[provider - 2];
                if (optional.Snapshot.CountText != null) { value = optional.DisplayText(now); offset = 31; }
                else { int index = weekly && optional.Snapshot.Quotas.Count > 1 ? 1 : 0; value = Percentage(DonutQuotaUsed(optional, index, now)); offset = index == 1 ? 116 : 54; }
            }
            return offset + graphics.MeasureString(value, font, PointF.Empty, StringFormat.GenericTypographic).Width;
        }
        private static void DrawDonuts(Graphics graphics, Size size, TaskbarTheme palette, bool weekly, UsageDemo usage, int activityKey, double activitySeconds, bool usageColors, bool shell)
        {
            var state = graphics.Save();
            try
            {
                palette = TaskbarTheme.AppPalette(palette);
                int width = ContentWidth(IndicatorStyle.Donuts, weekly, usage);
                float scale = Math.Min(size.Width / (float)width, size.Height / 34f);
                graphics.ScaleTransform(scale, scale); graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                if (shell) UiSurface.Draw(graphics, new RectangleF(1, 1, width - 2, 32), palette.PreviewBackground, palette.HighContrast ? palette.Text : Color.FromArgb(68, 73, 80), 16);
                using (var font = new Font("Segoe UI", 11, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var bold = new Font("Segoe UI", 10.5f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var caption = new Font("Segoe UI", 7, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    int slot = 0; var now = DateTime.UtcNow;
                    foreach (int provider in DonutProviders(usage))
                    {
                        int cellLeft = 8 + slot * DonutCellWidth(weekly);
                        int x = (int)Math.Round(cellLeft + (DonutCellWidth(weekly) - DonutContentWidth(graphics, bold, provider, usage, weekly, now)) / 2);
                        Color accent = provider == 0 ? palette.Claude : provider == 1 ? palette.Codex : usage.OptionalProviders[provider - 2].Color;
                        string name = provider == 0 ? "Claude" : provider == 1 ? "Codex" : usage.OptionalProviders[provider - 2].Name;
                        if (slot++ > 0) using (var divider = new Pen(palette.Divider, .6f)) graphics.DrawLine(divider, cellLeft, 8, cellLeft, 26);
                        using (var mark = new SolidBrush(accent)) DrawMark(graphics, font, mark, name, x);
                        if (provider < 2)
                        {
                            DrawDonutValue(graphics, x + 25, Value(usage, provider * 2), "5h", accent, palette, bold, caption, usageColors);
                            if (weekly) DrawDonutValue(graphics, x + 87, Value(usage, provider * 2 + 1), "7d", accent, palette, bold, caption, usageColors);
                        }
                        else
                        {
                            var optional = usage.OptionalProviders[provider - 2];
                            if (optional.Snapshot.CountText != null)
                            { using (var text = new SolidBrush(palette.Text)) graphics.DrawString(optional.DisplayText(now), bold, text, x + 31, 10, StringFormat.GenericTypographic); continue; }
                            DrawDonutValue(graphics, x + 25, DonutQuotaUsed(optional, 0, now), DonutQuotaCaption(optional, 0), accent, palette, bold, caption, usageColors);
                            if (weekly && optional.Snapshot.Quotas.Count > 1) DrawDonutValue(graphics, x + 87, DonutQuotaUsed(optional, 1, now), DonutQuotaCaption(optional, 1), accent, palette, bold, caption, usageColors);
                        }
                    }
                }
            }
            finally { graphics.Restore(state); }
        }
    }
}
