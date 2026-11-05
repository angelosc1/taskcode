using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal sealed class MainWindow : Form
    {
        private readonly Settings settings = Settings.Load();
        private readonly TaskbarWatcher watcher = new TaskbarWatcher();
        private readonly Timer timer = new Timer { Interval = 250 };
        private readonly IndicatorView preview = new IndicatorView();
        private readonly Label status = new Label();
        private readonly CheckBox enabled = new CheckBox();
        private IndicatorWindow indicator;
        private readonly DetailWindow details = new DetailWindow();
        private IntPtr attachedParent;
        private Rectangle indicatorBounds;
        private DateTime leftAt = DateTime.UtcNow;
        private bool pinned;
        private string saveError;
        private TaskbarTheme theme = TaskbarTheme.Read();
        private DateTime nextThemeCheck;
        private DateTime? hoverSince;

        internal MainWindow()
        {
            Text = "Taskcode - positioning prototype";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(590, 490);
            MinimumSize = Size;
            MaximumSize = Size;
            MaximizeBox = false;
            BackColor = Color.FromArgb(25, 28, 33);
            ForeColor = Color.FromArgb(237, 240, 244);
            Font = new Font("Segoe UI", 10f);
            AddLabel("Taskcode", 24, 20, 23, FontStyle.Bold, 540, 45);
            AddLabel("Taskbar positioning prototype", 26, 70, 11, FontStyle.Regular, 540, 26);
            var notice = AddLabel("DEMO DATA  /  No accounts connected", 26, 108, 9, FontStyle.Bold, 540, 26);
            notice.ForeColor = IndicatorView.Claude;
            AddLabel("Indicator style", 26, 154, 10, FontStyle.Regular, 180, 26);
            var style = new ComboBox { Location = new Point(220, 150), Width = 340, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Indicator style" };
            style.Items.AddRange(new object[] { "Lines (default)", "Essential", "Blocks" });
            style.SelectedIndex = (int)settings.Style;
            Controls.Add(style);
            AddLabel("Placement", 26, 197, 10, FontStyle.Regular, 180, 26);
            var mode = new ComboBox { Location = new Point(220, 193), Width = 340, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Placement mode" };
            mode.Items.AddRange(new object[] { "Embedded (experimental)", "Overlay" });
            mode.SelectedIndex = (int)settings.Mode;
            Controls.Add(mode);
            AddLabel("Preview", 26, 242, 10, FontStyle.Regular, 180, 26);
            preview.Location = new Point(220, 237);
            preview.Size = new Size(IndicatorView.LogicalWidth(settings.Style), IndicatorView.LogicalHeight);
            preview.Style = settings.Style;
            preview.Theme = theme;
            preview.BackColor = theme.PreviewBackground;
            Controls.Add(preview);
            enabled.Text = "Show indicator on the primary taskbar";
            enabled.Location = new Point(26, 298);
            enabled.Size = new Size(520, 28);
            enabled.Checked = true;
            Controls.Add(enabled);
            status.Location = new Point(26, 338);
            status.Size = new Size(534, 74);
            status.ForeColor = Color.FromArgb(184, 194, 207);
            Controls.Add(status);
            var detailsButton = new Button { Text = "Demo details", Location = new Point(26, 430), Size = new Size(145, 34) };
            var exit = new Button { Text = "Exit prototype", Location = new Point(415, 430), Size = new Size(145, 34) };
            Controls.Add(detailsButton);
            Controls.Add(exit);
            detailsButton.Click += delegate { ShowDetails(true, true); };
            preview.Click += delegate { ShowDetails(true, true); };
            exit.Click += delegate { Close(); };
            style.SelectedIndexChanged += delegate
            {
                settings.Style = (IndicatorStyle)style.SelectedIndex;
                preview.Style = settings.Style;
                preview.Width = IndicatorView.LogicalWidth(settings.Style);
                preview.Invalidate();
                Save();
                TickLayout();
            };
            mode.SelectedIndexChanged += delegate { settings.Mode = (HostMode)mode.SelectedIndex; RecreateIndicator(); Save(); TickLayout(); };
            enabled.CheckedChanged += delegate { TickLayout(); };
            timer.Tick += delegate { TickLayout(); };
            Shown += delegate { RecreateIndicator(); timer.Start(); };
            FormClosed += delegate
            {
                timer.Stop(); timer.Dispose(); watcher.Dispose();
                if (indicator != null && !indicator.IsDisposed) indicator.Dispose();
                details.Dispose();
            };
        }
        private Label AddLabel(string text, int x, int y, float size, FontStyle style, int width, int height)
        {
            var value = new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height), Font = new Font("Segoe UI", size, style) };
            Controls.Add(value);
            return value;
        }
        private void Save()
        {
            try { settings.Save(); saveError = null; }
            catch (IOException) { saveError = "Preference could not be saved."; }
            catch (UnauthorizedAccessException) { saveError = "Settings folder is not writable."; }
        }
        private void RecreateIndicator()
        {
            if (indicator != null && !indicator.IsDisposed) indicator.Dispose();
            attachedParent = IntPtr.Zero;
            indicator = new IndicatorWindow();
            indicator.Click += delegate { ShowDetails(true, false); };
            indicator.MouseHover += delegate { ShowDetails(false, false); };
        }
        private void PauseIndicator(string reason)
        {
            if (indicator != null && !indicator.IsDisposed) indicator.Hide();
            if (!pinned) details.Hide();
            indicatorBounds = Rectangle.Empty;
            hoverSince = null;
            status.Text = reason + (saveError == null ? "" : "\n" + saveError);
        }
        private void TickLayout()
        {
            if (DateTime.UtcNow >= nextThemeCheck)
            {
                nextThemeCheck = DateTime.UtcNow.AddSeconds(1);
                var current = TaskbarTheme.Read();
                if (current.Dark != theme.Dark || current.HighContrast != theme.HighContrast) theme = current;
                preview.Theme = theme;
                preview.BackColor = theme.PreviewBackground;
                preview.Invalidate();
                details.ApplyTheme(theme);
            }
            if (!enabled.Checked) { PauseIndicator("Indicator paused. The preview is still available."); return; }
            var layout = watcher.Latest;
            if (layout == null) { PauseIndicator("Measuring available taskbar space..."); return; }
            if (DateTime.UtcNow - layout.MeasuredAt > TimeSpan.FromSeconds(4)) { PauseIndicator("Waiting for a fresh taskbar measurement."); return; }
            if (layout.Error != null) { PauseIndicator(layout.Error); return; }
            if (!Native.IsWindow(layout.Taskbar)) { PauseIndicator("Waiting for Explorer to recreate the taskbar."); return; }
            Native.Rect liveBar, liveTray;
            var liveTrayHandle = Native.FindWindowEx(layout.Taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
            if (!Native.GetWindowRect(layout.Taskbar, out liveBar) || liveBar.Bounds != layout.Bar ||
                !Native.GetWindowRect(liveTrayHandle, out liveTray) || liveTray.Bounds != layout.Tray)
            { PauseIndicator("Taskbar layout changed; measuring again."); return; }
            var screen = Screen.FromRectangle(layout.Bar);
            if (Rectangle.Intersect(screen.Bounds, layout.Bar).Height < layout.Bar.Height / 2)
            { PauseIndicator("Taskbar is hidden."); return; }
            float scale = Math.Max(96, layout.Dpi) / 96f;
            int width = (int)Math.Round(IndicatorView.LogicalWidth(settings.Style) * scale);
            int height = (int)Math.Round(IndicatorView.LogicalHeight * scale);
            Rectangle slot;
            if (!Placement.TrySlot(layout.Bar, layout.Tray, layout.OccupiedRight, width, height, (int)Math.Round(8 * scale), out slot))
            { PauseIndicator("Not enough verified space before the system tray.\nTry Essential or reduce the number of taskbar buttons."); return; }
            if (indicator == null || indicator.IsDisposed || (attachedParent != IntPtr.Zero && !Native.IsWindow(indicator.Handle))) RecreateIndicator();
            indicator.DisplayStyle = settings.Style;
            indicator.Theme = theme;
            IntPtr expected = settings.Mode == HostMode.Embedded ? layout.Taskbar : IntPtr.Zero;
            if (attachedParent != expected || Native.GetParent(indicator.Handle) != expected)
            {
                indicator.Hide();
                long nativeStyle = Native.GetStyle(indicator.Handle, Native.GwlStyle).ToInt64();
                Native.SetStyle(indicator.Handle, Native.GwlStyle, new IntPtr(expected == IntPtr.Zero ? (nativeStyle & ~Native.Child) | Native.Popup : (nativeStyle & ~Native.Popup) | Native.Child));
                Native.SetParent(indicator.Handle, expected);
                if (Native.GetParent(indicator.Handle) != expected)
                { PauseIndicator("Embedding failed. Select Overlay to test the fallback."); return; }
                attachedParent = expected;
            }
            var point = new Native.Point { X = slot.Left, Y = slot.Top };
            if (expected != IntPtr.Zero && !Native.ScreenToClient(expected, ref point)) { PauseIndicator("Could not map taskbar coordinates."); return; }
            if (!Native.SetWindowPos(indicator.Handle, expected == IntPtr.Zero ? Native.TopMost : Native.Top, point.X, point.Y, slot.Width, slot.Height, Native.NoActivate | Native.FrameChanged))
            { PauseIndicator("Windows rejected indicator placement."); return; }
            indicator.Show();
            if (!indicator.RenderAlpha())
            { PauseIndicator("Windows rejected the transparent surface. Try the other placement mode."); return; }
            indicatorBounds = slot;
            status.Text = (expected == IntPtr.Zero ? "Overlay active" : "Embedded child window active") + " · " + layout.Dpi + " DPI · " + (theme.Dark ? "Dark" : "Light") + " taskbar\nTransparent indicator. Demo data only." + (saveError == null ? "" : "\n" + saveError);
            // The alpha surface has real transparent gaps between glyphs. Track the
            // complete indicator rectangle so those gaps do not reset hover dwell.
            if (indicatorBounds.Contains(Cursor.Position))
            {
                leftAt = DateTime.UtcNow;
                if (!hoverSince.HasValue) hoverSince = DateTime.UtcNow;
                if (!details.Visible && DateTime.UtcNow - hoverSince.Value > TimeSpan.FromMilliseconds(400)) ShowDetails(false, false);
            }
            else
            {
                hoverSince = null;
                if (details.Visible && details.Bounds.Contains(Cursor.Position)) leftAt = DateTime.UtcNow;
                else if (!pinned && DateTime.UtcNow - leftAt > TimeSpan.FromMilliseconds(550)) details.Hide();
            }
        }
        private void ShowDetails(bool focus, bool fromPreview)
        {
            Rectangle anchor = fromPreview || indicatorBounds.IsEmpty ? RectangleToScreen(preview.Bounds) : indicatorBounds;
            var area = Screen.FromRectangle(anchor).WorkingArea;
            int x = Math.Max(area.Left, Math.Min(anchor.Right - details.Width, area.Right - details.Width));
            int y = anchor.Top - details.Height - 8;
            if (y < area.Top) y = Math.Min(area.Bottom - details.Height, anchor.Bottom + 8);
            details.Location = new Point(x, y);
            pinned = focus;
            leftAt = DateTime.UtcNow;
            details.Show();
            if (focus) details.Activate();
        }
    }
}
