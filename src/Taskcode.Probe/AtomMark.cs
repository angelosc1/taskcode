using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Taskcode.Probe
{
    // Electrons around a working provider's logo. Count follows the usage level that already
    // sets the logo's spin and the Details speed arrows: 2 = lasts past the reset,
    // 4 = runs out near the reset, 6 = runs out before the reset.
    // Each electron has its own tilted path, neighbours run in opposite directions, and a
    // comet tail in the provider color trails behind while animations are on. No orbit lines.
    internal sealed partial class IndicatorView
    {
        // Appearance > Usage atom. Off keeps only the spinning logo.
        internal static bool ShowAtom = true;
        internal const float AtomRadius = 10.5f;
        private const float OrbitFlattening = .36f, ElectronSize = 1.15f, TailRadians = 1f;
        private const int TailSegments = 18;
        internal static int ElectronCount(int level) { return Math.Max(0, Math.Min(3, level)) * 2; }
        private static PointF OrbitPoint(float tilt, double angle, out double depth)
        {
            depth = Math.Sin(angle);
            float x = AtomRadius * (float)Math.Cos(angle), y = AtomRadius * OrbitFlattening * (float)depth;
            float c = (float)Math.Cos(tilt), s = (float)Math.Sin(tilt);
            return new PointF(x * c - y * s, x * s + y * c);
        }
        // Draws the parts behind the logo (back) or in front of it.
        private static void DrawAtom(Graphics graphics, Color color, int level, double spin, PointF center, bool back)
        {
            int count = ElectronCount(level);
            if (count == 0) return;
            bool tails = SpeedArrows.IsAnimated;
            Color electron = Color.FromArgb(Math.Min(255, (int)(color.R * 1.12)), Math.Min(255, (int)(color.G * 1.12)), Math.Min(255, (int)(color.B * 1.12)));
            var state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TranslateTransform(center.X, center.Y);
                for (int index = 0; index < count; index++)
                {
                    // Paths fan out over half a turn; odd electrons run the other way.
                    float tilt = -.35f + index * (float)Math.PI / count;
                    int direction = index % 2 == 0 ? 1 : -1;
                    double angle = index * 1.3 + direction * spin, headDepth;
                    var head = OrbitPoint(tilt, angle, out headDepth);
                    float size = ElectronSize * (.78f + .32f * (float)(headDepth + 1) / 2);
                    if (tails)
                    {
                        var previous = head; double previousDepth = headDepth;
                        for (int step = 1; step <= TailSegments; step++)
                        {
                            float fraction = step / (float)TailSegments; double depth;
                            var point = OrbitPoint(tilt, angle - direction * TailRadians * fraction, out depth);
                            if (((previousDepth + depth) / 2 < 0) == back)
                            {
                                double alpha = Math.Pow(1 - fraction, 1.3) * .85 * (back ? .6 : 1);
                                using (var pen = new Pen(Color.FromArgb((int)(alpha * 255), electron), size * 1.6f * (1 - fraction * .85f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                                    graphics.DrawLine(pen, previous, point);
                            }
                            previous = point; previousDepth = depth;
                        }
                    }
                    if ((headDepth < 0) != back) continue;
                    using (var brush = new SolidBrush(back ? Color.FromArgb(153, electron) : electron))
                        graphics.FillEllipse(brush, head.X - size, head.Y - size, size * 2, size * 2);
                    if (!back)
                        using (var shine = new SolidBrush(Color.FromArgb(140, Color.White)))
                            graphics.FillEllipse(shine, head.X - size * .7f, head.Y - size * .7f, size * .7f, size * .7f);
                }
            }
            finally { graphics.Restore(state); }
        }
    }
}
