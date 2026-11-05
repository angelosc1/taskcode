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
        internal bool ShowWeekly;
        internal int PulseKey;
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
        private static int ContentWidth(IndicatorStyle style, bool showWeekly)
        {
            if (showWeekly) return style == IndicatorStyle.Essential ? 184 : 268;
            return style == IndicatorStyle.Essential ? 156 : style == IndicatorStyle.Blocks ? 246 : 214;
        }
        internal static int LogicalWidth(IndicatorStyle style, bool showWeekly = false) { return (int)Math.Ceiling(ContentWidth(style, showWeekly) * IndicatorScale); }
        internal static string AccessibleText(bool weekly)
        {
            return weekly ? "Taskcode demo: Claude 5 hours 68 percent, weekly 81 percent remaining; Codex 5 hours 42 percent, weekly 63 percent remaining" :
                "Taskcode demo: Claude 68 percent remaining, Codex 42 percent remaining";
        }
        internal static int LogicalHeight { get { return (int)Math.Ceiling(34 * IndicatorScale); } }
        private static int SecondProviderX(IndicatorStyle style, bool weekly)
        { return weekly ? (style == IndicatorStyle.Essential ? 132 : 172) : style == IndicatorStyle.Essential ? 120 : style == IndicatorStyle.Blocks ? 162 : 148; }
        internal static Rectangle ProviderBounds(int provider, Size size, IndicatorStyle style, bool weekly)
        {
            if (provider < 0 || provider > 1 || size.Width <= 0 || size.Height <= 0) return Rectangle.Empty;
            float scale = Math.Min(size.Width / (float)ContentWidth(style, weekly), size.Height / 34f);
            int split = (int)Math.Round((SecondProviderX(style, weekly) - (weekly ? 12 : 11) - 32) * scale);
            return provider == 0 ? new Rectangle(0, 0, split, size.Height) : new Rectangle(split, 0, size.Width - split, size.Height);
        }
        internal static int ProviderAt(Point point, Size size, IndicatorStyle style, bool weekly)
        {
            if (!new Rectangle(Point.Empty, size).Contains(point)) return -1;
            return ProviderBounds(0, size, style, weekly).Contains(point) ? 0 : 1;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Draw(e.Graphics, ClientSize, Style, Theme, ShowWeekly, PulseKey);
        }
        internal static void Draw(Graphics g, Size size, IndicatorStyle style, TaskbarTheme theme, bool showWeekly = false, int pulseKey = 0)
        {
            var state = g.Save();
            float scale = Math.Min(size.Width / (float)ContentWidth(style, showWeekly), size.Height / 34f);
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
                if (showWeekly)
                {
                    int second = SecondProviderX(style, true);
                    DrawWeeklyProvider(g, font, text, "Claude", 68, 81, theme.Claude, 36, style, theme);
                    DrawWeeklyProvider(g, font, text, "Codex", 42, 63, theme.Codex, second, style, theme);
                    using (var pen = new Pen(theme.Divider, 0.6f)) g.DrawLine(pen, second - 12, 3, second - 12, 31);
                    DrawSpeedHint(g, theme.Claude, 36, style, true, pulseKey & 3, (pulseKey >> 2) & 3);
                    DrawSpeedHint(g, theme.Codex, second, style, true, (pulseKey >> 4) & 3, (pulseKey >> 6) & 3);
                }
                else
                {
                    int second = SecondProviderX(style, false);
                    DrawProvider(g, font, bold, text, "Claude", 68, theme.Claude, 36, style, theme);
                    DrawProvider(g, font, bold, text, "Codex", 42, theme.Codex, second, style, theme);
                    using (var pen = new Pen(theme.Divider, 0.6f)) g.DrawLine(pen, second - 11, 9, second - 11, 25);
                    DrawSpeedHint(g, theme.Claude, 36, style, false, pulseKey & 3, 0);
                    DrawSpeedHint(g, theme.Codex, second, style, false, (pulseKey >> 4) & 3, 0);
                }
            }
            g.Restore(state);
        }
        private static void DrawSpeedHint(Graphics g, Color color, int x, IndicatorStyle style, bool weekly, int shortLevel, int weeklyLevel)
        {
            if (!weekly && style == IndicatorStyle.Lines) DrawFilledArrows(g, color, shortLevel, x + 70, 18);
            else
            {
                DrawFilledArrows(g, color, shortLevel, x, 1);
                if (weekly) DrawFilledArrows(g, color, weeklyLevel, x, 28);
            }
        }
        private static void DrawFilledArrows(Graphics g, Color color, int count, float x, float y)
        {
            SpeedArrows.Draw(g, color, count, new RectangleF(x - 0.5f, y, 17, 6));
        }
        private static void DrawWeeklyProvider(Graphics g, Font font, Brush text, string name, int shortRemaining, int weeklyRemaining, Color color, int x, IndicatorStyle style, TaskbarTheme theme)
        {
            using (var accent = new SolidBrush(color))
            using (var track = new SolidBrush(theme.Track))
            using (var muted = new SolidBrush(theme.Muted))
            using (var periodFont = new Font("Segoe UI", 8f, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                DrawMark(g, font, accent, name, x);
                for (int row = 0; row < 2; row++)
                {
                    int y = row * 18;
                    int remaining = row == 0 ? shortRemaining : weeklyRemaining;
                    g.DrawString(row == 0 ? "5h" : "7d", periodFont, muted, x + 23, y + 2);
                    if (style == IndicatorStyle.Essential)
                    { g.DrawString(remaining + "%", font, text, x + 43, y); continue; }
                    if (style == IndicatorStyle.Lines)
                    {
                        g.FillRectangle(track, x + 40, y + 6, 44, 3);
                        g.FillRectangle(accent, x + 40, y + 6, 44 * remaining / 100f, 3);
                    }
                    else
                    {
                        for (int block = 0; block < 10; block++)
                            g.FillRectangle(block < (int)Math.Round(remaining / 10.0) ? accent : track, x + 40 + block * 4.4f, y + 5, 3.4f, 5);
                    }
                    g.DrawString(remaining + "%", font, text, x + 91, y);
                }
            }
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
        private bool renderedWeekly;
        private int renderedPulse;
        private int renderedFrame;
        private bool renderedMotion;
        internal IndicatorStyle DisplayStyle;
        internal bool ShowWeekly;
        internal int PulseKey;
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
            bool motion = PulseKey != 0 && SpeedArrows.IsAnimated;
            int frame = motion ? SpeedArrows.Frame : 0;
            if (renderedHandle == Handle && renderedSize == Size && renderedStyle == DisplayStyle && renderedTheme == Theme && renderedWeekly == ShowWeekly && renderedPulse == PulseKey && renderedFrame == frame && renderedMotion == motion) return true;
            if (Width <= 0 || Height <= 0) return false;
            using (var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb))
            {
                using (var graphics = Graphics.FromImage(bitmap)) IndicatorView.Draw(graphics, bitmap.Size, DisplayStyle, Theme, ShowWeekly, PulseKey);
                IntPtr screen = Native.GetDC(IntPtr.Zero), memory = Native.CreateCompatibleDC(screen);
                IntPtr image = bitmap.GetHbitmap(Color.FromArgb(0));
                IntPtr previous = Native.SelectObject(memory, image);
                try
                {
                    var size = new Native.WindowSize { Width = Width, Height = Height };
                    var source = new Native.Point();
                    var blend = new Native.Blend { SourceConstantAlpha = 255, AlphaFormat = 1 };
                    if (!Native.UpdateLayeredWindow(Handle, screen, IntPtr.Zero, ref size, memory, ref source, 0, ref blend, 2)) return false;
                    renderedHandle = Handle; renderedSize = Size; renderedStyle = DisplayStyle; renderedTheme = Theme; renderedWeekly = ShowWeekly; renderedPulse = PulseKey; renderedFrame = frame; renderedMotion = motion;
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

    internal sealed class QuotaBar : Control
    {
        internal QuotaPeriod Period;
        internal string Provider;
        internal TaskbarTheme Theme;
        internal Label Caption;
        internal SpeedReadout SpeedCaption;
        internal DemoReading Reading;
        internal DateTime RenderTimeUtc = DateTime.UtcNow;
        internal QuotaBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            AccessibleRole = AccessibleRole.ProgressBar;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var track = new SolidBrush(Theme.Track))
            using (var fill = new SolidBrush(Provider == "claude" ? Theme.Claude : Theme.Codex))
            using (var marker = new SolidBrush(Theme.Text))
            {
                // Both the used-quota boundary and the time marker advance left to right.
                // Remaining quota stays colored to the right of the consumed portion.
                const float inset = 6;
                float width = Math.Max(0, Width - 2 * inset);
                float used = (float)Period.UsedFraction * width;
                e.Graphics.FillRectangle(track, inset, 9, width, 6);
                e.Graphics.FillRectangle(fill, inset + used, 9, width - used, 6);
                var elapsed = Period.ElapsedAt(RenderTimeUtc);
                if (elapsed.HasValue)
                {
                    float x = inset + width * (float)elapsed.Value;
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.FillPolygon(marker, new[] { new PointF(x - 3.5f, 0), new PointF(x + 3.5f, 0), new PointF(x, 7) });
                }
            }
        }
    }

    internal sealed class DetailWindow : Form
    {
        private TaskbarTheme theme = TaskbarTheme.Read();
        private readonly Timer periodTimer = new Timer { Interval = 1000 };
        private readonly Timer animationTimer = new Timer { Interval = 33 };
        private readonly ToolTip hints = new ToolTip();
        private readonly UsageDemo usage;
        internal readonly int ProviderIndex;
        internal DetailWindow(UsageDemo usage = null, int providerIndex = 0)
        {
            if (providerIndex < 0 || providerIndex > 1) throw new ArgumentOutOfRangeException("providerIndex");
            ProviderIndex = providerIndex;
            this.usage = usage ?? new UsageDemo();
            string provider = providerIndex == 0 ? "Claude" : "Codex";
            Text = "Taskcode - " + provider + " details";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(330, 324);
            BackColor = Color.FromArgb(29, 32, 36);
            ForeColor = Color.FromArgb(238, 241, 245);
            Font = new Font("Segoe UI", 10f);
            KeyPreview = true;
            AddText("Taskcode", 18, 14, 13, FontStyle.Bold, ForeColor);
            AddText("DEMO DATA  /  Remaining quota", 18, 49, 9, FontStyle.Regular, Color.LightGray);
            var providerColor = providerIndex == 0 ? IndicatorView.Claude : IndicatorView.Codex;
            AddText(provider, 42, 80, 10, FontStyle.Bold, providerColor);
            if (providerIndex == 1) AddText(">_", 18, 80, 10, FontStyle.Bold, providerColor);
            AddQuota(this.usage.Readings[providerIndex * 2], 110);
            AddQuota(this.usage.Readings[providerIndex * 2 + 1], 196);
            AddText("Gray: used · Color: remaining · ▼: time", 18, 286, 8, FontStyle.Regular, Color.LightGray);
            AddText("Simulated usage history. No accounts connected.", 18, 305, 7.5f, FontStyle.Regular, Color.LightGray);
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Hide(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
            periodTimer.Tick += delegate { RefreshPeriods(); };
            animationTimer.Tick += delegate
            {
                foreach (Control control in Controls)
                { var readout = control as SpeedReadout; if (readout != null && readout.HasArrows) readout.Invalidate(); }
            };
            VisibleChanged += delegate { periodTimer.Enabled = Visible; if (Visible) RefreshPeriods(); else animationTimer.Stop(); };
            Disposed += delegate { periodTimer.Dispose(); animationTimer.Dispose(); hints.Dispose(); };
            ApplyTheme(theme);
        }
        private Label AddText(string text, int x, int y, float size, FontStyle style, Color color)
        {
            string role = color == IndicatorView.Claude ? "claude" : color == IndicatorView.Codex ? "codex" : color == Color.LightGray ? "muted" : "text";
            var label = new Label { Text = text, Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI", size, style), ForeColor = color, Tag = role };
            Controls.Add(label);
            return label;
        }
        private void AddQuota(DemoReading reading, int y)
        {
            string provider = reading.Provider, window = reading.Window;
            int remaining = reading.Period.Remaining;
            var color = provider == "Claude" ? IndicatorView.Claude : IndicatorView.Codex;
            AddText(window, 18, y, 9, FontStyle.Regular, ForeColor);
            AddText(remaining + "%", 270, y, 9, FontStyle.Bold, color);
            var caption = AddText("", 18, y + 41, 8, FontStyle.Regular, Color.LightGray);
            var speedCaption = new SpeedReadout { Location = new Point(18, y + 57), Size = new Size(294, 24), ForeColor = color, Tag = provider.ToLowerInvariant() };
            Controls.Add(speedCaption);
            Controls.Add(new QuotaBar
            {
                Location = new Point(12, y + 20), Size = new Size(306, 17),
                Period = reading.Period, Reading = reading, SpeedCaption = speedCaption,
                Provider = provider.ToLowerInvariant(), Theme = theme, Caption = caption,
                AccessibleName = provider + ", " + window + ", " + remaining + " percent remaining"
            });
        }
        internal void RefreshUsage() { RefreshPeriods(); }
        private void RefreshPeriods()
        {
            var now = DateTime.UtcNow;
            usage.Tick(now);
            bool hasArrows = false;
            foreach (Control control in Controls)
            {
                var bar = control as QuotaBar;
                if (bar == null) continue;
                bar.RenderTimeUtc = now;
                bar.Caption.Text = bar.Period.CaptionAt(now);
                var speed = bar.Reading.Speed.EstimateAt(now);
                bar.SpeedCaption.Estimate = speed;
                bar.SpeedCaption.AccessibleName = (speed.State == SpeedState.Active ? speed.Level + " speed arrows. " : "") + speed.Summary;
                bar.SpeedCaption.Invalidate();
                hasArrows |= bar.SpeedCaption.HasArrows;
                hints.SetToolTip(bar.SpeedCaption, speed.State == SpeedState.Active ? speed.PointsPerHour.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " percentage points/hour · 15-second recent window. Estimate, not a guarantee." : speed.Summary);
                var elapsed = bar.Period.ElapsedAt(now);
                string description = Math.Round(bar.Period.UsedFraction * 100) + "% quota used; " +
                    (elapsed.HasValue ? Math.Round(elapsed.Value * 100) + "% time elapsed. " : "") + bar.Period.PaceAt(now) + ".";
                bar.AccessibleDescription = description;
                hints.SetToolTip(bar, description);
                bar.Invalidate();
            }
            animationTimer.Enabled = Visible && hasArrows && SpeedArrows.IsAnimated;
        }
        internal void ApplyTheme(TaskbarTheme value)
        {
            if (theme == value && BackColor == value.PreviewBackground) return;
            theme = value;
            BackColor = value.PreviewBackground;
            ForeColor = value.Text;
            foreach (Control control in Controls)
            {
                var bar = control as QuotaBar;
                if (bar != null) { bar.Theme = value; bar.BackColor = value.PreviewBackground; bar.Invalidate(); continue; }
                string role = control.Tag as string;
                control.BackColor = value.PreviewBackground;
                control.ForeColor = role == "claude" ? value.Claude : role == "codex" ? value.Codex : role == "muted" ? value.Muted : value.Text;
            }
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (ProviderIndex == 0)
                using (var brush = new SolidBrush(theme.Claude)) BrandMarks.DrawClaude(e.Graphics, brush, new RectangleF(18, 84, 16, 16));
        }
        protected override bool ShowWithoutActivation { get { return true; } }
    }
}
