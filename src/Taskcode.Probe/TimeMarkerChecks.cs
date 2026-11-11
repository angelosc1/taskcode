using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Taskcode.Probe
{
    internal static class TimeMarkerChecks
    {
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static Bitmap Render(IndicatorStyle style, bool weekly, int scale)
        {
            var size = new Size(IndicatorView.LogicalWidth(style, weekly) * scale, IndicatorView.LogicalHeight * scale);
            var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(bitmap)) { graphics.Clear(Color.FromArgb(28, 29, 32)); IndicatorView.Draw(graphics, size, style, TaskbarTheme.Create(true), weekly); }
            return bitmap;
        }
        private static bool Differs(Bitmap a, Bitmap b)
        {
            for (int y = 0; y < a.Height; y++) for (int x = 0; x < a.Width; x++) if (a.GetPixel(x, y) != b.GetPixel(x, y)) return true;
            return false;
        }
        internal static void Run(string directory)
        {
            var savedMarker = IndicatorView.Marker; bool savedPace = IndicatorView.PaceAlert;
            try
            {
                Check(new Settings().TimeMarker == TimeMarker.ArrowsBetween && !new Settings().PaceAlert, "Arrows are the default time marker; pace colors start off.");
                var markers = (TimeMarker[])Enum.GetValues(typeof(TimeMarker));
                var styles = (IndicatorStyle[])Enum.GetValues(typeof(IndicatorStyle));
                const int scale = 3;
                int rowHeight = IndicatorView.LogicalHeight * scale + 6, columnWidth = 0;
                foreach (var style in styles) columnWidth = Math.Max(columnWidth, IndicatorView.LogicalWidth(style, true) * scale + 12);
                using (var board = new Bitmap(columnWidth * (markers.Length + 1), rowHeight * styles.Length * 2))
                using (var graphics = Graphics.FromImage(board))
                {
                    graphics.Clear(Color.FromArgb(18, 19, 21));
                    for (int row = 0; row < styles.Length * 2; row++)
                    {
                        var style = styles[row / 2]; bool weekly = row % 2 == 1;
                        IndicatorView.Marker = TimeMarker.None; IndicatorView.PaceAlert = false;
                        using (var plain = Render(style, weekly, scale))
                        {
                            graphics.DrawImageUnscaled(plain, 0, row * rowHeight);
                            for (int column = 0; column < markers.Length; column++)
                            {
                                IndicatorView.Marker = markers[column]; IndicatorView.PaceAlert = column % 2 == 0;
                                using (var marked = Render(style, weekly, scale))
                                {
                                    if (markers[column] != TimeMarker.None) Check(Differs(plain, marked), "Time marker " + markers[column] + " must be visible in " + style + (weekly ? " weekly" : ""));
                                    graphics.DrawImageUnscaled(marked, (column + 1) * columnWidth, row * rowHeight);
                                }
                            }
                        }
                    }
                    board.Save(Path.Combine(directory, "time-markers.png"), ImageFormat.Png);
                }
                // The cache key changes with the option and, while a marker is on, with each minute.
                var now = new DateTime(2026, 11, 11, 12, 0, 30, DateTimeKind.Utc);
                IndicatorView.Marker = TimeMarker.None; string off = IndicatorView.MarkerKey(now);
                Check(off == IndicatorView.MarkerKey(now.AddMinutes(5)), "Without a marker the surface must not redraw every minute.");
                IndicatorView.Marker = TimeMarker.Line;
                Check(IndicatorView.MarkerKey(now) != off && IndicatorView.MarkerKey(now) != IndicatorView.MarkerKey(now.AddMinutes(1)), "Markers must redraw as time advances.");
            }
            finally { IndicatorView.Marker = savedMarker; IndicatorView.PaceAlert = savedPace; }
        }
    }
}
