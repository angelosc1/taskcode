using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal static class SpeedArrows
    {
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        internal static double Seconds { get { return Clock.Elapsed.TotalSeconds; } }
        private static DateTime nextPreferenceRead;
        private static bool animationsEnabled = true;
        internal static bool IsAnimated
        {
            get
            {
                if (DateTime.UtcNow >= nextPreferenceRead)
                {
                    nextPreferenceRead = DateTime.UtcNow.AddSeconds(1);
                    bool enabled;
                    if (Native.SystemParametersInfo(0x1042, 0, out enabled, 0)) animationsEnabled = enabled;
                }
                return animationsEnabled && !SystemInformation.HighContrast;
            }
        }
        internal static int Frame { get { return IsAnimated ? (int)((Clock.Elapsed.TotalSeconds % 60) * 30) : 0; } }
        internal static double OpacityAt(double seconds, int index)
        {
            // A raised cosine gives a soft fade; delayed peaks travel left to right.
            double phase = seconds / 1.25 - index * 0.16;
            double wave = 0.5 + 0.5 * Math.Cos(2 * Math.PI * phase);
            return 0.20 + 0.80 * wave * wave;
        }
        internal static void Draw(Graphics graphics, Color color, int count, RectangleF bounds, double seconds = -1)
        {
            count = Math.Max(0, Math.Min(3, count));
            if (count == 0 || bounds.Width <= 0 || bounds.Height <= 0) return;
            bool animate = seconds >= 0 || IsAnimated;
            if (seconds < 0) seconds = Clock.Elapsed.TotalSeconds;
            float height = bounds.Height;
            float width = height * 0.85f, gap = height * 0.10f;
            float total = count * width + (count - 1) * gap;
            if (total > bounds.Width)
            { float scale = bounds.Width / total; height *= scale; width *= scale; gap *= scale; total = bounds.Width; }
            float x = bounds.Left + (bounds.Width - total) / 2;
            float y = bounds.Top + (bounds.Height - height) / 2;
            var state = graphics.Save();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            for (int index = 0; index < count; index++)
            {
                float left = x + index * (width + gap);
                using (var shape = new GraphicsPath())
                using (var fill = new SolidBrush(Color.FromArgb((int)Math.Round(255 * (animate ? OpacityAt(seconds, index) : 1)), color)))
                {
                    // Rounded, filled arrowheads rather than stroked chevron characters.
                    shape.StartFigure();
                    shape.AddLine(left + width * 0.17f, y + height * 0.06f, left + width * 0.90f, y + height * 0.43f);
                    shape.AddBezier(left + width * 0.90f, y + height * 0.43f, left + width * 1.02f, y + height * 0.48f, left + width * 1.02f, y + height * 0.52f, left + width * 0.90f, y + height * 0.57f);
                    shape.AddLine(left + width * 0.90f, y + height * 0.57f, left + width * 0.17f, y + height * 0.94f);
                    shape.AddBezier(left + width * 0.17f, y + height * 0.94f, left + width * 0.08f, y + height * 0.99f, left + width * 0.04f, y + height * 0.93f, left + width * 0.04f, y + height * 0.84f);
                    shape.AddLine(left + width * 0.04f, y + height * 0.84f, left + width * 0.04f, y + height * 0.16f);
                    shape.AddBezier(left + width * 0.04f, y + height * 0.16f, left + width * 0.04f, y + height * 0.07f, left + width * 0.08f, y + height * 0.01f, left + width * 0.17f, y + height * 0.06f);
                    shape.CloseFigure();
                    graphics.FillPath(fill, shape);
                }
            }
            graphics.Restore(state);
        }
    }

    internal sealed class SpeedReadout : Control
    {
        internal SpeedEstimate Estimate;
        internal bool HasArrows { get { return Estimate != null && Estimate.State == SpeedState.Active && Estimate.Level > 0; } }
        internal SpeedReadout()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Font = new Font("Segoe UI", 8.5f);
            AccessibleRole = AccessibleRole.StaticText;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Estimate == null) return;
            float textX = 0;
            if (HasArrows)
            { SpeedArrows.Draw(e.Graphics, ForeColor, Estimate.Level, new RectangleF(0, 5, 39, 11)); textX = 45; }
            using (var text = new SolidBrush(ForeColor)) e.Graphics.DrawString(Estimate.Summary, Font, text, textX, 0);
        }
    }
}
