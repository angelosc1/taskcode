using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Taskcode.Probe
{
    internal static class LogoMotion
    {
        private static readonly int[] levels = new int[2 + ProviderCatalog.Count];
        private static readonly double[] angles = new double[2 + ProviderCatalog.Count];
        internal static double DegreesPerSecond(int level)
        { return level <= 0 ? 0 : level == 1 ? 60 : level == 2 ? 155 : 280; }
        internal static int LevelAt(UsageDemo usage, int provider, DateTime now)
        {
            if (provider >= 2) return provider - 2 < usage.OptionalProviders.Length ? usage.OptionalProviders[provider - 2].RotationLevel(now) : 0;
            if (!usage.CoreEnabled[provider] || (usage.ActivityAt(now) & (1 << provider)) == 0) return 0;
            int level = 1; // Working, with too few observations to estimate speed yet.
            for (int window = 0; window < 2; window++)
            {
                var speed = usage.DetailSpeedAt(provider * 2 + window, now);
                if (speed.State == SpeedState.Active && !speed.PeriodAverage) level = Math.Max(level, speed.Level);
            }
            return Math.Min(3, level);
        }
        internal static void Update(UsageDemo usage, DateTime now)
        {
            for (int provider = 0; provider < levels.Length; provider++)
            {
                levels[provider] = LevelAt(usage, provider, now);
                if (levels[provider] == 0 || !SpeedArrows.IsAnimated) angles[provider] = 0;
            }
        }
        internal static bool IsMoving
        {
            get { if (!SpeedArrows.IsAnimated) return false; foreach (int level in levels) if (level > 0) return true; return false; }
        }
        internal static bool HasReflection(int provider) { return levels[provider] > 0 && SpeedArrows.IsAnimated; }
        internal static double Angle(int provider) { return SpeedArrows.IsAnimated ? angles[provider] : 0; }
        internal static void Advance(double seconds)
        {
            for (int provider = 0; provider < levels.Length; provider++)
                angles[provider] = levels[provider] == 0 || !SpeedArrows.IsAnimated ? 0 :
                    (angles[provider] + Math.Max(0, Math.Min(.1, seconds)) * DegreesPerSecond(levels[provider])) % 360;
        }
    }

    internal static class ExtrudedLogo
    {
        internal const float Depth = 24 * .15f;
        private sealed class Wall
        {
            internal PointF[] Points;
            internal double Z, Light;
        }
        private static Color Shade(Color color, double amount)
        { return Color.FromArgb(color.A, (int)Math.Min(255, color.R * amount), (int)Math.Min(255, color.G * amount), (int)Math.Min(255, color.B * amount)); }
        internal static void Draw(Graphics graphics, GraphicsPath source, Color color, RectangleF bounds, double degrees, bool reflection = true)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            double theta = -degrees * Math.PI / 180, c = Math.Cos(theta), s = Math.Sin(theta);
            float sx = bounds.Width / 24, sy = bounds.Height / 24;
            float cx = bounds.X + bounds.Width / 2, cy = bounds.Y + bounds.Height / 2;
            var saved = graphics.Save();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                var walls = new List<Wall>();
                using (var flat = (GraphicsPath)source.Clone())
                {
                    flat.Flatten(null, .035f);
                    PointF[] points = flat.PathPoints; byte[] types = flat.PathTypes;
                    int start = 0;
                    for (int end = 0; end < points.Length; end++)
                    {
                        if ((types[end] & 128) == 0 && end != points.Length - 1) continue;
                        double area = 0;
                        for (int i = start; i <= end; i++)
                        { var a = points[i]; var b = points[i == end ? start : i + 1]; area += a.X * b.Y - b.X * a.Y; }
                        for (int i = start; i <= end; i++)
                        {
                            var a = points[i]; var b = points[i == end ? start : i + 1];
                            double dx = b.X - a.X, dy = b.Y - a.Y, length = Math.Sqrt(dx * dx + dy * dy);
                            if (length < .0001) continue;
                            double nx = dy / length * (area >= 0 ? 1 : -1), ny = -dx / length * (area >= 0 ? 1 : -1);
                            // Hole contours must face into the opening, irrespective of SVG winding.
                            if (source.IsVisible((a.X + b.X) / 2 + (float)nx * .02f, (a.Y + b.Y) / 2 + (float)ny * .02f)) { nx = -nx; ny = -ny; }
                            double nz = -nx * s;
                            if (nz <= 0) continue;
                            double light = Math.Max(0, nx * c * -.48 + ny * -.58 + nz * .66);
                            walls.Add(new Wall { Z = -((a.X + b.X) / 2 - 12) * s, Light = .68 + .25 * light,
                                Points = new[] { Project(a, Depth / 2, c, s, sx, sy, cx, cy), Project(b, Depth / 2, c, s, sx, sy, cx, cy),
                                    Project(b, -Depth / 2, c, s, sx, sy, cx, cy), Project(a, -Depth / 2, c, s, sx, sy, cx, cy) } });
                        }
                        start = end + 1;
                    }
                }
                walls.Sort(delegate(Wall a, Wall b) { return a.Z.CompareTo(b.Z); });
                foreach (var wall in walls) using (var brush = new SolidBrush(Shade(color, wall.Light))) graphics.FillPolygon(brush, wall.Points);
                if (Math.Abs(c) < .0001) return;
                using (var face = (GraphicsPath)source.Clone())
                using (var transform = new Matrix((float)c * sx, 0, 0, sy, cx - 12 * (float)c * sx + (float)s * (c >= 0 ? Depth / 2 : -Depth / 2) * sx, cy - 12 * sy))
                {
                    face.Transform(transform);
                    using (var brush = new SolidBrush(Shade(color, c >= 0 ? 1 : .90))) graphics.FillPath(brush, face);
                    if (!reflection || System.Windows.Forms.SystemInformation.HighContrast) return;
                    // Fixed screen-space light: same diagonal and sweep on both faces.
                    float sweep = (float)Math.Sin(2 * theta) * bounds.Width * .825f;
                    float span = bounds.Width;
                    PointF center = new PointF(cx + sweep, cy);
                    using (var shine = new LinearGradientBrush(new PointF(center.X - span / 2, center.Y - span * .275f), new PointF(center.X + span / 2, center.Y + span * .275f), Color.Transparent, Color.White))
                    {
                        int alpha = (int)(195 * Math.Pow(Math.Abs(c), 8));
                        var blend = new ColorBlend(8);
                        blend.Positions = new[] { 0f, .32f, .43f, .49f, .52f, .58f, .69f, 1f };
                        double[] opacity = { 0, 0, .22, .8, .9, .28, 0, 0 };
                        blend.Colors = Array.ConvertAll(opacity, value => Color.FromArgb((int)(alpha * value), Color.White));
                        shine.InterpolationColors = blend; shine.WrapMode = WrapMode.TileFlipXY;
                        graphics.FillPath(shine, face);
                    }
                }
            }
            finally { graphics.Restore(saved); }
        }
        private static PointF Project(PointF p, float z, double c, double s, float sx, float sy, float cx, float cy)
        { return new PointF(cx + (float)((p.X - 12) * c + z * s) * sx, cy + (p.Y - 12) * sy); }
    }
}
