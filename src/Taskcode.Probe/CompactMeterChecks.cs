using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace Taskcode.Probe
{
    internal static class CompactMeterChecks
    {
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static Bitmap Meter(IndicatorStyle style, double used, TaskbarTheme palette)
        {
            var bitmap = new Bitmap(90, 40);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(palette.PreviewBackground); graphics.SmoothingMode = SmoothingMode.AntiAlias;
                if (style == IndicatorStyle.DotMatrix) IndicatorView.DrawDotMatrixMeter(graphics, new RectangleF(10, 14, 60, 5), used, palette.Claude, palette.Track);
                else IndicatorView.DrawColorStripMeter(graphics, new RectangleF(10, 5, 8, 24), used, palette.Claude, palette.Track);
            }
            return bitmap;
        }
        internal static void Run(string directory)
        {
            var palette = TaskbarTheme.Create(true);
            foreach (IndicatorStyle style in new[] { IndicatorStyle.DotMatrix, IndicatorStyle.ColorStrip })
            {
                int previous = -1;
                foreach (int used in new[] { 0, 25, 50, 100 })
                using (var bitmap = Meter(style, used, palette))
                {
                    int colored = 0;
                    for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++) if (bitmap.GetPixel(x, y) == palette.Claude) colored++;
                    Check(used == 0 ? colored == 0 : colored > previous, "Dot Matrix and Color Strip fill must grow with consumed quota."); previous = colored;
                    if (style == IndicatorStyle.ColorStrip && used == 25) Check(bitmap.GetPixel(14, 27) == palette.Claude && bitmap.GetPixel(14, 10) == palette.Track, "The strip must fill from bottom to top.");
                }
                using (var unknown = Meter(style, -1, palette))
                using (var empty = Meter(style, 0, palette))
                for (int y = 0; y < unknown.Height; y++) for (int x = 0; x < unknown.Width; x++) Check(unknown.GetPixel(x, y) == empty.GetPixel(x, y), "Unknown quota must not paint synthetic consumption.");
                Render(directory, style, palette);
            }
            using (var quarter = Meter(IndicatorStyle.DotMatrix, 20, palette))
            {
                Check(quarter.GetPixel(12, 16) == palette.Claude && quarter.GetPixel(18, 16) == palette.Claude && quarter.GetPixel(24, 16) == palette.Track, "Twenty percent must illuminate only the first two of ten dots.");
            }
        }
        private static void Render(string directory, IndicatorStyle style, TaskbarTheme palette)
        {
            using (var board = new Bitmap(700, 490))
            using (var graphics = Graphics.FromImage(board))
            using (var title = new Font("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var ink = new SolidBrush(palette.Text))
            using (var muted = new SolidBrush(palette.Muted))
            {
                graphics.Clear(palette.PreviewBackground); graphics.DrawString(style == IndicatorStyle.DotMatrix ? "Dot Matrix / Taskcode" : "Color Strip / Taskcode", title, ink, 20, 14);
                for (int row = 0; row < 6; row++)
                {
                    bool weekly = row == 1 || row == 3 || row == 5, colors = row == 2 || row == 3;
                    var model = new UsageDemo();
                    if (colors) { model.Readings[0].Period.Remaining = 75; model.Readings[1].Period.Remaining = 50; model.Readings[2].Period.Remaining = 15; model.Readings[3].Period.Remaining = 0; }
                    if (row == 4) model.CoreEnabled[0] = false;
                    if (row == 5) model.BeginLive();
                    string label = row == 0 ? "5h / provider colors" : row == 1 ? "5h + 7d / provider colors" : row == 2 ? "5h / usage colors" : row == 3 ? "5h + 7d / independent usage colors" : row == 4 ? "Claude hidden / Codex only" : "Missing quota / no invented values";
                    int y = 54 + row * 70; graphics.DrawString(label, font, muted, 20, y);
                    var size = new Size((int)Math.Round(IndicatorView.LogicalWidth(style, weekly, model) * 1.25), (int)Math.Round(IndicatorView.LogicalHeight * 1.25));
                    using (var bitmap = new Bitmap(size.Width, size.Height))
                    {
                        using (var canvas = Graphics.FromImage(bitmap)) IndicatorView.Draw(canvas, size, style, palette, weekly, 0, model, 0, -1, colors);
                        Check(bitmap.GetPixel(0, 0).A == 0, "New styles must retain transparent capsule corners."); graphics.DrawImageUnscaled(bitmap, 20, y + 17);
                    }
                }
                board.Save(Path.Combine(directory, style == IndicatorStyle.DotMatrix ? "dot-matrix-style-check.png" : "color-strip-style-check.png"));
            }
        }
    }
}
