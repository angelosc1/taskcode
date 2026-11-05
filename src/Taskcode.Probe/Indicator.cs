using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal enum IndicatorStyle { Lines, Essential, Blocks }
    internal enum HostMode { Embedded, Overlay }

    internal sealed class IndicatorView : Control
    {
        internal IndicatorStyle Style = IndicatorStyle.Lines;
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
        internal static int LogicalWidth(IndicatorStyle style) { return style == IndicatorStyle.Essential ? 188 : style == IndicatorStyle.Blocks ? 276 : 248; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            float scale = Math.Min(Width / (float)LogicalWidth(Style), Height / 34f);
            g.ScaleTransform(scale, scale);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (var font = new Font("Segoe UI", 9f))
            using (var bold = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var small = new Font("Segoe UI", 6.5f, FontStyle.Bold))
            using (var text = new SolidBrush(ForeColor))
            using (var muted = new SolidBrush(Color.FromArgb(167, 174, 183)))
            {
                g.DrawString("DEMO", small, muted, 2, 11);
                int second = Style == IndicatorStyle.Essential ? 112 : Style == IndicatorStyle.Blocks ? 158 : 144;
                DrawProvider(g, font, bold, text, "Claude", "C", 68, Claude, 36);
                DrawProvider(g, font, bold, text, "Codex", ">_", 42, Codex, second);
                using (var pen = new Pen(Color.FromArgb(66, 71, 77))) g.DrawLine(pen, second - 10, 8, second - 10, 26);
            }
        }
        private void DrawProvider(Graphics g, Font font, Font bold, Brush text, string name, string symbol, int remaining, Color color, int x)
        {
            using (var accent = new SolidBrush(color))
            using (var track = new SolidBrush(Color.FromArgb(73, 79, 86)))
            {
                if (Style == IndicatorStyle.Essential)
                {
                    g.DrawString(symbol, bold, accent, x, 8);
                    g.DrawString(remaining + "%", bold, text, x + 24, 8);
                    return;
                }
                if (Style == IndicatorStyle.Lines)
                {
                    g.DrawString(name, font, text, x, 1);
                    g.DrawString(remaining + "%", bold, text, x + 59, 1);
                    g.FillRectangle(track, x + 1, 25, 88, 3);
                    g.FillRectangle(accent, x + 1, 25, 88 * remaining / 100f, 3);
                    return;
                }
                g.DrawString(symbol, bold, accent, x, 8);
                for (int i = 0; i < 10; i++) g.FillRectangle(i < (int)Math.Round(remaining / 10.0) ? accent : track, x + 24 + i * 5, 13, 4, 9);
                g.DrawString(remaining + "%", font, text, x + 78, 8);
            }
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }
    }

    internal sealed class IndicatorWindow : Form
    {
        internal readonly IndicatorView View = new IndicatorView { Dock = DockStyle.Fill };
        internal IndicatorWindow()
        {
            Text = "Taskcode demo indicator";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            Controls.Add(View);
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { var value = base.CreateParams; value.ExStyle |= 0x80; return value; }
        }
    }

    internal sealed class DetailWindow : Form
    {
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
            AddText("Taskcode", 18, 14, 15, FontStyle.Bold, ForeColor);
            AddText("DEMO DATA  /  Remaining quota", 18, 49, 9, FontStyle.Regular, Color.LightGray);
            AddText("Claude                                      68%", 18, 84, 11, FontStyle.Bold, IndicatorView.Claude);
            AddText("5-hour window · resets in 2h 14m\nWeekly · 81% remaining", 18, 115, 9, FontStyle.Regular, ForeColor);
            AddText("Codex                                       42%", 18, 174, 11, FontStyle.Bold, IndicatorView.Codex);
            AddText("5-hour window · resets in 1h 08m\nWeekly · 63% remaining", 18, 205, 9, FontStyle.Regular, ForeColor);
            AddText("Sample values only. No accounts connected.", 18, 262, 8, FontStyle.Regular, Color.LightGray);
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Hide(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
        }
        private void AddText(string text, int x, int y, float size, FontStyle style, Color color)
        {
            Controls.Add(new Label { Text = text, Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI", size, style), ForeColor = color });
        }
        protected override bool ShowWithoutActivation { get { return true; } }
    }
}
