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
            internal CheckBox Weekly, UsageColors, Startup, AutomaticUpdates;
            internal Button Details, Close, Refresh, Claude, Codex, Disconnect;
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
            var close = MenuButton(this, "", 474, 16, 30, 30, "ghost"); close.AccessibleName = "Close settings"; close.Click += delegate { Hide(); };
            AddDivider(this, 16, 63, 488);
            string[] titles = { "Appearance", "Accounts", "General" };
            for (int index = 0; index < 3; index++)
            {
                int page = index;
                settingsNavigation[index] = MenuButton(this, titles[index], 12, 80 + index * 42, 122, 34, "navigation");
                settingsNavigation[index].TextAlign = ContentAlignment.MiddleLeft;
                settingsNavigation[index].Padding = new Padding(18, 0, 0, 0);
                settingsNavigation[index].Click += delegate { ShowSettingsPage(page); };
                settingsPages[index] = new Panel { Location = new Point(152, 80), Size = new Size(352, 312), AccessibleName = titles[index] + " settings" };
                Controls.Add(settingsPages[index]);
                settingsPages[index].Paint += delegate(object sender, PaintEventArgs e) { PaintSettingsCards((Panel)sender, e.Graphics); };
                var heading = MenuLabel(settingsPages[index], titles[index], 0, 0, 340, 24, "heading"); heading.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            }
            var appearance = settingsPages[0];
            var previewPanel = new Panel { Location = new Point(0, 40), Size = new Size(340, 64), Tag = "surface", AccessibleName = "Indicator preview" };
            appearance.Controls.Add(previewPanel);
            previewPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var brush = new SolidBrush(theme.PreviewBackground)) e.Graphics.FillRectangle(brush, previewPanel.ClientRectangle);
                UiSurface.Draw(e.Graphics, new RectangleF(.5f, .5f, previewPanel.Width - 1, previewPanel.Height - 1), UiSurface.Card(theme), Mix(BackColor, ForeColor, .07), 10);
            };
            previewPanel.SizeChanged += delegate { UiSurface.Round(previewPanel, 10); }; UiSurface.Round(previewPanel, 10);
            preview.Location = new Point(12, 12);
            preview.Size = new Size(IndicatorView.LogicalWidth(settings.Style, settings.ShowWeekly), IndicatorView.LogicalHeight);
            preview.Style = settings.Style; preview.ShowWeekly = settings.ShowWeekly; preview.UsageBarColors = settings.UsageBarColors; preview.Usage = usage; preview.Theme = theme;
            preview.AccessibleName = IndicatorView.LiveAccessibleText(usage, settings.ShowWeekly); previewPanel.Controls.Add(preview);
            var result = new MenuControls();
            result.Style = MenuSelect(appearance, "Style", 120, new[] { "Essential", "Lines", "Blocks" }, (int)settings.Style, "Indicator style");
            result.Trigger = MenuSelect(appearance, "Open details", 158, new[] { "On hover", "On click" }, (int)settings.DetailsTrigger, "Open details");
            result.Weekly = new SettingsToggle { Text = "Weekly quota", Location = new Point(12, 196), Size = new Size(316, 26), Checked = settings.ShowWeekly, AccessibleName = "Show weekly quota in the indicator" }; appearance.Controls.Add(result.Weekly);
            result.UsageColors = new SettingsToggle { Text = "Usage-based bar colors", Location = new Point(12, 226), Size = new Size(316, 26), Checked = settings.UsageBarColors, AccessibleName = "Usage-based bar colors" };
            appearance.Controls.Add(result.UsageColors);
            settingsHints.SetToolTip(result.UsageColors, "Green at 0%, yellow at 50%, red at 100% used. Applies to Details, Lines and Blocks.");
            var animation = MenuButton(appearance, "Preview animation", 0, 276, 158, 30, "action"); animation.Click += delegate { PreviewAnimation(); };
            result.Details = MenuButton(appearance, "Details", 180, 276, 160, 30, "action");
            var accounts = settingsPages[1];
            for (int provider = 0; provider < 2; provider++)
            {
                int row = 44 + provider * 88;
                var badge = new ProviderMenuMark(provider) { Location = new Point(12, row + 4), Size = new Size(20, 20) }; accounts.Controls.Add(badge);
                var label = MenuLabel(accounts, provider == 0 ? "Claude" : "Codex", 42, row, 184, 23, provider == 0 ? "claude" : "codex"); label.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                accountLabels[provider] = MenuLabel(accounts, live ? "Connecting..." : "Offline preview", 42, row + 27, 184, 38, "muted"); accountLabels[provider].Font = new Font("Segoe UI", 8.5f);
                var connect = MenuButton(accounts, "Connect", 238, row + 2, 90, 30, "action"); connect.Enabled = live; connect.AccessibleName = provider == 0 ? "Connect Claude" : "Connect Codex";
                if (provider == 0) result.Claude = connect; else result.Codex = connect;

            }
            result.Refresh = MenuButton(accounts, live ? "Refresh" : "Simulate speed", 0, 244, 132, 30, "action");
            result.Disconnect = MenuButton(accounts, "Disconnect", 148, 244, 120, 30, "action"); result.Disconnect.Enabled = live;
            var general = settingsPages[2];
            enabled.Text = "Show indicator"; enabled.Location = new Point(12, 44); enabled.Size = new Size(316, 26); enabled.Checked = true; general.Controls.Add(enabled);
            result.Startup = new SettingsToggle { Text = "Start with Windows", Location = new Point(12, 84), Size = new Size(316, 26) }; general.Controls.Add(result.Startup);

            result.Placement = MenuSelect(general, "Placement", 156, new[] { "Embedded (experimental)", "Overlay" }, (int)settings.Mode, "Placement mode");
            settingsHints.SetToolTip(result.Placement, "Embedded attaches to the taskbar. Overlay uses the same position in a separate window.");
            MenuLabel(general, "Version " + AppInfo.VersionText, 12, 228, 142, 22, "text");
            updateCheckButton = MenuButton(general, "Check for updates", 176, 226, 152, 28, "action");
            updateCheckButton.AccessibleName = "Check for Taskcode updates";
            updateStatus = MenuLabel(general, "Checks GitHub Releases", 12, 254, 316, 18, "muted"); updateStatus.Font = new Font("Segoe UI", 8f);
            result.AutomaticUpdates = new SettingsToggle { Text = "Check automatically", Location = new Point(12, 276), Size = new Size(316, 26), Checked = settings.AutomaticUpdateChecks, AccessibleName = "Check for updates automatically" };
            general.Controls.Add(result.AutomaticUpdates);
            settingsHints.SetToolTip(result.AutomaticUpdates, "Checks GitHub Releases once a day. Installs only when you choose Install.");
            AddDivider(this, 16, 400, 488);
            status.Location = new Point(18, 411); status.Size = new Size(365, 20); status.AutoEllipsis = true; status.Tag = "muted"; status.Font = new Font("Segoe UI", 8f); status.Text = "Starting..."; Controls.Add(status);
            result.Close = MenuButton(this, "Close", 414, 406, 90, 26, "ghost");
            result.Close.AccessibleName = "Close settings panel";
            Disposed += delegate { settingsHints.Dispose(); };
            ApplySettingsTheme(theme); ShowSettingsPage(0);
            return result;
        }
        private static Label MenuLabel(Control parent, string text, int x, int y, int width, int height, string role)
        { var label = new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height), Tag = role, AutoEllipsis = true }; parent.Controls.Add(label); return label; }
        private static Button MenuButton(Control parent, string text, int x, int y, int width, int height, string role)
        { var button = new SurfaceButton { Text = text, Location = new Point(x, y), Size = new Size(width, height), Tag = role, FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false }; button.FlatAppearance.BorderSize = role == "action" ? 1 : 0; button.SizeChanged += delegate { UiSurface.Round(button, 7); }; UiSurface.Round(button, 7); parent.Controls.Add(button); return button; }
        private static void AddDivider(Control parent, int x, int y, int width) { parent.Controls.Add(new Panel { Location = new Point(x, y), Size = new Size(width, 1), Tag = "divider" }); }
        private static ComboBox MenuSelect(Control parent, string label, int y, string[] choices, int selected, string accessibleName)
        {
            MenuLabel(parent, label, 12, y + 4, 112, 24, "text");
            var select = new SettingsSelect { Location = new Point(132, y), Width = 196, AccessibleName = accessibleName };
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
            palette = TaskbarTheme.AppPalette(palette);
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
                button.BackColor = index == settingsPage ? Mix(BackColor, theme.Codex, 0.12) : Mix(BackColor, Color.Black, .10);
                button.ForeColor = index == settingsPage ? theme.Codex : theme.Muted;
                if (theme.HighContrast && index == settingsPage) { button.BackColor = SystemColors.Highlight; button.ForeColor = SystemColors.HighlightText; }
                button.AccessibleDescription = index == settingsPage ? "Selected" : "";
            }
        }
        internal static void ApplyMenuColors(Control root, TaskbarTheme palette)
        {
            foreach (Control control in root.Controls)
            {
                string role = control.Tag as string;
                bool card = IsSettingsCard(root, control);
                control.BackColor = card && !(control is Button) && !(control is ComboBox) ? UiSurface.Card(palette) : role == "divider" ? Mix(palette.PreviewBackground, palette.Text, .12) : role == "action" || role == "surface" || control is ComboBox ? Mix(palette.PreviewBackground, palette.Text, .035) : palette.PreviewBackground;
                control.ForeColor = role == "muted" ? palette.Muted : role == "claude" ? palette.Claude : role == "codex" ? palette.Codex : palette.Text;
                var button = control as Button;
                if (button != null) { button.FlatAppearance.BorderColor = Mix(palette.PreviewBackground, palette.Text, .09); button.FlatAppearance.MouseOverBackColor = Mix(palette.PreviewBackground, palette.Codex, .10); button.FlatAppearance.MouseDownBackColor = Mix(palette.PreviewBackground, palette.Text, .15); }
                var mark = control as ProviderMenuMark; if (mark != null) mark.Palette = palette;
                ApplyMenuColors(control, palette);
            }
        }
        private static bool IsSettingsCard(Control root, Control control)
        {
            string page = root.AccessibleName ?? "";
            float top = control.Top * 312f / Math.Max(1, root.Height);
            return page == "Appearance settings" ? top >= 112 && top < 262 :
                page == "Accounts settings" ? top >= 40 && top < 208 :
                page == "General settings" && top >= 40 && top < 308;
        }
        private void PaintSettingsCards(Panel page, Graphics graphics)
        {
            var saved = graphics.Save(); graphics.ScaleTransform(page.Width / 352f, page.Height / 312f);
            RectangleF[] cards = page == settingsPages[0] ? new[] { new RectangleF(0, 112, 340, 150) } :
                page == settingsPages[1] ? new[] { new RectangleF(0, 40, 340, 80), new RectangleF(0, 128, 340, 80) } :
                new[] { new RectangleF(0, 40, 340, 80), new RectangleF(0, 144, 340, 64), new RectangleF(0, 220, 340, 88) };
            foreach (var rect in cards) UiSurface.Draw(graphics, rect, UiSurface.Card(theme), Mix(BackColor, ForeColor, .07), 10);
            using (var accent = new SolidBrush(theme.Codex)) graphics.FillRectangle(accent, 0, 28, 24, 2);
            graphics.Restore(saved);
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
            if (preview.Parent != null)
            {
                scale = Math.Min(scale, Math.Max(1, preview.Parent.ClientSize.Width - preview.Left * 2) / (float)IndicatorView.LogicalWidth(settings.Style, settings.ShowWeekly));
                scale = Math.Min(scale, Math.Max(1, preview.Parent.ClientSize.Height - preview.Top * 2) / (float)IndicatorView.LogicalHeight);
            }
            preview.Size = new Size((int)Math.Round(IndicatorView.LogicalWidth(settings.Style, settings.ShowWeekly) * scale), (int)Math.Round(IndicatorView.LogicalHeight * scale));
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var saved = e.Graphics.Save(); e.Graphics.ScaleTransform(Width / 520f, Height / 440f);
            using (var sidebar = new SolidBrush(Mix(BackColor, Color.Black, .10))) e.Graphics.FillRectangle(sidebar, 0, 64, 140, 336);
            using (var line = new Pen(Mix(BackColor, ForeColor, .045))) e.Graphics.DrawLine(line, 140, 78, 140, 386);
            e.Graphics.Restore(saved);
            using (var pen = new Pen(Mix(BackColor, ForeColor, .12))) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
        private sealed class SurfaceButton : Button
        {
            private bool hovered, pressed;
            internal SurfaceButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
            protected override void OnMouseEnter(EventArgs e) { hovered = true; base.OnMouseEnter(e); Invalidate(); }
            protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; base.OnMouseLeave(e); Invalidate(); }
            protected override void OnMouseDown(MouseEventArgs e) { pressed = true; base.OnMouseDown(e); Invalidate(); }
            protected override void OnMouseUp(MouseEventArgs e) { pressed = false; base.OnMouseUp(e); Invalidate(); }
            protected override void OnPaint(PaintEventArgs e)
            {
                var palette = TaskbarTheme.Read(); string role = Tag as string;
                Color outer = role == "navigation" ? Mix(palette.PreviewBackground, Color.Black, .10) : Parent == null ? palette.PreviewBackground : Parent.BackColor;
                using (var brush = new SolidBrush(outer)) e.Graphics.FillRectangle(brush, ClientRectangle);
                Color fill = Enabled && hovered ? (pressed ? FlatAppearance.MouseDownBackColor : FlatAppearance.MouseOverBackColor) : BackColor;
                Color border = role == "action" ? FlatAppearance.BorderColor : fill;
                UiSurface.Draw(e.Graphics, new RectangleF(.5f, .5f, Width - 1, Height - 1), fill, border, Height * .23f);
                Color ink = Enabled ? ForeColor : palette.Muted;
                if (AccessibleName == "Close settings")
                {
                    float unit = Width / 30f, x = Width / 2f, y = Height / 2f, radius = 4 * unit;
                    using (var pen = new Pen(ink, 1.3f * unit))
                    { pen.StartCap = pen.EndCap = System.Drawing.Drawing2D.LineCap.Round; e.Graphics.DrawLine(pen, x - radius, y - radius, x + radius, y + radius); e.Graphics.DrawLine(pen, x + radius, y - radius, x - radius, y + radius); }
                }
                else
                {
                    var bounds = new Rectangle(Padding.Left + 6, 0, Math.Max(1, Width - Padding.Horizontal - 12), Height);
                    var align = TextAlign == ContentAlignment.MiddleLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter;
                    TextRenderer.DrawText(e.Graphics, Text, Font, bounds, ink, align | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
                if (role == "navigation" && AccessibleDescription == "Selected")
                    UiSurface.Draw(e.Graphics, new RectangleF(7, Height * .31f, 2, Height * .38f), palette.Codex, palette.Codex, 1);
                if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(3, 3, Width - 6, Height - 6), ink, fill);
            }
        }
        private sealed class SettingsToggle : CheckBox
        {
            internal SettingsToggle() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
            protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
            protected override void OnPaint(PaintEventArgs e)
            {
                using (var brush = new SolidBrush(BackColor)) e.Graphics.FillRectangle(brush, ClientRectangle);
                float scale = Height / 26f;
                var track = new RectangleF(Width - 30 * scale, (Height - 16 * scale) / 2, 30 * scale, 16 * scale);
                var palette = TaskbarTheme.Read();
                Color fill = Checked ? palette.Codex : Mix(BackColor, ForeColor, .18);
                UiSurface.Draw(e.Graphics, track, fill, fill, 8 * scale);
                using (var knob = new SolidBrush(Checked ? palette.PreviewBackground : palette.Text))
                    e.Graphics.FillEllipse(knob, track.X + (Checked ? 16 : 2) * scale, track.Y + 2 * scale, 12 * scale, 12 * scale);
                TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(0, 0, Width - (int)(42 * scale), Height), Enabled ? ForeColor : palette.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(0, 2, Width - (int)(42 * scale), Height - 4), ForeColor, BackColor);
            }
        }
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
                using (var brush = new SolidBrush(UiSurface.Card(TaskbarTheme.Read()))) graphics.FillRectangle(brush, ClientRectangle);
                UiSurface.Draw(graphics, new RectangleF(.5f, .5f, Width - 1, Height - 1), BackColor, Mix(BackColor, ForeColor, .12), 5);
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
