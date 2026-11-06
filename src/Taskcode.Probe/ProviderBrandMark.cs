using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal sealed class ProviderBrandMark : Control
    {
        private int provider;
        internal ProviderBrandMark()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            AccessibleRole = AccessibleRole.Graphic;
        }
        internal void SetProvider(int index, Color color, string name)
        {
            if (provider == index && ForeColor == color && AccessibleName == name) return;
            provider = index; ForeColor = color; AccessibleName = name; Invalidate();
        }
        internal static void Draw(Graphics graphics, Brush brush, int index, RectangleF bounds)
        {
            if (index == 0) BrandMarks.DrawClaude(graphics, brush, bounds);
            else if (index == 1) BrandMarks.DrawCodex(graphics, brush, bounds);
            else BrandMarks.DrawProvider(graphics, brush, index - 2, bounds);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float side = System.Math.Min(ClientSize.Width, ClientSize.Height);
            using (var brush = new SolidBrush(ForeColor))
                Draw(e.Graphics, brush, provider, new RectangleF((Width - side) / 2, (Height - side) / 2, side, side));
        }
    }
}
