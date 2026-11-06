using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal static class UiSurface
    {
        internal static Color Blend(Color background, Color foreground, double amount)
        { return Color.FromArgb((int)(background.R * (1 - amount) + foreground.R * amount), (int)(background.G * (1 - amount) + foreground.G * amount), (int)(background.B * (1 - amount) + foreground.B * amount)); }
        internal static Color Card(TaskbarTheme theme) { return Blend(theme.PreviewBackground, theme.Text, .035); }
        internal static GraphicsPath Shape(RectangleF bounds, float radius)
        {
            float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure(); return path;
        }
        internal static void Draw(Graphics graphics, RectangleF bounds, Color fill, Color border, float radius)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Shape(bounds, radius))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border, .7f))
            { graphics.FillPath(brush, path); graphics.DrawPath(pen, path); }
        }
        internal static void Round(Control control, float radius)
        {
            using (var path = Shape(new RectangleF(0, 0, control.Width, control.Height), radius))
            {
                var old = control.Region; control.Region = new Region(path); if (old != null) old.Dispose();
            }
        }
    }
}
