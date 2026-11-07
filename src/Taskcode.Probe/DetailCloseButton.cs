using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal sealed class DetailCloseButton : Button
    {
        private bool hovered;
        internal DetailCloseButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            AccessibleName = "Close details"; TabStop = false;
        }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; base.OnMouseLeave(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            using (var background = new SolidBrush(hovered ? UiSurface.Blend(BackColor, ForeColor, .10) : BackColor))
                e.Graphics.FillRectangle(background, ClientRectangle);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = Math.Min(Width, Height) / 24f, radius = 4 * scale;
            float x = Width / 2f, y = Height / 2f;
            using (var pen = new Pen(ForeColor, 1.25f * scale))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                e.Graphics.DrawLine(pen, x - radius, y - radius, x + radius, y + radius);
                e.Graphics.DrawLine(pen, x + radius, y - radius, x - radius, y + radius);
            }
        }
    }
}
