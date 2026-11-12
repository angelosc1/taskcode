using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Taskcode.Probe
{
    // Vertical pill for taskbars docked on the left or right edge. Only the compact styles stack well
    // in a narrow column: Donuts keeps its rings, Color Strip its upright bars, the rest fall back to Essential.
    internal sealed partial class IndicatorView
    {
        private const int VerticalPillWidth = 34;
        private const int VerticalPadding = 6;
        internal static IndicatorStyle VerticalStyle(IndicatorStyle style)
        { return style == IndicatorStyle.Donuts || style == IndicatorStyle.ColorStrip ? style : IndicatorStyle.Essential; }
        internal static bool SupportsVertical(IndicatorStyle style) { return VerticalStyle(style) == style; }
        private static int VerticalCellHeight(IndicatorStyle style, bool weekly)
        { return VerticalStyle(style) != IndicatorStyle.Essential ? (weekly ? 96 : 60) : (weekly ? 54 : 40); }
        private static int VerticalContentHeight(IndicatorStyle style, bool weekly, UsageDemo usage)
        { int count = DonutProviders(usage).Count; return count == 0 ? 0 : count * VerticalCellHeight(style, weekly) + 2 * VerticalPadding; }
        internal static int VerticalLogicalWidth { get { return LogicalHeight; } }
        internal static int VerticalLogicalHeight(IndicatorStyle style, bool weekly = false, UsageDemo usage = null)
        { return (int)Math.Ceiling(VerticalContentHeight(style, weekly, usage) * IndicatorScale); }
        private static float VerticalScale(Size size, IndicatorStyle style, bool weekly, UsageDemo usage)
        { return Math.Min(size.Width / (float)VerticalPillWidth, size.Height / (float)VerticalContentHeight(style, weekly, usage)); }
        internal static Rectangle VerticalProviderBounds(int provider, Size size, IndicatorStyle style, bool weekly, UsageDemo usage)
        {
            var providers = DonutProviders(usage); int slot = providers.IndexOf(provider);
            if (slot < 0 || size.Width <= 0 || size.Height <= 0) return Rectangle.Empty;
            float scale = VerticalScale(size, style, weekly, usage);
            int cell = VerticalCellHeight(style, weekly);
            int top = slot == 0 ? 0 : (int)Math.Round((VerticalPadding + slot * cell) * scale);
            int bottom = slot == providers.Count - 1 ? size.Height : (int)Math.Round((VerticalPadding + (slot + 1) * cell) * scale);
            return new Rectangle(0, top, size.Width, bottom - top);
        }
        internal static int VerticalProviderAt(Point point, Size size, IndicatorStyle style, bool weekly, UsageDemo usage)
        {
            if (!new Rectangle(Point.Empty, size).Contains(point)) return -1;
            foreach (int provider in DonutProviders(usage)) if (VerticalProviderBounds(provider, size, style, weekly, usage).Contains(point)) return provider;
            return -1;
        }
        internal static void DrawVertical(Graphics graphics, Size size, IndicatorStyle style, TaskbarTheme theme, bool weekly, UsageDemo usage, bool usageColors, bool shell = true)
        {
            style = VerticalStyle(style);
            int content = VerticalContentHeight(style, weekly, usage);
            if (content <= 0 || size.Width <= 0 || size.Height <= 0) return;
            var state = graphics.Save();
            try
            {
                var palette = TaskbarTheme.AppPalette(theme);
                float scale = VerticalScale(size, style, weekly, usage);
                graphics.TranslateTransform((size.Width - VerticalPillWidth * scale) / 2, 0);
                graphics.ScaleTransform(scale, scale);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                if (shell) UiSurface.Draw(graphics, new RectangleF(1, 1, VerticalPillWidth - 2, content - 2), palette.PreviewBackground, palette.HighContrast ? palette.Text : Color.FromArgb(68, 73, 80), 16);
                using (var font = new Font("Segoe UI", 11, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var caption = new Font("Segoe UI", 7, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var text = new SolidBrush(palette.Text))
                using (var divider = new Pen(palette.Divider, .6f))
                {
                    int slot = 0, cell = VerticalCellHeight(style, weekly); var now = DateTime.UtcNow;
                    foreach (int provider in DonutProviders(usage))
                    {
                        float top = VerticalPadding + slot * cell;
                        if (slot++ > 0) graphics.DrawLine(divider, 9, top, VerticalPillWidth - 9, top);
                        Color accent = provider == 0 ? palette.Claude : provider == 1 ? palette.Codex : usage.OptionalProviders[provider - 2].Color;
                        string name = provider == 0 ? "Claude" : provider == 1 ? "Codex" : usage.OptionalProviders[provider - 2].Name;
                        // DrawMark places logos at y = 9; shift the canvas so the logo sits at the top of its cell.
                        var markState = graphics.Save();
                        graphics.TranslateTransform(0, top + 3 - 9);
                        using (var mark = new SolidBrush(accent)) DrawMark(graphics, font, mark, name, (VerticalPillWidth - 16) / 2);
                        graphics.Restore(markState);
                        float y = top + 21; int rowIndex = 0;
                        foreach (var row in VerticalRows(provider, usage, weekly, now))
                        {
                            // Essential has no ring captions: the second (7-day) value is muted to tell the rows apart.
                            bool secondary = style == IndicatorStyle.Essential && rowIndex++ > 0;
                            if (style == IndicatorStyle.Donuts && row.Text == null)
                            {
                                var ring = new RectangleF((VerticalPillWidth - 20) / 2f, y + 1, 20, 20);
                                DrawDonutMeter(graphics, ring, row.Used, UsageBarPalette.Resolve(accent, row.Used, usageColors, palette.HighContrast), palette.Track);
                                DrawDonutTime(graphics, ring, 3.2f, row.Used, row.Elapsed, palette);
                                using (var center = new SolidBrush(row.Used < 0 ? palette.Muted : palette.Text))
                                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                                    graphics.DrawString(row.Caption, caption, center, ring, format);
                                y += 23;
                            }
                            else if (style == IndicatorStyle.ColorStrip && row.Text == null)
                            {
                                // Upright strip filling bottom-up, with its period beside it.
                                var strip = new RectangleF(VerticalPillWidth / 2f - 6, y + 1, 7, 20);
                                DrawColorStripMeter(graphics, strip, row.Used, UsageBarPalette.Resolve(accent, row.Used, usageColors, palette.HighContrast), palette.Track, row.Elapsed, palette);
                                DrawStripMarker(graphics, strip, row.Used, row.Elapsed, palette);
                                using (var muted = new SolidBrush(palette.Muted)) graphics.DrawString(row.Caption, caption, muted, strip.Right + 2, y + 6);
                                y += 23;
                            }
                            using (var muted = new SolidBrush(palette.Muted)) DrawVerticalValue(graphics, secondary ? muted : text, row.Text ?? Percentage(row.Used), y);
                            if (style == IndicatorStyle.Essential && row.Text == null) DrawTimeRule(graphics, 7, VerticalPillWidth - 7, y + 12.5f, row.Used, row.Elapsed, palette);
                            y += 14;
                        }
                    }
                }
            }
            finally { graphics.Restore(state); }
        }
        private sealed class VerticalRow { internal int Used; internal double? Elapsed; internal string Caption, Text; }
        private static IEnumerable<VerticalRow> VerticalRows(int provider, UsageDemo usage, bool weekly, DateTime now)
        {
            if (provider < 2)
            {
                yield return new VerticalRow { Used = Value(usage, provider * 2), Elapsed = Elapsed(usage, provider * 2, now), Caption = "5h" };
                if (weekly) yield return new VerticalRow { Used = Value(usage, provider * 2 + 1), Elapsed = Elapsed(usage, provider * 2 + 1, now), Caption = "7d" };
                yield break;
            }
            var optional = usage.OptionalProviders[provider - 2];
            if (optional.Snapshot.CountText != null) { yield return new VerticalRow { Used = optional.DisplayUsed(now), Text = optional.DisplayText(now) }; yield break; }
            yield return new VerticalRow { Used = DonutQuotaUsed(optional, 0, now), Elapsed = OptionalElapsed(optional, 0, now), Caption = DonutQuotaCaption(optional, 0) };
            if (weekly && optional.Snapshot.Quotas.Count > 1)
                yield return new VerticalRow { Used = DonutQuotaUsed(optional, 1, now), Elapsed = OptionalElapsed(optional, 1, now), Caption = DonutQuotaCaption(optional, 1) };
        }
        // Centers a value in the narrow column, shrinking long texts (e.g. "100%" or counters) to fit.
        private static void DrawVerticalValue(Graphics graphics, Brush brush, string value, float y)
        {
            float size = 10.5f;
            using (var probe = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                float width = graphics.MeasureString(value, probe, PointF.Empty, StringFormat.GenericTypographic).Width;
                if (width > VerticalPillWidth - 6) size = Math.Max(6.5f, size * (VerticalPillWidth - 6) / width);
            }
            using (var bold = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var format = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center })
                graphics.DrawString(value, bold, brush, new RectangleF(0, y + (10.5f - size) / 2, VerticalPillWidth, 14), format);
        }
    }
}
