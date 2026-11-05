using System;
using System.Drawing;

namespace Taskcode.Probe
{
    internal static class ActivityRipple
    {
        internal const double Duration = 2.4;
        internal static double Seconds { get { return SpeedArrows.Seconds; } }
        internal static double PhaseAt(double seconds, int ring) { return ((seconds / Duration - ring * 0.5) % 1 + 1) % 1; }
        internal static float RadiusAt(double phase) { return (float)(5.5 + phase * 6.0); }
        internal static int AlphaAt(double phase) { double wave = Math.Sin(Math.PI * phase); return (int)Math.Round(190 * wave * wave); }
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
                float radius = RadiusAt(phase);
                int alpha = AlphaAt(phase);
                using (var pen = new Pen(Color.FromArgb(alpha, color), 1f))
                    graphics.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
            }
        }
    }
}
