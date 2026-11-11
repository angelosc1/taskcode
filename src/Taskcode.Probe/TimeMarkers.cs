using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Taskcode.Probe
{
    // How the indicator shows elapsed period time, like the white arrow on the Details bars.
    // Enum names are saved in settings.xml; append new values instead of renaming.
    internal enum TimeMarker { None, ArrowsBetween, ArrowsOutside, Line, Trail }

    internal sealed partial class IndicatorView
    {
        internal static TimeMarker Marker = TimeMarker.None;
        internal static bool PaceAlert;
        private static readonly double[] DemoElapsed = { .55, .25, .77, .28 };
        private static bool Arrows { get { return Marker == TimeMarker.ArrowsBetween || Marker == TimeMarker.ArrowsOutside; } }

        // Changes once a minute while a marker is on, so cached surfaces redraw as time advances.
        internal static string MarkerKey(DateTime now)
        { return "|t" + (int)Marker + (PaceAlert ? "a" : "") + (Marker == TimeMarker.None ? "" : ":" + now.Ticks / TimeSpan.TicksPerMinute); }

        internal static double? Elapsed(UsageDemo usage, int index, DateTime now)
        {
            if (usage == null) return DemoElapsed[index];
            if (usage.DisplayUsed(index, now) < 0) return null;
            return usage.Readings[index].Period.ElapsedAt(now);
        }
        internal static double? OptionalElapsed(OptionalProvider provider, int index, DateTime now)
        {
            var snapshot = provider.Snapshot;
            if (snapshot.Failed || index >= snapshot.Quotas.Count) return null;
            var quota = snapshot.Quotas[index];
            if (!quota.StartUtc.HasValue || !quota.ResetUtc.HasValue || quota.ResetUtc.Value <= quota.StartUtc.Value) return null;
            return Math.Max(0, Math.Min(1, (now - quota.StartUtc.Value).TotalSeconds / (quota.ResetUtc.Value - quota.StartUtc.Value).TotalSeconds));
        }
        private static bool Marked(int used, double? elapsed) { return Marker != TimeMarker.None && used >= 0 && elapsed.HasValue; }
        private static bool AheadOfPace(int used, double elapsed) { return used / 100.0 > elapsed + .005; }
        // Pace colors: green while usage stays within elapsed time, white (the normal marker) once it gets ahead.
        private static Color MarkerColor(TaskbarTheme palette, int used, double elapsed)
        { return PaceAlert && !palette.HighContrast && !AheadOfPace(used, elapsed) ? UsageBarPalette.Green : palette.Text; }
        private static Color TrailColor(TaskbarTheme palette, int used = -1, double elapsed = 0)
        {
            if (palette.HighContrast) return palette.Muted;
            return UiSurface.Blend(palette.Track, PaceAlert && used >= 0 && !AheadOfPace(used, elapsed) ? UsageBarPalette.Green : palette.Text, .35);
        }
        private static bool PaintOvershoot(TaskbarTheme palette, int used, double elapsed)
        { return Marker == TimeMarker.Trail && PaceAlert && !palette.HighContrast && AheadOfPace(used, elapsed); }

        // Weekly rows: 5h is row 0 (top), 7d row 1 (bottom). "Between" puts both arrows in the gap
        // between the rows; "outside" puts each arrow on the far side of its own bar.
        private static bool ArrowBelow(bool weekly, int window)
        {
            bool between = Marker == TimeMarker.ArrowsBetween;
            return weekly ? (window == 0) == between : between;
        }
        private static void FillTriangle(Graphics graphics, Color color, PointF tip, PointF baseCenter, float halfWidth)
        {
            float dx = baseCenter.X - tip.X, dy = baseCenter.Y - tip.Y, length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length <= 0) return;
            float px = -dy / length * halfWidth, py = dx / length * halfWidth;
            using (var brush = new SolidBrush(color))
                graphics.FillPolygon(brush, new[] { tip, new PointF(baseCenter.X + px, baseCenter.Y + py), new PointF(baseCenter.X - px, baseCenter.Y - py) });
        }
        // Arrow or line for a horizontal bar spanning x0..x1 between top and bottom.
        private static void DrawBarMarker(Graphics graphics, float x0, float x1, float top, float bottom, int used, double? elapsed, bool below, TaskbarTheme palette)
        {
            if (!Marked(used, elapsed) || Marker == TimeMarker.Trail) return;
            float x = x0 + (x1 - x0) * (float)elapsed.Value; var color = MarkerColor(palette, used, elapsed.Value);
            if (Marker == TimeMarker.Line) { using (var brush = new SolidBrush(color)) graphics.FillRectangle(brush, x - .5f, top - 1.8f, 1, bottom - top + 3.6f); return; }
            if (below) FillTriangle(graphics, color, new PointF(x, bottom + .6f), new PointF(x, bottom + 3.4f), 1.9f);
            else FillTriangle(graphics, color, new PointF(x, top - .6f), new PointF(x, top - 3.4f), 1.9f);
        }
        // Color Strip fills from the bottom: a horizontal line, or an arrow on the left pointing right.
        private static void DrawStripMarker(Graphics graphics, RectangleF strip, int used, double? elapsed, TaskbarTheme palette)
        {
            if (!Marked(used, elapsed) || Marker == TimeMarker.Trail) return;
            float y = strip.Bottom - strip.Height * (float)elapsed.Value; var color = MarkerColor(palette, used, elapsed.Value);
            if (Marker == TimeMarker.Line) { using (var brush = new SolidBrush(color)) graphics.FillRectangle(brush, strip.Left - 1.8f, y - .5f, strip.Width + 3.6f, 1); return; }
            FillTriangle(graphics, color, new PointF(strip.Left - .6f, y), new PointF(strip.Left - 3.4f, y), 1.9f);
        }
        // Styles without a bar (Essential, Sparkline) get a thin time rule under the value.
        private static void DrawTimeRule(Graphics graphics, float x0, float x1, float y, int used, double? elapsed, TaskbarTheme palette)
        {
            if (!Marked(used, elapsed)) return;
            float width = x1 - x0, x = x0 + width * (float)elapsed.Value; var color = MarkerColor(palette, used, elapsed.Value);
            using (var track = new SolidBrush(palette.Track)) graphics.FillRectangle(track, x0, y - .4f, width, .8f);
            if (Marker == TimeMarker.Trail) { using (var fill = new SolidBrush(Color.FromArgb(215, color))) graphics.FillRectangle(fill, x0, y - .4f, width * (float)elapsed.Value, .8f); return; }
            if (Marker == TimeMarker.Line) { using (var brush = new SolidBrush(color)) graphics.FillRectangle(brush, x - .5f, y - 1.6f, 1, 3.2f); return; }
            FillTriangle(graphics, color, new PointF(x, y + .9f), new PointF(x, y + 3.5f), 1.8f);
        }
        // Trail for a continuous horizontal bar: drawn under the usage fill (lit track) and over it (overshoot).
        private static void DrawTrailUnder(Graphics graphics, float x, float y, float width, float height, int used, double? elapsed, TaskbarTheme palette)
        {
            if (Marker != TimeMarker.Trail || !Marked(used, elapsed)) return;
            using (var lit = new SolidBrush(TrailColor(palette, used, elapsed.Value))) graphics.FillRectangle(lit, x, y, width * (float)elapsed.Value, height);
        }
        private static void DrawTrailOver(Graphics graphics, float x, float y, float width, float height, int used, double? elapsed, TaskbarTheme palette)
        {
            if (!Marked(used, elapsed) || !PaintOvershoot(palette, used, elapsed.Value)) return;
            float from = width * (float)elapsed.Value, to = width * Math.Min(1, used / 100f);
            using (var brush = new SolidBrush(palette.Text)) graphics.FillRectangle(brush, x + from, y, to - from, height);
        }
        // Color of one block in a ten-block meter under the trail option.
        private static Color BlockColor(int block, int used, double? elapsed, Color fill, TaskbarTheme palette)
        {
            bool filled = block < (int)Math.Round(used / 10.0);
            if (Marker != TimeMarker.Trail || !Marked(used, elapsed)) return filled ? fill : palette.Track;
            int passed = (int)Math.Round(elapsed.Value * 10);
            if (!filled) return block < passed ? TrailColor(palette, used, elapsed.Value) : palette.Track;
            return block >= passed && PaintOvershoot(palette, used, elapsed.Value) ? palette.Text : fill;
        }
        // Donut: start tick at 12 o'clock, then line, inward arrow or outer time ring.
        private static void DrawDonutTime(Graphics graphics, RectangleF ring, float stroke, int used, double? elapsed, TaskbarTheme palette)
        {
            if (Marker == TimeMarker.None || used < 0) return;
            float cx = ring.X + ring.Width / 2, cy = ring.Y + ring.Height / 2, radius = ring.Width / 2, outer = radius + stroke / 2;
            using (var start = new Pen(palette.Muted, .9f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                graphics.DrawLine(start, cx, cy - outer - .5f, cx, cy - outer - 2.3f);
            if (!elapsed.HasValue) return;
            double angle = -Math.PI / 2 + 2 * Math.PI * elapsed.Value; float cos = (float)Math.Cos(angle), sin = (float)Math.Sin(angle);
            var color = MarkerColor(palette, used, elapsed.Value);
            if (Marker == TimeMarker.Line)
            {
                using (var pen = new Pen(color, 1.1f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    graphics.DrawLine(pen, cx + (radius - stroke / 2 - 1) * cos, cy + (radius - stroke / 2 - 1) * sin, cx + (outer + 1.2f) * cos, cy + (outer + 1.2f) * sin);
            }
            else if (Marker == TimeMarker.Trail)
            {
                float trailRadius = outer + 1.6f; var bounds = new RectangleF(cx - trailRadius, cy - trailRadius, trailRadius * 2, trailRadius * 2);
                using (var track = new Pen(Color.FromArgb(140, palette.Track), .8f)) graphics.DrawEllipse(track, bounds);
                if (elapsed.Value > 0) using (var pen = new Pen(Color.FromArgb(215, color), .8f)) graphics.DrawArc(pen, bounds, -90, (float)(elapsed.Value * 360));
                if (PaintOvershoot(palette, used, elapsed.Value))
                    using (var pen = new Pen(palette.Text, stroke)) graphics.DrawArc(pen, ring, (float)(-90 + elapsed.Value * 360), (float)((Math.Min(100, used) / 100.0 - elapsed.Value) * 360));
            }
            else
            {
                // Inside the ring: the capsule leaves no room above a 22-unit donut.
                float tip = radius - stroke / 2 - .5f, baseRadius = tip - 2.6f;
                FillTriangle(graphics, color, new PointF(cx + tip * cos, cy + tip * sin), new PointF(cx + baseRadius * cos, cy + baseRadius * sin), 1.7f);
            }
        }
    }
}
