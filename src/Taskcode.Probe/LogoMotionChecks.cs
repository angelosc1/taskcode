using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace Taskcode.Probe
{
    internal static class LogoMotionChecks
    {
        internal static void Run(string directory)
        {
            var now = DateTime.UtcNow;
            var usage = new UsageDemo(); usage.BeginLive();
            Check(LogoMotion.LevelAt(usage, 0, now) == 0 && LogoMotion.LevelAt(usage, 1, now) == 0, "Idle providers must face forward without rotating.");
            usage.ActivityKey = 2;
            Check(LogoMotion.LevelAt(usage, 0, now) == 0 && LogoMotion.LevelAt(usage, 1, now) == 1, "An active provider must rotate slowly while speed is still calculating, independently of the other provider.");
            usage.CoreEnabled[1] = false;
            Check(LogoMotion.LevelAt(usage, 1, now) == 0, "Disconnected providers must not rotate.");
            var demo = new UsageDemo();
            for (int provider = 0; provider < 2; provider++)
            {
                int expected = 1;
                for (int window = 0; window < 2; window++)
                {
                    var estimate = demo.DetailSpeedAt(provider * 2 + window, now);
                    if (estimate.State == SpeedState.Active && !estimate.PeriodAverage) expected = Math.Max(expected, estimate.Level);
                }
                Check(LogoMotion.LevelAt(demo, provider, now) == expected, "Rotation must use the same recent-speed levels as Details, excluding period-average estimates.");
            }
            Check(LogoMotion.DegreesPerSecond(0) == 0 && LogoMotion.DegreesPerSecond(1) < LogoMotion.DegreesPerSecond(2) && LogoMotion.DegreesPerSecond(2) < LogoMotion.DegreesPerSecond(3), "Rotation speed must increase with arrow level.");
            Check(LogoMotion.DegreesPerSecond(1) == 60 && LogoMotion.DegreesPerSecond(2) == 155 && LogoMotion.DegreesPerSecond(3) == 280, "Slow and medium rotation must use the increased speeds while fast remains unchanged.");
            Check(Math.Abs(ExtrudedLogo.Depth / 24 - .15) < .0001, "Extrusion must remain 15 percent of the reference height.");
            LogoMotion.Update(demo, now); LogoMotion.Advance(.1);
            if (SpeedArrows.IsAnimated) Check(LogoMotion.HasReflection(0) && LogoMotion.HasReflection(1) && LogoMotion.Angle(0) > 0 && LogoMotion.Angle(1) > 0, "Active logos must advance on their own axes.");
            usage.ActivityKey = 0; LogoMotion.Update(usage, now);
            Check(LogoMotion.Angle(0) == 0 && LogoMotion.Angle(1) == 0 && !LogoMotion.IsMoving && !LogoMotion.HasReflection(0) && !LogoMotion.HasReflection(1), "Idle providers must stop and restore both front faces without reflection.");
            double[] angles = { 0, 35, 90, 145, 180, 215, 270, 325 };
            using (var board = new Bitmap(960, 310))
            using (var graphics = Graphics.FromImage(board))
            using (var font = new Font("Segoe UI", 10))
            {
                graphics.Clear(Color.FromArgb(23, 27, 29));
                for (int i = 0; i < angles.Length; i++)
                {
                    using (var text = new SolidBrush(Color.White)) graphics.DrawString(angles[i] + " degrees", font, text, 12 + i * 120, 8);
                    using (var brush = new SolidBrush(IndicatorView.Claude)) BrandMarks.DrawClaude(graphics, brush, new RectangleF(18 + i * 120, 45, 80, 80), angles[i]);
                    using (var brush = new SolidBrush(IndicatorView.Codex)) BrandMarks.DrawCodex(graphics, brush, new RectangleF(18 + i * 120, 155, 80, 80), angles[i]);
                    foreach (int provider in new[] { 0, 1 })
                    using (var native = new Bitmap(24, 24))
                    {
                        using (var canvas = Graphics.FromImage(native))
                        using (var brush = new SolidBrush(provider == 0 ? IndicatorView.Claude : IndicatorView.Codex))
                        {
                            if (provider == 0) BrandMarks.DrawClaude(canvas, brush, new RectangleF(4, 4, 16, 16), angles[i]);
                            else BrandMarks.DrawCodex(canvas, brush, new RectangleF(4, 4, 16, 16), angles[i]);
                        }
                        int visible = 0;
                        for (int y = 0; y < 24; y++) for (int x = 0; x < 24; x++) if (native.GetPixel(x, y).A > 8) visible++;
                        Check(visible > 2 && native.GetPixel(0, 0).A == 0, "Every orientation, including edge-on, must retain visible volume and transparent corners at taskbar size.");
                        graphics.DrawImageUnscaled(native, 35 + i * 120 + provider * 26, 266);
                    }
                }
                board.Save(Path.Combine(directory, "logos-3d-check.png"));
            }
            var clock = Stopwatch.StartNew();
            using (var bitmap = new Bitmap(48, 24))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var brush = new SolidBrush(Color.White))
                for (int frame = 0; frame < 120; frame++)
                {
                    graphics.Clear(Color.Transparent);
                    BrandMarks.DrawClaude(graphics, brush, new RectangleF(2, 4, 16, 16), frame * 3);
                    BrandMarks.DrawCodex(graphics, brush, new RectangleF(26, 4, 16, 16), frame * 3);
                }
            File.WriteAllText(Path.Combine(directory, "logos-3d-timing.txt"), "Two native-size logos: " + (clock.Elapsed.TotalMilliseconds / 120).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " ms/frame (120 frames).");
        }
        private static void Check(bool result, string message) { if (!result) throw new InvalidOperationException(message); }
    }
}
