using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal sealed partial class IndicatorWindow
    {
        private readonly Timer scrollTimer = new Timer { Interval = 33 };
        private double scrollClock, scrollPosition;
        private int contentWidth;
        private int renderedScroll = -1;
        internal bool ScrollPaused;
        internal int ContentWidth
        {
            get { return Math.Max(Width, contentWidth); }
            set { if (contentWidth != value) { contentWidth = value; scrollPosition = 0; InvalidateSurface(); } }
        }
        internal bool IsScrolling { get { return contentWidth > Width && Width > 0; } }
        internal int ScrollGap { get { return IsScrolling ? (int)Math.Round(Height * .5) : 0; } }
        internal int ScrollCycleWidth { get { return ContentWidth + ScrollGap; } }
        internal int ScrollOffset { get { return IsScrolling ? (int)scrollPosition % ScrollCycleWidth : 0; } }
        private void InitializeScrolling()
        {
            scrollClock = SpeedArrows.Seconds;
            scrollTimer.Tick += delegate
            {
                double now = SpeedArrows.Seconds, elapsed = Math.Min(.1, now - scrollClock); scrollClock = now;
                if (!Visible || !IsScrolling || ScrollPaused || !SpeedArrows.IsAnimated) return;
                AdvanceScroll(elapsed); RenderAlpha();
            };
            VisibleChanged += delegate { scrollClock = SpeedArrows.Seconds; scrollTimer.Enabled = Visible; };
            MouseWheel += delegate(object sender, MouseEventArgs e)
            {
                if (!IsScrolling) return;
                scrollPosition = Wrap(scrollPosition - Math.Sign(e.Delta) * Height * 2, ScrollCycleWidth);
                RenderAlpha();
            };
            Disposed += delegate { scrollTimer.Dispose(); };
        }
        private static double Wrap(double position, int width) { return (position % width + width) % width; }
        internal void AdvanceScroll(double seconds)
        {
            if (IsScrolling && !ScrollPaused) scrollPosition = Wrap(scrollPosition + Math.Max(0, seconds) * Height * .65, ScrollCycleWidth);
        }
        internal int ProviderAt(Point point)
        {
            if (!ClientRectangle.Contains(point)) return -1;
            int x = (point.X + ScrollOffset) % ScrollCycleWidth;
            if (x >= ContentWidth) return -1;
            var contentPoint = new Point(x, point.Y);
            return IndicatorView.ProviderAt(contentPoint, new Size(ContentWidth, Height), DisplayStyle, ShowWeekly, Usage);
        }
        internal Rectangle ProviderBounds(int provider, Point? pointer = null)
        {
            var bounds = IndicatorView.ProviderBounds(provider, new Size(ContentWidth, Height), DisplayStyle, ShowWeekly, Usage);
            bounds.Offset(-ScrollOffset, 0);
            var visible = Rectangle.Intersect(bounds, ClientRectangle);
            bounds.Offset(ScrollCycleWidth, 0);
            var repeated = Rectangle.Intersect(bounds, ClientRectangle);
            // While scrolling, a provider can be split across both edges. Prefer the piece under
            // the pointer so hover dismissal and hover opening agree (otherwise Details blinks).
            if (pointer.HasValue)
            {
                if (visible.Contains(pointer.Value)) return visible;
                if (repeated.Contains(pointer.Value)) return repeated;
            }
            return repeated.Width > visible.Width ? repeated : visible;
        }
        internal void DrawViewport(Graphics graphics)
        {
            if (!IsScrolling)
            { DrawInputSurface(graphics, ClientSize, DisplayStyle, Theme, ShowWeekly, PulseKey, Usage, ActivityKey, UsageBarColors); return; }
            graphics.Clear(Color.FromArgb(1, Theme.PreviewBackground));
            var state = graphics.Save();
            var palette = TaskbarTheme.AppPalette(Theme);
            float inset = Height / 34f;
            var shell = new RectangleF(inset, inset, Width - 2 * inset, Height - 2 * inset);
            using (var shape = UiSurface.Shape(shell, shell.Height / 2))
            using (var fill = new SolidBrush(palette.PreviewBackground))
            using (var border = new Pen(palette.HighContrast ? palette.Text : Color.FromArgb(68, 73, 80), .65f * inset))
            using (var content = new Bitmap(ContentWidth, Height, PixelFormat.Format32bppPArgb))
            {
                using (var canvas = Graphics.FromImage(content))
                    IndicatorView.Draw(canvas, content.Size, DisplayStyle, Theme, ShowWeekly, PulseKey, Usage, ActivityKey, -1, UsageBarColors, false);
                graphics.SetClip(shape); graphics.FillPath(fill, shape);
                graphics.DrawImageUnscaled(content, -ScrollOffset, 0);
                graphics.DrawImageUnscaled(content, ScrollCycleWidth - ScrollOffset, 0);
                float dividerX = ContentWidth + ScrollGap / 2f - ScrollOffset;
                using (var divider = new Pen(palette.Divider, .6f * inset)) graphics.DrawLine(divider, dividerX, Height * .24f, dividerX, Height * .76f);
                graphics.Restore(state); graphics.DrawPath(border, shape);
            }
        }
    }
}
