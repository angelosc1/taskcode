using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace Taskcode.Probe
{
    internal static class DonutChecks
    {
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        internal static void Run(string directory)
        {
            var palette = TaskbarTheme.Create(true);
            var now = DateTime.UtcNow;
            var snapshot = new OptionalSnapshot { ObservedUtc = now };
            snapshot.Quotas.Add(new OptionalQuota { Name = "5 hours", Used = 10, ResetUtc = now.AddHours(2) });
            snapshot.Quotas.Add(new OptionalQuota { Name = "Weekly", Used = 30, ResetUtc = now.AddSeconds(1) });
            var provider = ProviderCatalog.Create()[0]; provider.Enabled = true; provider.Snapshot = snapshot;
            var keyModel = new UsageDemo { OptionalProviders = new[] { provider } };
            string beforeReset = IndicatorView.DonutDataKey(keyModel, now);
            Check(beforeReset != IndicatorView.DonutDataKey(keyModel, now.AddSeconds(2)), "A secondary donut expiring must invalidate the taskbar even when the primary remains unchanged.");
            snapshot.Quotas[1].Used = 50;
            Check(beforeReset != IndicatorView.DonutDataKey(keyModel, now), "A secondary-only quota update must invalidate the taskbar surface.");
            int previous = -1;
            foreach (int used in new[] { 0, 25, 50, 100 })
            using (var bitmap = new Bitmap(48, 48))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(palette.PreviewBackground); graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    IndicatorView.DrawDonutMeter(graphics, new RectangleF(10, 10, 24, 24), used, palette.Claude, palette.Track);
                }
                int painted = 0;
                for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++) if (bitmap.GetPixel(x, y) == palette.Claude) painted++;
                Check(used == 0 ? painted == 0 : painted > previous, "Donut arc coverage must grow with actual consumption and stay empty at zero."); previous = painted;
                Check(bitmap.GetPixel(22, 22) == palette.PreviewBackground, "Donut meters must retain an open center.");
                if (used == 25) Check(bitmap.GetPixel(34, 22) == palette.Claude && bitmap.GetPixel(22, 34) == palette.Track, "Donut consumption must start at noon and grow clockwise.");
                if (used == 100) Check(bitmap.GetPixel(10, 22) == palette.Claude, "Exhausted quota must draw a complete ring.");
            }
            using (var unknown = new Bitmap(48, 48))
            using (var empty = new Bitmap(48, 48))
            {
                using (var g = Graphics.FromImage(unknown)) IndicatorView.DrawDonutMeter(g, new RectangleF(10, 10, 24, 24), -1, palette.Claude, palette.Track);
                using (var g = Graphics.FromImage(empty)) IndicatorView.DrawDonutMeter(g, new RectangleF(10, 10, 24, 24), 0, palette.Claude, palette.Track);
                for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++) Check(unknown.GetPixel(x, y) == empty.GetPixel(x, y), "Unavailable quota must never paint a synthetic consumption arc.");
            }
            using (var board = new Bitmap(700, 490))
            using (var graphics = Graphics.FromImage(board))
            using (var title = new Font("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var text = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var ink = new SolidBrush(palette.Text))
            using (var muted = new SolidBrush(palette.Muted))
            {
                graphics.Clear(palette.PreviewBackground);
                graphics.DrawString("Donuts / Taskcode", title, ink, 20, 14);
                for (int row = 0; row < 6; row++)
                {
                    bool weekly = row == 1 || row == 3 || row == 5, colors = row == 2 || row == 3;
                    var usage = new UsageDemo();
                    if (colors) { usage.Readings[0].Period.Remaining = 75; usage.Readings[1].Period.Remaining = 50; usage.Readings[2].Period.Remaining = 15; usage.Readings[3].Period.Remaining = 0; }
                    if (row == 4) usage.CoreEnabled[0] = false;
                    if (row == 5) usage.BeginLive();
                    string label = row == 0 ? "5h / provider colors" : row == 1 ? "5h + 7d / provider colors" : row == 2 ? "5h / usage colors" : row == 3 ? "5h + 7d / independent usage colors" : row == 4 ? "Claude hidden / Codex only" : "Missing quota / no invented values";
                    int y = 54 + row * 70;
                    graphics.DrawString(label, text, muted, 20, y);
                    var size = new Size((int)Math.Round(IndicatorView.LogicalWidth(IndicatorStyle.Donuts, weekly, usage) * 1.25), (int)Math.Round(IndicatorView.LogicalHeight * 1.25));
                    using (var surface = new Bitmap(size.Width, size.Height))
                    {
                        using (var canvas = Graphics.FromImage(surface)) IndicatorView.Draw(canvas, size, IndicatorStyle.Donuts, palette, weekly, 0, usage, 0, -1, colors);
                        Check(surface.GetPixel(0, 0).A == 0, "Donuts must preserve transparent outer corners.");
                        graphics.DrawImageUnscaled(surface, 20, y + 17);
                    }
                }
                board.Save(Path.Combine(directory, "donuts-style-check.png"));
            }
        }
    }
}
