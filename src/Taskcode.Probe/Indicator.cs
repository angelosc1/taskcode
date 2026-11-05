using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal enum IndicatorStyle { Lines, Essential, Blocks }
    internal enum HostMode { Embedded, Overlay }

    internal sealed class IndicatorView : Control
    {
        internal IndicatorStyle Style = IndicatorStyle.Lines;
        internal TaskbarTheme Theme = TaskbarTheme.Read();
        internal static readonly Color Claude = Color.FromArgb(222, 157, 126), Codex = Color.FromArgb(144, 219, 188);
        internal IndicatorView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(36, 38, 41);
            ForeColor = Color.FromArgb(240, 242, 245);
            Cursor = Cursors.Hand;
            AccessibleName = "Taskcode demo: Claude 68 percent remaining, Codex 42 percent remaining";
            AccessibleRole = AccessibleRole.PushButton;
            TabStop = true;
        }
        private const float IndicatorScale = 1.17f;
        private static int ContentWidth(IndicatorStyle style) { return style == IndicatorStyle.Essential ? 156 : style == IndicatorStyle.Blocks ? 246 : 214; }
        internal static int LogicalWidth(IndicatorStyle style) { return (int)Math.Ceiling(ContentWidth(style) * IndicatorScale); }
        internal static int LogicalHeight { get { return (int)Math.Ceiling(34 * IndicatorScale); } }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Draw(e.Graphics, ClientSize, Style, Theme);
        }
        internal static void Draw(Graphics g, Size size, IndicatorStyle style, TaskbarTheme theme)
        {
            var state = g.Save();
            float scale = Math.Min(size.Width / (float)ContentWidth(style), size.Height / 34f);
            g.ScaleTransform(scale, scale);
            // Remove the former demo-label gutter while enlarging the entire content.
            g.TranslateTransform(-32, 0);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // Pixel units avoid applying the monitor's DPI twice when scaling.
            // Grayscale antialiasing works on an alpha surface without colored fringes.
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (var font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var bold = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var text = new SolidBrush(theme.Text))
            {
                int second = style == IndicatorStyle.Essential ? 120 : style == IndicatorStyle.Blocks ? 162 : 148;
                DrawProvider(g, font, bold, text, "Claude", 68, theme.Claude, 36, style, theme);
                DrawProvider(g, font, bold, text, "Codex", 42, theme.Codex, second, style, theme);
                using (var pen = new Pen(theme.Divider, 0.6f)) g.DrawLine(pen, second - 11, 9, second - 11, 25);
            }
            g.Restore(state);
        }
        private static void DrawProvider(Graphics g, Font font, Font bold, Brush text, string name, int remaining, Color color, int x, IndicatorStyle style, TaskbarTheme theme)
        {
            using (var accent = new SolidBrush(color))
            using (var track = new SolidBrush(theme.Track))
            {
                if (style == IndicatorStyle.Essential)
                {
                    DrawMark(g, font, accent, name, x);
                    g.DrawString(remaining + "%", font, text, x + 26, 10);
                    return;
                }
                if (style == IndicatorStyle.Lines)
                {
                    g.DrawString(name, font, text, x, 4);
                    g.DrawString(remaining + "%", font, text, x + 53, 4);
                    g.FillRectangle(track, x + 1, 24, 88, 3);
                    g.FillRectangle(accent, x + 1, 24, 88 * remaining / 100f, 3);
                    return;
                }
                DrawMark(g, font, accent, name, x);
                for (int i = 0; i < 10; i++) g.FillRectangle(i < (int)Math.Round(remaining / 10.0) ? accent : track, x + 25 + i * 5, 13, 4, 8);
                g.DrawString(remaining + "%", font, text, x + 78, 10);
            }
        }
        private static void DrawMark(Graphics g, Font font, Brush accent, string name, int x)
        {
            if (name == "Claude") BrandMarks.DrawClaude(g, accent, new RectangleF(x, 9, 16, 16));
            else using (var symbol = new Font("Segoe UI", 13f, FontStyle.Regular, GraphicsUnit.Pixel)) g.DrawString(">_", symbol, accent, x, 8);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }
    }

    internal sealed class IndicatorWindow : Form
    {
        private Size renderedSize;
        private IntPtr renderedHandle;
        private IndicatorStyle renderedStyle;
        private TaskbarTheme renderedTheme;
        internal IndicatorStyle DisplayStyle;
        internal TaskbarTheme Theme;
        internal IndicatorWindow()
        {
            Text = "Taskcode demo indicator";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            AccessibleName = "Taskcode demo: Claude 68 percent remaining, Codex 42 percent remaining";
            Cursor = Cursors.Hand;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { var value = base.CreateParams; value.ExStyle |= 0x80000 | 0x80; return value; }
        }
        internal bool RenderAlpha()
        {
            if (renderedHandle == Handle && renderedSize == Size && renderedStyle == DisplayStyle && renderedTheme == Theme) return true;
            if (Width <= 0 || Height <= 0) return false;
            using (var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb))
            {
                using (var graphics = Graphics.FromImage(bitmap)) IndicatorView.Draw(graphics, bitmap.Size, DisplayStyle, Theme);
                IntPtr screen = Native.GetDC(IntPtr.Zero), memory = Native.CreateCompatibleDC(screen);
                IntPtr image = bitmap.GetHbitmap(Color.FromArgb(0));
                IntPtr previous = Native.SelectObject(memory, image);
                try
                {
                    var size = new Native.WindowSize { Width = Width, Height = Height };
                    var source = new Native.Point();
                    var blend = new Native.Blend { SourceConstantAlpha = 255, AlphaFormat = 1 };
                    if (!Native.UpdateLayeredWindow(Handle, screen, IntPtr.Zero, ref size, memory, ref source, 0, ref blend, 2)) return false;
                    renderedHandle = Handle; renderedSize = Size; renderedStyle = DisplayStyle; renderedTheme = Theme;
                    return true;
                }
                finally
                {
                    Native.SelectObject(memory, previous);
                    Native.DeleteObject(image); Native.DeleteDC(memory); Native.ReleaseDC(IntPtr.Zero, screen);
                }
            }
        }
    }

    internal sealed class DetailWindow : Form
    {
        private TaskbarTheme theme = TaskbarTheme.Read();
        internal DetailWindow()
        {
            Text = "Taskcode - demo details";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(330, 290);
            BackColor = Color.FromArgb(29, 32, 36);
            ForeColor = Color.FromArgb(238, 241, 245);
            Font = new Font("Segoe UI", 10f);
            KeyPreview = true;
            AddText("Taskcode", 18, 14, 13, FontStyle.Bold, ForeColor);
            AddText("DEMO DATA  /  Remaining quota", 18, 49, 9, FontStyle.Regular, Color.LightGray);
            AddText("Claude", 42, 84, 10, FontStyle.Bold, IndicatorView.Claude);
            AddText("68%", 270, 84, 10, FontStyle.Bold, IndicatorView.Claude);
            AddText("5-hour window · resets in 2h 14m\nWeekly · 81% remaining", 18, 115, 9, FontStyle.Regular, ForeColor);
            AddText(">_", 18, 174, 10, FontStyle.Bold, IndicatorView.Codex);
            AddText("Codex", 42, 174, 10, FontStyle.Bold, IndicatorView.Codex);
            AddText("42%", 270, 174, 10, FontStyle.Bold, IndicatorView.Codex);
            AddText("5-hour window · resets in 1h 08m\nWeekly · 63% remaining", 18, 205, 9, FontStyle.Regular, ForeColor);
            AddText("Sample values only. No accounts connected.", 18, 262, 8, FontStyle.Regular, Color.LightGray);
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Hide(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
            ApplyTheme(theme);
        }
        private void AddText(string text, int x, int y, float size, FontStyle style, Color color)
        {
            string role = color == IndicatorView.Claude ? "claude" : color == IndicatorView.Codex ? "codex" : color == Color.LightGray ? "muted" : "text";
            Controls.Add(new Label { Text = text, Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI", size, style), ForeColor = color, Tag = role });
        }
        internal void ApplyTheme(TaskbarTheme value)
        {
            if (theme == value && BackColor == value.PreviewBackground) return;
            theme = value;
            BackColor = value.PreviewBackground;
            ForeColor = value.Text;
            foreach (Control control in Controls)
            {
                string role = control.Tag as string;
                control.ForeColor = role == "claude" ? value.Claude : role == "codex" ? value.Codex : role == "muted" ? value.Muted : value.Text;
            }
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(theme.Claude)) BrandMarks.DrawClaude(e.Graphics, brush, new RectangleF(18, 88, 16, 16));
        }
        protected override bool ShowWithoutActivation { get { return true; } }
    }
}
