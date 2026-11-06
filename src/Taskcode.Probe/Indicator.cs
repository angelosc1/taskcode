using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal enum IndicatorStyle { Essential, Lines, Blocks, Donuts, Sparkline, DotMatrix, ColorStrip }
    internal enum HostMode { Embedded, Overlay }

    internal sealed partial class IndicatorView : Control
    {
        internal IndicatorStyle Style = IndicatorStyle.Essential;
        internal bool ShowWeekly;
        internal bool UsageBarColors;
        internal int PulseKey;
        internal int ActivityKey;
        internal UsageDemo Usage;
        internal TaskbarTheme Theme = TaskbarTheme.Read();
        internal static readonly Color Claude = Color.FromArgb(222, 157, 126), Codex = Color.FromArgb(144, 219, 188);
        internal IndicatorView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(36, 38, 41);
            ForeColor = Color.FromArgb(240, 242, 245);
            Cursor = Cursors.Hand;
            AccessibleName = "Taskcode demo: Claude 32 percent used, Codex 58 percent used";
            AccessibleRole = AccessibleRole.PushButton;
            TabStop = true;
        }
        private const float IndicatorScale = 1.17f;
        private const int PillLeftPadding = 8;
        private static bool Compact(IndicatorStyle style) { return style == IndicatorStyle.Essential; }
        private static int ProviderGapReduction(IndicatorStyle style, bool weekly) { return Compact(style) ? (weekly ? 4 : 8) : 0; }
        private static int OptionalCellWidth(IndicatorStyle style) { return Compact(style) ? 90 : 136; }
        private static bool Filtered(UsageDemo usage) { return usage != null && (!usage.CoreEnabled[0] || !usage.CoreEnabled[1]); }
        private static int FilteredCellWidth(IndicatorStyle style, bool weekly) { return weekly ? (Compact(style) ? 100 : 140) : OptionalCellWidth(style); }
        private static System.Collections.Generic.List<int> VisibleProviders(UsageDemo usage)
        {
            var result = new System.Collections.Generic.List<int>();
            for (int provider = 0; provider < 2 + usage.OptionalProviders.Length; provider++) if (usage.ProviderEnabled(provider)) result.Add(provider);
            return result;
        }
        private static int OptionalCount(UsageDemo usage)
        { int count = 0; if (usage != null) foreach (var provider in usage.OptionalProviders) if (provider.Enabled) count++; return count; }
        private static int ContentWidth(IndicatorStyle style, bool showWeekly, UsageDemo usage = null)
        {
            if (style == IndicatorStyle.DotMatrix || style == IndicatorStyle.ColorStrip) { int count = DonutProviders(usage).Count; return count == 0 ? 0 : count * CompactMeterCellWidth(style, showWeekly) + 16; }
            if (style == IndicatorStyle.Sparkline) { int count = DonutProviders(usage).Count; return count == 0 ? 0 : count * SparklineCellWidth + 16; }
            if (style == IndicatorStyle.Donuts) { int count = DonutProviders(usage).Count; return count == 0 ? 0 : count * DonutCellWidth(showWeekly) + 16; }
            if (Filtered(usage)) return usage.VisibleProviderCount == 0 ? 0 : usage.VisibleProviderCount * FilteredCellWidth(style, showWeekly) + 16;
            int width = showWeekly ? (Compact(style) ? 184 : 268) : Compact(style) ? 156 : style == IndicatorStyle.Blocks ? 246 : 262;
            return width + PillLeftPadding - ProviderGapReduction(style, showWeekly) + OptionalCount(usage) * OptionalCellWidth(style);
        }
        internal static int LogicalWidth(IndicatorStyle style, bool showWeekly = false, UsageDemo usage = null) { return (int)Math.Ceiling(ContentWidth(style, showWeekly, usage) * IndicatorScale); }
        internal static string AccessibleText(bool weekly)
        {
            return weekly ? "Taskcode demo: Claude 5 hours 32 percent used, weekly 19 percent used; Codex 5 hours 58 percent used, weekly 37 percent used" :
                "Taskcode demo: Claude 32 percent used, Codex 58 percent used";
        }
        internal static int LogicalHeight { get { return (int)Math.Ceiling(34 * IndicatorScale); } }
        private static int SecondProviderX(IndicatorStyle style, bool weekly)
        { return (weekly ? (Compact(style) ? 132 : 172) : Compact(style) ? 120 : style == IndicatorStyle.Blocks ? 162 : 172) - ProviderGapReduction(style, weekly); }
        internal static Rectangle ProviderBounds(int provider, Size size, IndicatorStyle style, bool weekly, UsageDemo usage = null)
        {
            if (style == IndicatorStyle.DotMatrix || style == IndicatorStyle.ColorStrip) return CompactMeterProviderBounds(provider, size, style, weekly, usage);
            if (style == IndicatorStyle.Sparkline) return SparklineProviderBounds(provider, size, usage);
            if (style == IndicatorStyle.Donuts) return DonutProviderBounds(provider, size, weekly, usage);
            if (Filtered(usage))
            {
                var visible = VisibleProviders(usage); int slot = visible.IndexOf(provider);
                if (slot < 0 || size.Width <= 0 || size.Height <= 0) return Rectangle.Empty;
                float factor = Math.Min(size.Width / (float)ContentWidth(style, weekly, usage), size.Height / 34f);
                int left = slot == 0 ? 0 : (int)Math.Round((8 + slot * FilteredCellWidth(style, weekly)) * factor);
                int right = slot == visible.Count - 1 ? size.Width : (int)Math.Round((8 + (slot + 1) * FilteredCellWidth(style, weekly)) * factor);
                return new Rectangle(left, 0, right - left, size.Height);
            }
            if (OptionalCount(usage) > 0)
            {
                float dynamicScale = Math.Min(size.Width / (float)ContentWidth(style, weekly, usage), size.Height / 34f);
                int baseWidth = (int)Math.Round(ContentWidth(style, weekly) * dynamicScale);
                if (provider < 2) return ProviderBounds(provider, new Size(baseWidth, size.Height), style, weekly);
                int slot = 0;
                foreach (var optional in usage.OptionalProviders)
                {
                    if (!optional.Enabled) continue;
                    if (provider == optional.Index + 2)
                    {
                        int left = (int)Math.Round((ContentWidth(style, weekly) + slot * OptionalCellWidth(style)) * dynamicScale);
                        int right = (int)Math.Round((ContentWidth(style, weekly) + (slot + 1) * OptionalCellWidth(style)) * dynamicScale);
                        return new Rectangle(left, 0, right - left, size.Height);
                    }
                    slot++;
                }
                return Rectangle.Empty;
            }
            if (provider < 0 || provider > 1 || size.Width <= 0 || size.Height <= 0) return Rectangle.Empty;
            float scale = Math.Min(size.Width / (float)ContentWidth(style, weekly), size.Height / 34f);
            int split = (int)Math.Round((SecondProviderX(style, weekly) - (weekly ? 12 : 11) - 32 + PillLeftPadding) * scale);
            return provider == 0 ? new Rectangle(0, 0, split, size.Height) : new Rectangle(split, 0, size.Width - split, size.Height);
        }
        internal static int ProviderAt(Point point, Size size, IndicatorStyle style, bool weekly, UsageDemo usage = null)
        {
            if (!new Rectangle(Point.Empty, size).Contains(point)) return -1;
            if (style == IndicatorStyle.DotMatrix || style == IndicatorStyle.ColorStrip)
            { foreach (int provider in DonutProviders(usage)) if (CompactMeterProviderBounds(provider, size, style, weekly, usage).Contains(point)) return provider; return -1; }
            if (style == IndicatorStyle.Sparkline)
            { foreach (int provider in DonutProviders(usage)) if (SparklineProviderBounds(provider, size, usage).Contains(point)) return provider; return -1; }
            if (style == IndicatorStyle.Donuts)
            { foreach (int provider in DonutProviders(usage)) if (DonutProviderBounds(provider, size, weekly, usage).Contains(point)) return provider; return -1; }
            if (Filtered(usage))
            { foreach (int provider in VisibleProviders(usage)) if (ProviderBounds(provider, size, style, weekly, usage).Contains(point)) return provider; return -1; }
            if (OptionalCount(usage) > 0)
            {
                for (int provider = 0; provider < 2 + ProviderCatalog.Count; provider++) if (ProviderBounds(provider, size, style, weekly, usage).Contains(point)) return provider;
                return -1;
            }
            return ProviderBounds(0, size, style, weekly).Contains(point) ? 0 : 1;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Draw(e.Graphics, ClientSize, Style, Theme, ShowWeekly, PulseKey, Usage, ActivityKey, -1, UsageBarColors);
        }
        internal static void Draw(Graphics g, Size size, IndicatorStyle style, TaskbarTheme theme, bool showWeekly = false, int pulseKey = 0, UsageDemo usage = null, int activityKey = 0, double activitySeconds = -1, bool usageBarColors = false)
        {
            if (ContentWidth(style, showWeekly, usage) <= 0 || size.Width <= 0 || size.Height <= 0) return;
            if (style == IndicatorStyle.DotMatrix || style == IndicatorStyle.ColorStrip) { DrawCompactMeters(g, size, style, theme, showWeekly, usage, activityKey, activitySeconds, usageBarColors); return; }
            if (style == IndicatorStyle.Sparkline) { DrawSparklines(g, size, theme, showWeekly, usage, activityKey, activitySeconds, usageBarColors); return; }
            if (style == IndicatorStyle.Donuts) { DrawDonuts(g, size, theme, showWeekly, usage, activityKey, activitySeconds, usageBarColors); return; }
            var state = g.Save();
            float scale = Math.Min(size.Width / (float)ContentWidth(style, showWeekly, usage), size.Height / 34f);
            g.ScaleTransform(scale, scale);
            // Remove the former demo-label gutter while enlarging the entire content.
            g.TranslateTransform(-32, 0);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            {
                theme = TaskbarTheme.AppPalette(theme);
                float left = 33, top = 1, width = ContentWidth(style, showWeekly, usage) - 2, height = 32;
                using (var shape = new GraphicsPath())
                using (var background = new SolidBrush(theme.PreviewBackground))
                using (var edge = new Pen(theme.HighContrast ? theme.Text : Color.FromArgb(68, 73, 80), 0.65f))
                {
                    shape.AddArc(left, top, height, height, 90, 180);
                    shape.AddArc(left + width - height, top, height, height, 270, 180);
                    shape.CloseFigure();
                    g.FillPath(background, shape);
                    g.DrawPath(edge, shape);
                }
                g.TranslateTransform(PillLeftPadding, 0);
            }
            // Pixel units avoid applying the monitor's DPI twice when scaling.
            // Grayscale antialiasing works on an alpha surface without colored fringes.
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (var font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var bold = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var text = new SolidBrush(theme.Text))
            {
                if (Filtered(usage))
                {
                    int slot = 0;
                    foreach (int provider in VisibleProviders(usage))
                    {
                        int x = 36 + slot++ * FilteredCellWidth(style, showWeekly);
                        if (slot > 1) using (var pen = new Pen(theme.Divider, .6f)) g.DrawLine(pen, x - 10, 8, x - 10, 26);
                        if (provider < 2)
                        {
                            string name = provider == 0 ? "Claude" : "Codex"; Color accent = provider == 0 ? theme.Claude : theme.Codex;
                            if (showWeekly) DrawWeeklyProvider(g, font, bold, text, name, Value(usage, provider * 2), Value(usage, provider * 2 + 1), accent, x, style, theme, usageBarColors);
                            else DrawProvider(g, font, bold, text, name, Value(usage, provider * 2), accent, x, style, theme, usageBarColors);
                            if ((activityKey & (1 << provider)) != 0) ActivityRipple.Draw(g, accent, MarkCenter(x), activitySeconds);
                        }
                        else
                        {
                            var optional = usage.OptionalProviders[provider - 2];
                            DrawProvider(g, font, bold, text, optional.Name, optional.DisplayUsed(DateTime.UtcNow), optional.Color, x, style, theme, usageBarColors, optional.DisplayText(DateTime.UtcNow));
                        }
                    }
                    g.Restore(state); return;
                }
                if (showWeekly)
                {
                    int second = SecondProviderX(style, true);
                    DrawWeeklyProvider(g, font, bold, text, "Claude", Value(usage, 0), Value(usage, 1), theme.Claude, 36, style, theme, usageBarColors);
                    DrawWeeklyProvider(g, font, bold, text, "Codex", Value(usage, 2), Value(usage, 3), theme.Codex, second, style, theme, usageBarColors);
                    using (var pen = new Pen(theme.Divider, 0.6f)) g.DrawLine(pen, second - 12, 6, second - 12, 28);
                }
                else
                {
                    int second = SecondProviderX(style, false);
                    DrawProvider(g, font, bold, text, "Claude", Value(usage, 0), theme.Claude, 36, style, theme, usageBarColors);
                    DrawProvider(g, font, bold, text, "Codex", Value(usage, 2), theme.Codex, second, style, theme, usageBarColors);
                    using (var pen = new Pen(theme.Divider, 0.6f)) g.DrawLine(pen, second - 11, 9, second - 11, 25);
                }
                if ((activityKey & 1) != 0) ActivityRipple.Draw(g, theme.Claude, MarkCenter(36), activitySeconds);
                if ((activityKey & 2) != 0) ActivityRipple.Draw(g, theme.Codex, MarkCenter(SecondProviderX(style, showWeekly)), activitySeconds);
                if (usage != null)
                {
                    int slot = 0;
                    foreach (var provider in usage.OptionalProviders)
                    {
                        if (!provider.Enabled) continue;
                        int x = ContentWidth(style, showWeekly) + 32 + slot++ * OptionalCellWidth(style);
                        using (var pen = new Pen(theme.Divider, .6f)) g.DrawLine(pen, x - 10, 9, x - 10, 25);
                        DrawProvider(g, font, bold, text, provider.Name, provider.DisplayUsed(DateTime.UtcNow), provider.Color, x, style, theme, usageBarColors, provider.DisplayText(DateTime.UtcNow));
                    }
                }
            }
            g.Restore(state);
        }
        private static int Value(UsageDemo usage, int index) { return usage == null ? new[] { 32, 19, 58, 37 }[index] : usage.DisplayUsed(index, DateTime.UtcNow); }
        private static string Percentage(int value) { return value < 0 ? "--" : value + "%"; }
        internal static string LiveAccessibleText(UsageDemo usage, bool weekly)
        {
            if (usage == null || !usage.IsLive) return AccessibleText(weekly);
            if (Filtered(usage))
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (int provider in VisibleProviders(usage))
                    if (provider < 2) parts.Add((provider == 0 ? "Claude " : "Codex ") + Percentage(Value(usage, provider * 2)) + (weekly ? ", weekly " + Percentage(Value(usage, provider * 2 + 1)) : ""));
                    else { var optional = usage.OptionalProviders[provider - 2]; parts.Add(optional.Name + " " + optional.DisplayText(DateTime.UtcNow)); }
                return parts.Count == 0 ? "Taskcode: No providers enabled." : "Taskcode: " + string.Join("; ", parts) + " used. -- means unavailable.";
            }
            string optionalText = "";
            foreach (var provider in usage.OptionalProviders) if (provider.Enabled) optionalText += "; " + provider.Name + " " + provider.DisplayText(DateTime.UtcNow);
            return "Taskcode: Claude " + Percentage(Value(usage, 0)) + (weekly ? ", weekly " + Percentage(Value(usage, 1)) : "") + "; Codex " + Percentage(Value(usage, 2)) + (weekly ? ", weekly " + Percentage(Value(usage, 3)) : "") + optionalText + " used. -- means unavailable.";
        }
        private static void DrawWeeklyProvider(Graphics g, Font font, Font bold, Brush text, string name, int shortUsed, int weeklyUsed, Color color, int x, IndicatorStyle style, TaskbarTheme theme, bool usageBarColors)
        {
            using (var accent = new SolidBrush(color))
            using (var track = new SolidBrush(theme.Track))
            using (var muted = new SolidBrush(theme.Muted))
            using (var periodFont = new Font("Segoe UI", 8f, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                DrawMark(g, font, accent, name, x);
                for (int row = 0; row < 2; row++)
                {
                    int y = 3 + row * 14;
                    int used = row == 0 ? shortUsed : weeklyUsed;
                    g.DrawString(row == 0 ? "5h" : "7d", periodFont, muted, x + 23, y + 2);
                    if (style == IndicatorStyle.Essential)
                    { g.DrawString(Percentage(used), bold, text, x + 43, y); continue; }
                    if (style == IndicatorStyle.Lines)
                    {
                        g.FillRectangle(track, x + 40, y + 6, 44, 3);
                        if (used >= 0) using (var meter = new SolidBrush(UsageBarPalette.Resolve(color, used, usageBarColors, theme.HighContrast))) g.FillRectangle(meter, x + 40, y + 6, 44 * used / 100f, 3);
                    }
                    else
                    {
                        using (var meter = new SolidBrush(UsageBarPalette.Resolve(color, used, usageBarColors, theme.HighContrast)))
                        for (int block = 0; block < 10; block++)
                            g.FillRectangle(block < (int)Math.Round(used / 10.0) ? meter : track, x + 40 + block * 4.4f, y + 5, 3.4f, 5);
                    }
                    g.DrawString(Percentage(used), bold, text, x + 91, y);
                }
            }
        }
        private static void DrawProvider(Graphics g, Font font, Font bold, Brush text, string name, int used, Color color, int x, IndicatorStyle style, TaskbarTheme theme, bool usageBarColors, string valueText = null)
        {
            string percentage = valueText ?? Percentage(used);
            int optionalIndex = Array.IndexOf(ProviderCatalog.Names, name);
            string displayName = optionalIndex < 0 ? name : ProviderCatalog.ShortNames[optionalIndex];
            using (var accent = new SolidBrush(color))
            using (var meter = new SolidBrush(UsageBarPalette.Resolve(color, used, usageBarColors, theme.HighContrast)))
            using (var track = new SolidBrush(theme.Track))
            {
                if (style == IndicatorStyle.Essential)
                {
                    DrawMark(g, font, accent, name, x);
                    g.DrawString(percentage, bold, text, x + 26, 10);
                    return;
                }
                if (style == IndicatorStyle.Lines)
                {
                    DrawMark(g, font, accent, name, x);
                    g.DrawString(displayName, font, text, x + 23, 4);
                    if (optionalIndex < 0) g.DrawString(percentage, bold, text, x + 76, 4);
                    else using (var alignment = new StringFormat { Alignment = StringAlignment.Far }) g.DrawString(percentage, bold, text, new RectangleF(x + 77, 4, 38, 16), alignment);
                    g.FillRectangle(track, x + 23, 24, 89, 3);
                    if (used >= 0) g.FillRectangle(meter, x + 23, 24, 89 * used / 100f, 3);
                    return;
                }
                DrawMark(g, font, accent, name, x);
                for (int i = 0; i < 10; i++) g.FillRectangle(i < (int)Math.Round(used / 10.0) ? meter : track, x + 25 + i * 5, 13, 4, 8);
                g.DrawString(percentage, bold, text, x + 78, 10);
            }
        }
        private static void DrawMark(Graphics g, Font font, Brush accent, string name, int x)
        {
            var bounds = MarkBounds(x);
            if (name == "Claude") BrandMarks.DrawClaude(g, accent, bounds);
            else if (name == "Codex") BrandMarks.DrawCodex(g, accent, bounds);
            else
            {
                int index = Array.IndexOf(ProviderCatalog.Names, name);
                if (index >= 0) BrandMarks.DrawProvider(g, accent, index, bounds);
                else g.DrawString("?", font, accent, bounds.X, bounds.Y + 1);
            }
        }
        internal static RectangleF MarkBounds(int x) { return new RectangleF(x, 9, 16, 16); }
        internal static PointF MarkCenter(int x) { var bounds = MarkBounds(x); return new PointF(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2); }
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
        private bool renderedWeekly, renderedUsageBarColors;
        private int renderedPulse;
        private int renderedActivity;
        private int renderedFrame;
        private bool renderedMotion;
        private string renderedValues;
        internal IndicatorStyle DisplayStyle;
        internal bool ShowWeekly;
        internal bool UsageBarColors;
        internal int PulseKey;
        internal int ActivityKey;
        internal UsageDemo Usage;
        internal TaskbarTheme Theme;
        internal event EventHandler ContextRequested;
        internal IndicatorWindow()
        {
            Text = "Taskcode indicator";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            AccessibleName = "Taskcode demo: Claude 32 percent used, Codex 58 percent used";
            Cursor = Cursors.Hand;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0021) // WM_MOUSEACTIVATE: accept input without activating Explorer's child.
            { message.Result = new IntPtr(3); return; } // MA_NOACTIVATE
            if (message.Msg == 0x007B) // WM_CONTEXTMENU must never fall through to the taskbar parent.
            {
                var handler = ContextRequested;
                if (handler != null) handler(this, EventArgs.Empty);
                message.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref message);
        }
        internal void InvalidateSurface() { renderedHandle = IntPtr.Zero; }
        protected override void OnVisibleChanged(EventArgs e)
        {
            if (Visible) InvalidateSurface();
            base.OnVisibleChanged(e);
        }
        internal bool Place(Rectangle bounds, IntPtr parent, bool frameChanged)
        {
            Native.Rect current;
            if (!frameChanged && Visible && Native.GetWindowRect(Handle, out current) && current.Bounds == bounds) return true;
            var point = new Native.Point { X = bounds.Left, Y = bounds.Top };
            if (parent != IntPtr.Zero && !Native.ScreenToClient(parent, ref point)) return false;
            return Native.SetWindowPos(Handle, parent == IntPtr.Zero ? Native.TopMost : Native.Top,
                point.X, point.Y, bounds.Width, bounds.Height, Native.NoActivate | (frameChanged ? Native.FrameChanged : 0));
        }
        internal static void DrawInputSurface(Graphics graphics, Size size, IndicatorStyle style, TaskbarTheme theme, bool weekly, int pulse, UsageDemo usage, int activity, bool usageBarColors = false)
        {
            // Windows skips alpha-zero pixels during mouse hit testing. Alpha 1/255
            // is visually transparent, but keeps gaps inside this window's input area.
            graphics.Clear(Color.FromArgb(1, theme.PreviewBackground));
            IndicatorView.Draw(graphics, size, style, theme, weekly, pulse, usage, activity, -1, usageBarColors);
        }
        protected override CreateParams CreateParams
        {
            get { var value = base.CreateParams; value.ExStyle |= 0x80000 | 0x80; return value; }
        }
        internal bool RenderAlpha()
        {
            bool motion = ActivityKey != 0 && SpeedArrows.IsAnimated;
            int frame = motion ? SpeedArrows.Frame : 0;
            string values = IndicatorView.LiveAccessibleText(Usage, ShowWeekly);
            if ((DisplayStyle == IndicatorStyle.Donuts || DisplayStyle == IndicatorStyle.DotMatrix || DisplayStyle == IndicatorStyle.ColorStrip) && Usage != null) values += IndicatorView.DonutDataKey(Usage, DateTime.UtcNow);
            if (DisplayStyle == IndicatorStyle.Sparkline && Usage != null) values += IndicatorView.SparklineDataKey(Usage, DateTime.UtcNow);
            if (renderedHandle == Handle && renderedSize == Size && renderedStyle == DisplayStyle && renderedTheme == Theme && renderedWeekly == ShowWeekly && renderedUsageBarColors == UsageBarColors && renderedPulse == PulseKey && renderedActivity == ActivityKey && renderedFrame == frame && renderedMotion == motion && renderedValues == values) return true;
            if (Width <= 0 || Height <= 0) return false;
            using (var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb))
            {
                using (var graphics = Graphics.FromImage(bitmap)) DrawInputSurface(graphics, bitmap.Size, DisplayStyle, Theme, ShowWeekly, PulseKey, Usage, ActivityKey, UsageBarColors);
                IntPtr screen = Native.GetDC(IntPtr.Zero), memory = Native.CreateCompatibleDC(screen);
                IntPtr image = bitmap.GetHbitmap(Color.FromArgb(0));
                IntPtr previous = Native.SelectObject(memory, image);
                try
                {
                    var size = new Native.WindowSize { Width = Width, Height = Height };
                    var source = new Native.Point();
                    var blend = new Native.Blend { SourceConstantAlpha = 255, AlphaFormat = 1 };
                    if (!Native.UpdateLayeredWindow(Handle, screen, IntPtr.Zero, ref size, memory, ref source, 0, ref blend, 2)) return false;
                    renderedHandle = Handle; renderedSize = Size; renderedStyle = DisplayStyle; renderedTheme = Theme; renderedWeekly = ShowWeekly; renderedUsageBarColors = UsageBarColors; renderedPulse = PulseKey; renderedActivity = ActivityKey; renderedFrame = frame; renderedMotion = motion; renderedValues = values;
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
        internal Label ValueCaption;
        internal Label BudgetCaption;
        internal bool UsageBarColors;
        internal DateTime RenderTimeUtc = DateTime.UtcNow;
        internal QuotaBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            AccessibleRole = AccessibleRole.ProgressBar;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Reading != null && (!Reading.Available || (!Period.WaitingForFirstRequest && Period.EndUtc <= RenderTimeUtc) || (Reading.ObservedUtc != DateTime.MinValue && RenderTimeUtc - Reading.ObservedUtc > TimeSpan.FromMinutes(Reading.Provider == "Claude" ? 6 : 2)))) return;
            using (var track = new SolidBrush(Theme.Track))
            using (var fill = new SolidBrush(UsageBarPalette.Resolve(Provider == "claude" ? Theme.Claude : Theme.Codex, Period.UsedFraction * 100, UsageBarColors, Theme.HighContrast)))
            using (var marker = new SolidBrush(Theme.Text))
            {
                // Used quota and elapsed time both advance from left to right.
                // The independent time marker still advances left to right.
                const float inset = 6;
                float width = Math.Max(0, Width - 2 * inset);
                float used = (float)Period.UsedFraction * width;
                using (var shape = UiSurface.Shape(new RectangleF(inset, 9, width, 6), 3))
                {
                    e.Graphics.FillPath(track, shape);
                    var state = e.Graphics.Save(); e.Graphics.SetClip(shape);
                    e.Graphics.FillRectangle(fill, inset, 9, used, 6);
                    e.Graphics.Restore(state);
                }
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
        private readonly Timer dismissalTimer = new Timer { Interval = 16 };
        private readonly ToolTip hints = new ToolTip();
        private readonly UsageDemo usage;
        private readonly Label providerLabel;
        private readonly DetailInteraction interaction = new DetailInteraction();
        private readonly Button closeDetails;
        internal bool Pinned { get { return interaction.Pinned; } }
        internal bool RequiresIndicator;
        internal void SetInteraction(bool pinned)
        { interaction.Reset(pinned, (Native.GetAsyncKeyState(1) & 0x8000) != 0); closeDetails.Visible = pinned; }
        internal Rectangle HoverAnchor { get; set; }
        private bool usageBarColors;
        internal bool UsageBarColors
        {
            get { return usageBarColors; }
            set
            {
                usageBarColors = value;
                foreach (Control control in Controls)
                {
                    var bar = control as QuotaBar;
                    if (bar != null) { bar.UsageBarColors = value; bar.Invalidate(); }
                }
            }
        }
        internal readonly int ProviderIndex;
        internal DetailWindow(UsageDemo usage = null, int providerIndex = 0)
        {
            if (providerIndex < 0 || providerIndex > 1) throw new ArgumentOutOfRangeException("providerIndex");
            ProviderIndex = providerIndex;
            this.usage = usage ?? new UsageDemo();
            string provider = providerIndex == 0 ? "Claude" : "Codex";
            Text = "Taskcode - " + provider + " details";
            FormBorderStyle = FormBorderStyle.None;
            ControlBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(300, 276);
            BackColor = Color.FromArgb(29, 32, 36);
            ForeColor = Color.FromArgb(238, 241, 245);
            Font = new Font("Segoe UI", 10f);
            KeyPreview = true;
            var providerColor = providerIndex == 0 ? IndicatorView.Claude : IndicatorView.Codex;
            providerLabel = AddText(provider, 44, 11, 10, FontStyle.Bold, providerColor);
            providerLabel.AutoSize = false; providerLabel.Size = new Size(230, 26); providerLabel.TextAlign = ContentAlignment.MiddleLeft;
            closeDetails = new Button { Text = "×", AccessibleName = "Close details", Location = new Point(275, 10), Size = new Size(19, 23), FlatStyle = FlatStyle.Flat, TabStop = false, Visible = false, Tag = "muted", Font = new Font("Segoe UI", 10f) };
            closeDetails.FlatAppearance.BorderSize = 0; closeDetails.Click += delegate { Hide(); }; Controls.Add(closeDetails);
            AddQuota(this.usage.Readings[providerIndex * 2], 50);
            AddQuota(this.usage.Readings[providerIndex * 2 + 1], 162);
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Hide(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
            periodTimer.Tick += delegate { RefreshPeriods(); };
            animationTimer.Tick += delegate
            {
                foreach (Control control in Controls)
                { var readout = control as SpeedReadout; if (readout != null && readout.HasArrows) readout.Invalidate(); }
            };
            dismissalTimer.Tick += delegate { TrackPointer(Cursor.Position, DateTime.UtcNow); };
            VisibleChanged += delegate
            {
                periodTimer.Enabled = dismissalTimer.Enabled = Visible;
                if (Visible) { RefreshPeriods(); } else animationTimer.Stop();
            };
            Disposed += delegate { periodTimer.Dispose(); animationTimer.Dispose(); dismissalTimer.Dispose(); hints.Dispose(); };
            ApplyTheme(theme);
            L10n.Bind(this);
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
            int used = reading.Period.Remaining < 0 ? -1 : 100 - reading.Period.Remaining;
            var color = provider == "Claude" ? IndicatorView.Claude : IndicatorView.Codex;
            AddText(window == "Weekly" ? "Weekly" : "5 hours", 20, y, 9, FontStyle.Bold, ForeColor);
            var valueCaption = AddText(used < 0 ? "--" : used + "%", 212, y - 2, 11, FontStyle.Bold, color);
            int valueWidth = Math.Max(72, TextRenderer.MeasureText("100%", valueCaption.Font).Width + 6);
            valueCaption.AutoSize = false; valueCaption.Location = new Point(284 - valueWidth, y - 2);
            valueCaption.Size = new Size(valueWidth, 24); valueCaption.TextAlign = ContentAlignment.TopRight;
            var budgetCaption = AddText("", 16, y + 42, 8, FontStyle.Regular, Color.LightGray);
            var caption = AddText("", 16, y + 58, 8, FontStyle.Regular, Color.LightGray);
            var speedCaption = new SpeedReadout { Location = new Point(16, y + 74), Size = new Size(268, 20), ForeColor = color, Tag = provider.ToLowerInvariant() };
            Controls.Add(speedCaption);
            Controls.Add(new QuotaBar
            {
                Location = new Point(10, y + 24), Size = new Size(280, 17),
                Period = reading.Period, Reading = reading, SpeedCaption = speedCaption, ValueCaption = valueCaption,
                Provider = provider.ToLowerInvariant(), Theme = theme, Caption = caption, BudgetCaption = budgetCaption,
                AccessibleName = provider + ", " + window + ", " + used + " percent used"
            });
        }
        internal void RefreshUsage() { RefreshPeriods(); }
        internal void TrackPointer(Point pointer, DateTime now)
        {
            if (!Visible) return;
            if (interaction.ShouldDismiss(Bounds, HoverAnchor, pointer, (Native.GetAsyncKeyState(1) & 0x8000) != 0)) Hide();
        }
        private void RefreshPeriods()
        {
            var now = DateTime.UtcNow;
            usage.Tick(now);
            AccessibleDescription = usage.ProviderStatus(ProviderIndex, now);
            L10n.Hint(hints, providerLabel, AccessibleDescription);
            bool hasArrows = false;
            foreach (Control control in Controls)
            {
                var bar = control as QuotaBar;
                if (bar == null) continue;
                bar.RenderTimeUtc = now;
                int index = ProviderIndex * 2 + (bar.Reading.Window == "Weekly" ? 1 : 0);
                bool resetPending = usage.ResetPending(index, now);
                int value = usage.DisplayUsed(index, now);
                bar.ValueCaption.Text = value < 0 ? "--" : value + "%";
                bar.AccessibleName = bar.Reading.Provider + ", " + bar.Reading.Window + ", " + (value < 0 ? "quota unavailable" : value + " percent used");
                bar.Caption.Text = resetPending ? "Window reset" : value < 0 ? "Unavailable" : bar.Period.ResetCaptionAt(now);
                var speed = usage.DetailSpeedAt(ProviderIndex * 2 + (bar.Reading.Window == "Weekly" ? 1 : 0), now);
                bar.SpeedCaption.Estimate = speed;
                bar.SpeedCaption.Visible = true;
                L10n.Hint(hints, bar.ValueCaption, "Used quota. " + AccessibleDescription);
                bar.SpeedCaption.AccessibleName = (speed.State == SpeedState.Active ? speed.Level + " speed arrows. " : "") + speed.Summary;
                bar.SpeedCaption.Invalidate();
                hasArrows |= bar.SpeedCaption.HasArrows;
                L10n.Hint(hints, bar.SpeedCaption, speed.State == SpeedState.Active ? (speed.PeriodAverage ? "Average since reset, not recent speed. " : "Recent measured speed. ") + speed.PointsPerHour.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " percentage points/hour · " + speed.WindowSeconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "-second observation window. Estimate, not a guarantee." : speed.Summary);
                var elapsed = bar.Period.ElapsedAt(now);
                bar.BudgetCaption.Text = resetPending ? "Awaiting provider confirmation" : value < 0 ? "-- used / -- time" : value + "% used / " +
                    (elapsed.HasValue ? Math.Round(elapsed.Value * 100) + "% time" : "-- time");
                string description = Math.Round(bar.Period.UsedFraction * 100) + "% quota used; " +
                    (elapsed.HasValue ? Math.Round(elapsed.Value * 100) + "% time elapsed. " : "") + bar.Period.PaceAt(now) + ".";
                bar.AccessibleDescription = description;
                L10n.Hint(hints, bar, description);
                bar.Invalidate();
            }
            animationTimer.Enabled = Visible && hasArrows && SpeedArrows.IsAnimated;
        }
        internal void ApplyTheme(TaskbarTheme value)
        {
            value = TaskbarTheme.AppPalette(value);
            if (theme == value && BackColor == value.PreviewBackground) return;
            theme = value;
            BackColor = value.PreviewBackground;
            ForeColor = value.Text;
            foreach (Control control in Controls)
            {
                var bar = control as QuotaBar;
                if (bar != null) { bar.Theme = value; bar.BackColor = UiSurface.Card(value); bar.Invalidate(); continue; }
                string role = control.Tag as string;
                control.BackColor = control == providerLabel ? value.PreviewBackground : UiSurface.Card(value);
                control.ForeColor = role == "claude" ? value.Claude : role == "codex" ? value.Codex : role == "muted" ? value.Muted : value.Text;
            }
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var state = e.Graphics.Save(); e.Graphics.ScaleTransform(Width / 300f, Height / 276f);
            foreach (int y in new[] { 46, 158 })
                UiSurface.Draw(e.Graphics, new RectangleF(10, y, 280, 102), UiSurface.Card(theme), UiSurface.Blend(theme.PreviewBackground, theme.Text, .08), 10);
            var accent = ProviderIndex == 0 ? theme.Claude : theme.Codex;
            UiSurface.Draw(e.Graphics, new RectangleF(11, 10, 26, 26), UiSurface.Blend(theme.PreviewBackground, accent, .10), UiSurface.Blend(theme.PreviewBackground, accent, .18), 8);
            e.Graphics.Restore(state);
            if (ProviderIndex == 0)
                using (var brush = new SolidBrush(theme.Claude)) BrandMarks.DrawClaude(e.Graphics, brush, new RectangleF(16, 16, 16, 16));
            else using (var brush = new SolidBrush(theme.Codex)) BrandMarks.DrawCodex(e.Graphics, brush, new RectangleF(16, 16, 16, 16));
            using (var border = new Pen(theme.Divider, 1)) e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        }
        protected override CreateParams CreateParams
        {
            get
            {
                var value = base.CreateParams;
                value.ExStyle |= 0x08000000 | 0x80 | 0x08; // NOACTIVATE | TOOLWINDOW | TOPMOST, without the activating Form.TopMost setter.
                return value;
            }
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0021) // WM_MOUSEACTIVATE: read-only card never takes focus.
            { message.Result = new IntPtr(3); return; } // MA_NOACTIVATE, keep mouse input.
            base.WndProc(ref message);
        }
        protected override bool ShowWithoutActivation { get { return true; } }
    }
}
