using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Taskcode.Probe
{
    internal static class TrayArtwork
    {
        internal static Icon Create()
        {
            using (var bitmap = new Bitmap(32, 32))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var background = new SolidBrush(Color.FromArgb(35, 40, 47)))
            using (var mint = new Pen(IndicatorView.Codex, 3))
            using (var warm = new Pen(IndicatorView.Claude, 3))
            using (var shape = new GraphicsPath())
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                shape.AddArc(1, 1, 10, 10, 180, 90);
                shape.AddArc(21, 1, 10, 10, 270, 90);
                shape.AddArc(21, 21, 10, 10, 0, 90);
                shape.AddArc(1, 21, 10, 10, 90, 90);
                shape.CloseFigure();
                graphics.FillPath(background, shape);
                mint.StartCap = mint.EndCap = warm.StartCap = warm.EndCap = LineCap.Round;
                graphics.DrawLines(mint, new[] { new Point(11, 10), new Point(6, 16), new Point(11, 22) });
                graphics.DrawLines(mint, new[] { new Point(21, 10), new Point(26, 16), new Point(21, 22) });
                graphics.DrawLine(warm, 18, 9, 14, 23);
                IntPtr handle = bitmap.GetHicon();
                try { using (var icon = Icon.FromHandle(handle)) return (Icon)icon.Clone(); }
                finally { Native.DestroyIcon(handle); }
            }
        }
    }
}
