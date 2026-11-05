using System;
using System.Drawing;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal sealed partial class MainWindow
    {
        private readonly Panel[] settingsPages = new Panel[3];
        private readonly Button[] settingsNavigation = new Button[3];
        private readonly ToolTip settingsHints = new ToolTip();
        private int settingsPage;
        private sealed class MenuControls
        {
            internal ComboBox Style, Placement, Trigger;
            internal CheckBox Weekly, Startup;
            internal Button Details, Exit, Refresh, Claude, Codex, Disconnect;
        }
        private MenuControls BuildSettingsMenu(bool live)
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(520, 440);
            MinimumSize = Size;
            SizeGripStyle = SizeGripStyle.Hide;
            Font = new Font("Segoe UI", 9f);
            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Hide(); };
            var logo = new PictureBox { Location = new Point(18, 18), Size = new Size(26, 26), SizeMode = PictureBoxSizeMode.Zoom };
            using (var icon = TrayArtwork.Create()) logo.Image = icon.ToBitmap();
            logo.Disposed += delegate { logo.Image.Dispose(); }; Controls.Add(logo);
            var name = MenuLabel(this, "Taskcode", 54, 19, 190, 28, "heading"); name.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
            MouseEventHandler drag = delegate(object sender, MouseEventArgs e)
            { if (e.Button == MouseButtons.Left) { Native.ReleaseCapture(); Native.SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero); } };
            name.MouseDown += drag; logo.MouseDown += drag;
            MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Y < 64) drag(sender, e); };
            if (!live) MenuLabel(this, "Demo", 176, 25, 64, 18, "muted");
            var close = MenuButton(this, "×", 474, 16, 30, 30, "ghost"); close.Font = new Font("Segoe UI", 14f); close.AccessibleName = "Close settings"; close.Click += delegate { Hide(); };
            AddDivider(this, 16, 63, 488);
            string[] titles = { "Appearance", "Accounts", "General" };
            for (int index = 0; index < 3; index++)
            {
                int page = index;
                settingsNavigation[index] = MenuButton(this, titles[index], 12, 80 + index * 42, 122, 34, "navigation");
                settingsNavigation[index].TextAlign = ContentAlignment.MiddleLeft;
                settingsNavigation[index].Padding = new Padding(10, 0, 0, 0);
                settingsNavigation[index].Click += delegate { ShowSettingsPage(page); };
                settingsPages[index] = new Panel { Location = new Point(152, 80), Size = new Size(352, 312), AccessibleName = titles[index] + " settings" };
                Controls.Add(settingsPages[index]);
                var heading = MenuLabel(settingsPages[index], titles[index], 0, 0, 340, 24, "heading"); heading.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            }
            var appearance = settingsPages[0];
            var previewPanel = new Panel { Location = new Point(0, 40), Size = new Size(340, 64), Tag = "surface", AccessibleName = "Indicator preview" };
            appearance.Controls.Add(previewPanel);
            preview.Location = new Point(12, 12);
            preview.Size = new Size(IndicatorView.LogicalWidth(settings.Style, settings.ShowWeekly), IndicatorView.LogicalHeight);
            preview.Style = settings.Style; preview.ShowWeekly = settings.ShowWeekly; preview.Usage = usage; preview.Theme = theme;
            preview.AccessibleName = IndicatorView.LiveAccessibleText(usage, settings.ShowWeekly); previewPanel.Controls.Add(preview);
            var result = new MenuControls();
            result.Style = MenuSelect(appearance, "Style", 128, new[] { "Lines", "Essential", "Blocks" }, (int)settings.Style, "Indicator style");
            result.Trigger = MenuSelect(appearance, "Open details", 172, new[] { "On hover", "On click" }, (int)settings.DetailsTrigger, "Open details");
            result.Weekly = new CheckBox { Text = "Weekly quota", Location = new Point(0, 216), Size = new Size(330, 26), Checked = settings.ShowWeekly, AccessibleName = "Show weekly quota in the indicator" }; appearance.Controls.Add(result.Weekly);
            var animation = MenuButton(appearance, "Preview animation", 0, 264, 158, 30, "action"); animation.Click += delegate { PreviewAnimation(); };
            result.Details = MenuButton(appearance, "Details", 180, 264, 160, 30, "action");
            var accounts = settingsPages[1];
            for (int provider = 0; provider < 2; provider++)
            {
                int row = 44 + provider * 88;
                var badge = new ProviderMenuMark(provider) { Location = new Point(0, row + 4), Size = new Size(20, 20) }; accounts.Controls.Add(badge);
                var label = MenuLabel(accounts, provider == 0 ? "Claude" : "Codex", 30, row, 204, 23, provider == 0 ? "claude" : "codex"); label.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                accountLabels[provider] = MenuLabel(accounts, live ? "Connecting..." : "Offline preview", 30, row + 27, 206, 38, "muted"); accountLabels[provider].Font = new Font("Segoe UI", 8.5f);
                var connect = MenuButton(accounts, "Connect", 250, row + 2, 90, 30, "action"); connect.Enabled = live; connect.AccessibleName = provider == 0 ? "Connect Claude" : "Connect Codex";
                if (provider == 0) result.Claude = connect; else result.Codex = connect;
                if (provider == 0) AddDivider(accounts, 0, row + 74, 340);
            }
            result.Refresh = MenuButton(accounts, live ? "Refresh" : "Simulate speed", 0, 244, 108, 30, "action");
            result.Disconnect = MenuButton(accounts, "Disconnect", 124, 244, 108, 30, "action"); result.Disconnect.Enabled = live;
            var general = settingsPages[2];
            enabled.Text = "Show indicator"; enabled.Location = new Point(0, 44); enabled.Size = new Size(330, 26); enabled.Checked = true; general.Controls.Add(enabled);
            result.Startup = new CheckBox { Text = "Start with Windows", Location = new Point(0, 84), Size = new Size(330, 26) }; general.Controls.Add(result.Startup);
            AddDivider(general, 0, 132, 340);
            result.Placement = MenuSelect(general, "Placement", 156, new[] { "Embedded (experimental)", "Overlay" }, (int)settings.Mode, "Placement mode");
            settingsHints.SetToolTip(result.Placement, "Embedded attaches to the taskbar. Overlay uses the same position in a separate window.");
            AddDivider(this, 16, 400, 488);
            status.Location = new Point(18, 411); status.Size = new Size(365, 20); status.AutoEllipsis = true; status.Tag = "muted"; status.Font = new Font("Segoe UI", 8f); status.Text = "Starting..."; Controls.Add(status);
            result.Exit = MenuButton(this, "Exit", 414, 406, 90, 26, "ghost");
            Disposed += delegate { settingsHints.Dispose(); };
            ApplySettingsTheme(theme); ShowSettingsPage(0);
            return result;
        }
        private static Label MenuLabel(Control parent, string text, int x, int y, int width, int height, string role)
        { var label = new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height), Tag = role, AutoEllipsis = true }; parent.Controls.Add(label); return label; }
        private static Button MenuButton(Control parent, string text, int x, int y, int width, int height, string role)
        { var button = new Button { Text = text, Location = new Point(x, y), Size = new Size(width, height), Tag = role, FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false }; button.FlatAppearance.BorderSize = 0; parent.Controls.Add(button); return button; }
        private static void AddDivider(Control parent, int x, int y, int width) { parent.Controls.Add(new Panel { Location = new Point(x, y), Size = new Size(width, 1), Tag = "divider" }); }
        private static ComboBox MenuSelect(Control parent, string label, int y, string[] choices, int selected, string accessibleName)
        {
            MenuLabel(parent, label, 0, y + 4, 124, 24, "text");
            var select = new SettingsSelect { Location = new Point(132, y), Width = 208, AccessibleName = accessibleName };
            select.Items.AddRange(choices); select.SelectedIndex = selected; parent.Controls.Add(select); return select;
        }
        internal void ShowSettingsPage(int page)
        {
            settingsPage = page;
            for (int index = 0; index < settingsPages.Length; index++) settingsPages[index].Visible = index == page;
            ApplyNavigationTheme();
        }
        private static Color Mix(Color background, Color foreground, double amount)
        { return Color.FromArgb((int)(background.R * (1 - amount) + foreground.R * amount), (int)(background.G * (1 - amount) + foreground.G * amount), (int)(background.B * (1 - amount) + foreground.B * amount)); }
        internal void ApplySettingsTheme(TaskbarTheme palette)
        {
            theme = palette;
            BackColor = palette.PreviewBackground; ForeColor = palette.Text;
            ApplyMenuColors(this, palette);
            preview.Theme = palette; preview.BackColor = Mix(palette.PreviewBackground, palette.Text, 0.035);
            ApplyNavigationTheme();
            foreach (var window in accountWindows) if (window != null && !window.IsDisposed) window.ApplyTheme(palette);
            Invalidate();
        }
        private void ApplyNavigationTheme()
        {
            for (int index = 0; index < settingsNavigation.Length; index++)
            {
                var button = settingsNavigation[index]; if (button == null) continue;
                button.BackColor = index == settingsPage ? Mix(BackColor, ForeColor, 0.10) : BackColor;
                button.ForeColor = ForeColor;
                if (theme.HighContrast && index == settingsPage) { button.BackColor = SystemColors.Highlight; button.ForeColor = SystemColors.HighlightText; }
                button.AccessibleDescription = index == settingsPage ? "Selected" : "";
            }
        }
        internal static void ApplyMenuColors(Control root, TaskbarTheme palette)
        {
            foreach (Control control in root.Controls)
            {
                string role = control.Tag as string;
                control.BackColor = role == "divider" ? Mix(palette.PreviewBackground, palette.Text, .12) : role == "action" || role == "surface" || control is ComboBox ? Mix(palette.PreviewBackground, palette.Text, .035) : palette.PreviewBackground;
                control.ForeColor = role == "muted" ? palette.Muted : role == "claude" ? palette.Claude : role == "codex" ? palette.Codex : palette.Text;
                var button = control as Button;
                if (button != null) { button.FlatAppearance.MouseOverBackColor = Mix(palette.PreviewBackground, palette.Text, .10); button.FlatAppearance.MouseDownBackColor = Mix(palette.PreviewBackground, palette.Text, .15); }
                var mark = control as ProviderMenuMark; if (mark != null) mark.Palette = palette;
                ApplyMenuColors(control, palette);
            }
        }
        private void SetSettingsStatus(string message, bool problem = false)
        {
            status.Text = saveError ?? (message.StartsWith("Indicator paused", StringComparison.Ordinal) ? "Indicator paused" : problem ? message.Split('\n')[0] : "Indicator active");
            status.AccessibleDescription = message; settingsHints.SetToolTip(status, message);
        }
        private void UpdateAccountLabels()
        {
            for (int provider = 0; provider < 2; provider++)
            {
                string text = usage.AccountStatus[provider];
                accountLabels[provider].Text = text.StartsWith("Connected", StringComparison.Ordinal) ? string.IsNullOrWhiteSpace(usage.AccountIdentity[provider]) ? "Connected" : usage.AccountIdentity[provider] : text;
                settingsHints.SetToolTip(accountLabels[provider], (string.IsNullOrWhiteSpace(usage.AccountIdentity[provider]) ? "" : usage.AccountIdentity[provider] + "\n") + usage.ProviderStatus(provider, DateTime.UtcNow));
            }
        }
        private void ResizeIndicatorPreview()
        {
            float scale = Math.Max(96, Native.GetDpiForWindow(Handle)) / 96f;
            preview.Size = new Size((int)Math.Round(IndicatorView.LogicalWidth(settings.Style, settings.ShowWeekly) * scale), (int)Math.Round(IndicatorView.LogicalHeight * scale));
        }
        protected override void OnPaint(PaintEventArgs e)
        { base.OnPaint(e); using (var pen = new Pen(Mix(BackColor, ForeColor, .12))) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1); }
        private sealed class SettingsSelect : ComboBox
        {
            internal SettingsSelect() { DropDownStyle = ComboBoxStyle.DropDownList; FlatStyle = FlatStyle.Flat; DrawMode = DrawMode.OwnerDrawFixed; ItemHeight = 22; }
            protected override void OnDrawItem(DrawItemEventArgs e)
            {
                if (e.Index < 0) return;
                bool selected = (e.State & DrawItemState.Selected) != 0;
                using (var background = new SolidBrush(selected ? Mix(BackColor, ForeColor, .12) : BackColor)) e.Graphics.FillRectangle(background, e.Bounds);
                TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height), ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
            }
            protected override void WndProc(ref Message message)
            {
                base.WndProc(ref message);
                if (message.Msg == 0x000F) using (var graphics = Graphics.FromHwnd(Handle)) DrawFace(graphics);
                else if ((message.Msg == 0x0317 || message.Msg == 0x0318) && message.WParam != IntPtr.Zero)
                    using (var graphics = Graphics.FromHdc(message.WParam)) DrawFace(graphics);
            }
            private void DrawFace(Graphics graphics)
            {
                using (var background = new SolidBrush(BackColor)) graphics.FillRectangle(background, ClientRectangle);
                using (var border = new Pen(Mix(BackColor, ForeColor, .16))) graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
                var textBounds = new Rectangle(9, 0, Math.Max(0, Width - 37), Height);
                TextRenderer.DrawText(graphics, Text, Font, textBounds, ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var arrow = new Pen(ForeColor, 1.1f)) graphics.DrawLines(arrow, new[] { new PointF(Width - 20, Height / 2f - 2), new PointF(Width - 16, Height / 2f + 2), new PointF(Width - 12, Height / 2f - 2) });
                if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(graphics, new Rectangle(4, 4, Width - 30, Height - 8), ForeColor, BackColor);
            }
        }
        private sealed class ProviderMenuMark : Control
        {
            private readonly int provider;
            internal TaskbarTheme Palette = TaskbarTheme.Read();
            internal ProviderMenuMark(int provider) { this.provider = provider; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using (var brush = new SolidBrush(provider == 0 ? Palette.Claude : Palette.Codex))
                    if (provider == 0) BrandMarks.DrawClaude(e.Graphics, brush, new RectangleF(1, 1, 18, 18)); else BrandMarks.DrawCodex(e.Graphics, brush, new RectangleF(1, 1, 18, 18));
            }
        }
    }
}
