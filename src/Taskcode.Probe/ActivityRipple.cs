using System;
using System.Diagnostics;
using System.Drawing;

namespace Taskcode.Probe
{
    internal static class ActivityRipple
    {
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        internal static double Seconds { get { return Clock.Elapsed.TotalSeconds; } }
        internal static double PhaseAt(double seconds, int ring) { return ((seconds / 2.21 - ring * 0.5) % 1 + 1) % 1; }
        internal static void Draw(Graphics graphics, Color color, PointF center, double seconds = -1)
        {
            if (!SpeedArrows.IsAnimated && seconds < 0)
            {
                using (var dot = new SolidBrush(color)) graphics.FillEllipse(dot, center.X + 8, center.Y - 9, 3, 3);
                return;
            }
            if (seconds < 0) seconds = Seconds;
            for (int ring = 0; ring < 2; ring++)
            {
                double phase = PhaseAt(seconds, ring);
                float radius = (float)(7.8 + phase * 4.2);
                int alpha = (int)Math.Round(140 * Math.Pow(1 - phase, 1.6));
                using (var pen = new Pen(Color.FromArgb(alpha, color), 0.85f))
                    graphics.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
            }
        }
    }
}
