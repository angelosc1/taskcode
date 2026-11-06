using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Taskcode.Probe
{
    internal static class UsageColorChecks
    {
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        internal static void Run(string directory)
        {
            var theme = TaskbarTheme.Create(true);
            Check(!new Settings().UsageBarColors, "Provider bar colors must remain the default.");
            Check(UsageBarPalette.Resolve(theme.Claude, 0, true, false) == UsageBarPalette.Green &&
                UsageBarPalette.Resolve(theme.Claude, 50, true, false) == UsageBarPalette.Yellow &&
                UsageBarPalette.Resolve(theme.Claude, 100, true, false) == UsageBarPalette.Red,
                "Usage colors must anchor at green, yellow and red for 0, 50 and 100 percent used.");
            Check(UsageBarPalette.Resolve(theme.Claude, 25, true, false) == UiSurface.Blend(UsageBarPalette.Green, UsageBarPalette.Yellow, .5) &&
                UsageBarPalette.Resolve(theme.Claude, 75, true, false) == UiSurface.Blend(UsageBarPalette.Yellow, UsageBarPalette.Red, .5),
                "Usage colors must interpolate continuously between anchors.");
            Check(UsageBarPalette.Resolve(theme.Codex, 100, false, false) == theme.Codex &&
                UsageBarPalette.Resolve(theme.Codex, 100, true, true) == theme.Codex &&
                UsageBarPalette.Resolve(theme.Codex, -1, true, false) == theme.Codex,
                "Disabled, unknown, and high-contrast readings must preserve provider/system colors.");
            foreach (IndicatorStyle style in Enum.GetValues(typeof(IndicatorStyle)))
            foreach (bool weekly in new[] { false, true })
            using (var original = new Bitmap(IndicatorView.LogicalWidth(style, weekly), IndicatorView.LogicalHeight))
            using (var colored = new Bitmap(original.Width, original.Height))
            {
                using (var g = Graphics.FromImage(original)) IndicatorView.Draw(g, original.Size, style, theme, weekly);
                using (var g = Graphics.FromImage(colored)) IndicatorView.Draw(g, colored.Size, style, theme, weekly, 0, null, 0, -1, true);
                int changed = 0;
                for (int y = 0; y < original.Height; y++) for (int x = 0; x < original.Width; x++)
                {
                    if (original.GetPixel(x, y) == colored.GetPixel(x, y)) continue;
                    changed++;
                    Check(x >= 40, "Usage coloring must not recolor provider logos.");
                }
                Check(style == IndicatorStyle.Essential ? changed == 0 : changed > 0,
                    "Usage coloring must affect Lines and Blocks meters, while Essential has no meter to recolor.");
            }
            using (var board = new Bitmap(640, 222))
            using (var graphics = Graphics.FromImage(board))
            using (var font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var ink = new SolidBrush(theme.Text))
            {
                graphics.Clear(theme.PreviewBackground);
                graphics.DrawString("Provider colors (default)", font, ink, 78, 12);
                graphics.DrawString("Usage-based bar colors", font, ink, 370, 12);
                for (int row = 0; row < 5; row++)
                {
                    int used = row * 25, y = 42 + row * 34;
                    graphics.DrawString(used + "%", font, ink, 16, y + 6);
                    foreach (bool enabled in new[] { false, true })
                    using (var bar = new QuotaBar { Size = new Size(248, 17), Provider = "claude", Theme = theme, UsageBarColors = enabled,
                        BackColor = theme.PreviewBackground, Period = new QuotaPeriod { Remaining = 100 - used } })
                    using (var surface = new Bitmap(bar.Width, bar.Height))
                    {
                        bar.DrawToBitmap(surface, bar.ClientRectangle);
                        if (used > 0) Check(surface.GetPixel(12, 11) == UsageBarPalette.Resolve(theme.Claude, used, enabled, false),
                            "The actual quota fill must use the configured consumption color.");
                        graphics.DrawImageUnscaled(surface, enabled ? 364 : 72, y);
                    }
                }
                board.Save(Path.Combine(directory, "usage-colors-check.png"), ImageFormat.Png);
            }
        }
    }
}
