using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal sealed class MainWindow : Form
    {
        internal const string WindowTitle = "Taskcode settings";
        internal static readonly uint OpenSettingsMessage = Native.RegisterWindowMessage("Taskcode.OpenSettings");
        internal static readonly uint ExitMessage = Native.RegisterWindowMessage("Taskcode.Exit");
        internal static readonly uint PreviewAnimationMessage = Native.RegisterWindowMessage("Taskcode.PreviewAnimation");
        private readonly Settings settings = Settings.Load();
        private readonly TaskbarWatcher watcher = new TaskbarWatcher();
        private readonly Timer timer = new Timer { Interval = 250 };
        private readonly Timer animationTimer = new Timer { Interval = 33 };
        private readonly IndicatorView preview = new IndicatorView();
        private readonly Label status = new Label();
        private readonly CheckBox enabled = new CheckBox();
        private IndicatorWindow indicator;
        private readonly UsageDemo usage = new UsageDemo();
        private readonly DetailWindow[] providerDetails;
        private DetailWindow details;
        private int selectedProvider, hoverProvider = -1;
        private IntPtr attachedParent;
        private Rectangle indicatorBounds;
        private string saveError;
        private TaskbarTheme theme = TaskbarTheme.Read();
        private DateTime nextThemeCheck;
        private DateTime? hoverSince;
        private readonly NotifyIcon tray;
        private readonly ContextMenuStrip trayMenu;
        private readonly Icon trayImage;
        private bool startInTray, exiting;
        private readonly AccountConnections connections;
        private readonly Label accountNotice;
        private readonly AccountConnectionWindow[] accountWindows = new AccountConnectionWindow[2];
        private DateTime ripplePreviewUntil;
        private int CurrentActivity { get { return DateTime.UtcNow < ripplePreviewUntil ? 3 : usage.ActivityAt(DateTime.UtcNow); } }

        internal MainWindow(bool startInTray = false, bool connectAccounts = true)
        {
            this.startInTray = startInTray;
            if (connectAccounts)
            {
                usage.BeginLive();
                connections = new AccountConnections { CodexEnabled = settings.CodexEnabled, ClaudeEnabled = settings.ClaudeEnabled, CodexPrivateProfile = settings.CodexPrivateProfile, ClaudePrivateProfile = settings.ClaudePrivateProfile };
                connections.OpenLogin = delegate(string url)
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
                    catch (System.ComponentModel.Win32Exception) { saveError = connections.CodexAuthMessage = "Could not open the browser. Use Open sign-in page again or check your default browser."; }
                };
                if (!settings.ClaudeEnabled) usage.ClearProvider(0, "Disconnected from Taskcode.");
                if (!settings.CodexEnabled) usage.ClearProvider(1, "Disconnected from Taskcode.");
            }
            providerDetails = new[] { new DetailWindow(usage, 0), new DetailWindow(usage, 1) };
            details = providerDetails[0];
            Text = WindowTitle;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(590, 762);
            MinimumSize = Size;
            MaximumSize = Size;
            MaximizeBox = false;
            BackColor = Color.FromArgb(25, 28, 33);
            ForeColor = Color.FromArgb(237, 240, 244);
            Font = new Font("Segoe UI", 10f);
            AddLabel("Taskcode", 24, 20, 23, FontStyle.Bold, 540, 45);
            AddLabel("AI quota monitor", 26, 70, 11, FontStyle.Regular, 540, 26);
            accountNotice = AddLabel(connectAccounts ? "Account data / Connecting..." : "DEMO DATA / Offline preview", 26, 108, 9, FontStyle.Regular, 540, 42);
            accountNotice.ForeColor = IndicatorView.Claude;
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
            AddLabel("Open details", 26, 240, 10, FontStyle.Regular, 180, 26);
            var trigger = new ComboBox { Location = new Point(220, 236), Width = 340, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Open details" };
            trigger.Items.AddRange(new object[] { "Hover (default)", "Click only" });
            trigger.SelectedIndex = (int)settings.DetailsTrigger;
            Controls.Add(trigger);
            AddLabel("Preview", 26, 287, 10, FontStyle.Regular, 180, 26);
            preview.Location = new Point(220, 282);
            preview.Size = new Size(IndicatorView.LogicalWidth(settings.Style, settings.ShowWeekly), IndicatorView.LogicalHeight);
            preview.Style = settings.Style;
            preview.ShowWeekly = settings.ShowWeekly;
            preview.AccessibleName = IndicatorView.LiveAccessibleText(usage, settings.ShowWeekly);
            preview.Theme = theme;
            preview.Usage = usage;
            preview.BackColor = theme.PreviewBackground;
            Controls.Add(preview);
            enabled.Text = "Show indicator on the primary taskbar";
            enabled.Location = new Point(26, 343);
            enabled.Size = new Size(520, 28);
            enabled.Checked = true;
            Controls.Add(enabled);
            var weekly = new CheckBox { Text = "Show weekly quota in the indicator", Location = new Point(26, 375), Size = new Size(520, 28), Checked = settings.ShowWeekly };
            Controls.Add(weekly);
            var startup = new CheckBox { Text = "Start with Windows", Location = new Point(26, 407), Size = new Size(520, 28) };
            try { startup.Checked = StartupRegistration.IsEnabled(Application.ExecutablePath); }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)) throw;
                startup.Enabled = false;
                saveError = "Windows startup settings are unavailable.";
            }
            Controls.Add(startup);
            bool restoringStartup = false;
            startup.CheckedChanged += delegate
            {
                if (restoringStartup) return;
                bool requested = startup.Checked;
                try { StartupRegistration.SetEnabled(Application.ExecutablePath, requested); saveError = null; }
                catch (Exception ex)
                {
                    if (!(ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)) throw;
                    restoringStartup = true;
                    startup.Checked = !requested;
                    restoringStartup = false;
                    saveError = "Could not change Windows startup settings.";
                }
                TickLayout();
            };
            status.Location = new Point(26, 447);
            status.Size = new Size(534, 74);
            status.ForeColor = Color.FromArgb(184, 194, 207);
            Controls.Add(status);
            var detailsButton = new Button { Text = "Details", Location = new Point(26, 702), Size = new Size(145, 34) };
            var exit = new Button { Text = "Exit Taskcode", Location = new Point(415, 702), Size = new Size(145, 34) };
            Controls.Add(detailsButton);
            Controls.Add(exit);
            var simulate = new Button { Text = connectAccounts ? "Refresh accounts" : "Simulate speed", Location = new Point(185, 702), Size = new Size(210, 34) };
            Controls.Add(simulate);
            simulate.Click += delegate
            {
                if (connections != null) { connections.Refresh(); return; }
                usage.Cycle(DateTime.UtcNow);
                details.RefreshUsage();
                TickLayout();
                ShowDetails(true, true, selectedProvider);
            };
            AddLabel("Accounts", 26, 534, 11, FontStyle.Bold, 520, 25);
            var claudeConnect = new Button { Text = "Connect Claude", Location = new Point(26, 569), Size = new Size(168, 34), Enabled = connectAccounts };
            var codexConnect = new Button { Text = "Connect Codex", Location = new Point(210, 569), Size = new Size(168, 34), Enabled = connectAccounts };
            var disconnect = new Button { Text = "Disconnect...", Location = new Point(394, 569), Size = new Size(166, 34), Enabled = connectAccounts };
            Controls.Add(claudeConnect); Controls.Add(codexConnect); Controls.Add(disconnect);
            AddLabel("Connect opens account sign-in and connection status.\nChoose a browser sign-in or use an existing CLI account.", 26, 617, 9, FontStyle.Regular, 534, 60);
            claudeConnect.Click += delegate
            {
                OpenAccountConnection(0);
            };
            codexConnect.Click += delegate { OpenAccountConnection(1); };
            var disconnectMenu = new ContextMenuStrip();
            disconnectMenu.Items.Add("Disconnect Claude (restore status line)", null, delegate
            {
                try { connections.ClaudeAuth.Cancel(); ClaudeBridge.Uninstall(); settings.ClaudeEnabled = connections.ClaudeEnabled = false; usage.ClearProvider(0, "Disconnected from Taskcode."); Save(); }
                catch (Exception) { MessageBox.Show(this, "Could not restore Claude Code's status line. Settings were left connected.", "Disconnect Claude", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            });
            disconnectMenu.Items.Add("Disconnect Codex from Taskcode", null, delegate { connections.CancelCodex(); settings.CodexEnabled = connections.CodexEnabled = false; usage.ClearProvider(1, "Disconnected from Taskcode. Your Codex login was preserved."); Save(); });
            disconnect.Click += delegate { disconnectMenu.Show(disconnect, new Point(0, disconnect.Height)); };
            var detailsMenu = new ContextMenuStrip();
            detailsMenu.Items.Add("Claude details", null, delegate { ShowDetails(true, true, 0); });
            detailsMenu.Items.Add("Codex details", null, delegate { ShowDetails(true, true, 1); });
            detailsButton.Click += delegate { detailsMenu.Show(detailsButton, new Point(0, detailsButton.Height)); };
            preview.Click += delegate
            {
                int provider = IndicatorView.ProviderAt(preview.PointToClient(Cursor.Position), preview.ClientSize, settings.Style, settings.ShowWeekly);
                ShowDetails(true, true, provider < 0 ? selectedProvider : provider);
            };
            exit.Click += delegate { RequestExit(); };
            style.SelectedIndexChanged += delegate
            {
                settings.Style = (IndicatorStyle)style.SelectedIndex;
                preview.Style = settings.Style;
                preview.Width = IndicatorView.LogicalWidth(settings.Style, settings.ShowWeekly);
                preview.Invalidate();
                Save();
                TickLayout();
            };
            weekly.CheckedChanged += delegate
            {
                settings.ShowWeekly = weekly.Checked;
                preview.ShowWeekly = settings.ShowWeekly;
                preview.AccessibleName = IndicatorView.LiveAccessibleText(usage, settings.ShowWeekly);
                preview.Width = IndicatorView.LogicalWidth(settings.Style, settings.ShowWeekly);
                preview.Invalidate();
                Save();
                TickLayout();
            };
            mode.SelectedIndexChanged += delegate { settings.Mode = (HostMode)mode.SelectedIndex; RecreateIndicator(); Save(); TickLayout(); };
            trigger.SelectedIndexChanged += delegate
            {
                settings.DetailsTrigger = (DetailsTrigger)trigger.SelectedIndex;
                hoverSince = null;
                hoverProvider = -1;
                details.Hide();
                Save();
            };
            enabled.CheckedChanged += delegate { TickLayout(); };
            timer.Tick += delegate { TickLayout(); };
            animationTimer.Tick += delegate
            {
                int key = usage.PulseKey(DateTime.UtcNow, settings.ShowWeekly);
                int activity = CurrentActivity;
                preview.PulseKey = key;
                preview.ActivityKey = activity;
                preview.Invalidate();
                if (indicator != null && !indicator.IsDisposed && indicator.Visible)
                { indicator.PulseKey = key; indicator.ActivityKey = activity; indicator.RenderAlpha(); }
                if (key == 0 && activity == 0) animationTimer.Stop();
            };
            trayImage = TrayArtwork.Create();
            Icon = trayImage;
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Open settings", null, delegate { OpenSettings(); });
            trayMenu.Items.Add("Preview animation (8 seconds)", null, delegate { PreviewAnimation(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Exit", null, delegate { RequestExit(); });
            trayMenu.Closed += delegate
            {
                hoverSince = null; hoverProvider = -1;
                if (indicator != null && !indicator.IsDisposed) indicator.InvalidateSurface();
            };
            tray = new NotifyIcon { Icon = trayImage, Text = "Taskcode - AI quota monitor", ContextMenuStrip = trayMenu, Visible = true };
            tray.MouseClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) OpenSettings(); };
            Resize += delegate { if (WindowState == FormWindowState.Minimized) Hide(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            };
            FormClosed += delegate
            {
                timer.Stop(); timer.Dispose(); animationTimer.Dispose(); watcher.Dispose();
                if (connections != null) connections.Dispose();
                foreach (var accountWindow in accountWindows) if (accountWindow != null && !accountWindow.IsDisposed) accountWindow.Dispose();
                disconnectMenu.Dispose();
                if (indicator != null && !indicator.IsDisposed) indicator.Dispose();
                foreach (var panel in providerDetails) panel.Dispose();
                detailsMenu.Dispose();
                tray.Visible = false; tray.Dispose(); trayMenu.Dispose(); trayImage.Dispose();
            };
            // Keep the host alive even when startup suppresses the settings window.
            CreateHandle();
            RecreateIndicator();
            timer.Start();
        }
        protected override void SetVisibleCore(bool value)
        {
            if (value && startInTray) { startInTray = false; base.SetVisibleCore(false); return; }
            base.SetVisibleCore(value);
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == OpenSettingsMessage) { OpenSettings(); return; }
            if (message.Msg == ExitMessage) { RequestExit(); return; }
            if (message.Msg == PreviewAnimationMessage) { PreviewAnimation(); return; }
            base.WndProc(ref message);
        }
        internal bool TrayVisible { get { return tray.Visible; } }
        internal void OpenAccountConnection(int provider)
        {
            if (connections == null) return;
            if (accountWindows[provider] == null || accountWindows[provider].IsDisposed)
                accountWindows[provider] = new AccountConnectionWindow(provider, connections, usage, delegate(bool signIn)
                {
                    usage.ClearProvider(provider, signIn ? "Preparing sign-in..." : "Reading local account...");
                    if (provider == 0)
                    {
                        ClaudeBridge.Install(); settings.ClaudeEnabled = true; settings.ClaudePrivateProfile = signIn;
                        connections.ConnectClaude(signIn);
                    }
                    else { settings.CodexEnabled = true; settings.CodexPrivateProfile = signIn; connections.ConnectCodex(signIn); }
                    Save();
                });
            accountWindows[provider].Show(this); accountWindows[provider].Activate();
        }
        internal void PreviewAnimation() { ripplePreviewUntil = DateTime.UtcNow.AddSeconds(8); TickLayout(); }
        internal void OpenSettings()
        {
            startInTray = false;
            WindowState = FormWindowState.Normal;
            Show();
            Activate();
        }
        internal void RequestExit() { exiting = true; Close(); }
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
            indicator.ContextRequested += delegate
            {
                hoverSince = null; hoverProvider = -1;
                details.Hide();
                if (!trayMenu.Visible) trayMenu.Show(Cursor.Position);
            };
            indicator.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                int provider = IndicatorView.ProviderAt(e.Location, indicator.ClientSize, settings.Style, settings.ShowWeekly);
                if (provider >= 0) ShowDetails(true, false, provider);
            };
            indicator.MouseHover += delegate
            {
                int provider = IndicatorView.ProviderAt(indicator.PointToClient(Cursor.Position), indicator.ClientSize, settings.Style, settings.ShowWeekly);
                if (provider >= 0) ShowDetails(false, false, provider);
            };
        }
        private void PauseIndicator(string reason)
        {
            if (indicator != null && !indicator.IsDisposed) indicator.Hide();
            details.Hide();
            indicatorBounds = Rectangle.Empty;
            hoverSince = null;
            hoverProvider = -1;
            status.Text = reason + (saveError == null ? "" : "\n" + saveError);
        }
        private void TickLayout()
        {
            if (connections != null)
            {
                connections.Tick(usage);
                accountNotice.Text = "Claude: " + usage.AccountStatus[0] + "\nCodex: " + usage.AccountStatus[1];
                preview.AccessibleName = IndicatorView.LiveAccessibleText(usage, settings.ShowWeekly);
                preview.Invalidate();
            }
            usage.Tick(DateTime.UtcNow);
            int pulse = usage.PulseKey(DateTime.UtcNow, settings.ShowWeekly);
            int activityKey = CurrentActivity;
            animationTimer.Enabled = (pulse != 0 || activityKey != 0) && enabled.Checked && SpeedArrows.IsAnimated;
            if (preview.ActivityKey != activityKey) { preview.ActivityKey = activityKey; preview.Invalidate(); }
            if (preview.PulseKey != pulse) { preview.PulseKey = pulse; preview.Invalidate(); }
            if (DateTime.UtcNow >= nextThemeCheck)
            {
                nextThemeCheck = DateTime.UtcNow.AddSeconds(1);
                var current = TaskbarTheme.Read();
                if (current.Dark != theme.Dark || current.HighContrast != theme.HighContrast) theme = current;
                preview.Theme = theme;
                preview.BackColor = theme.PreviewBackground;
                preview.Invalidate();
                foreach (var panel in providerDetails) panel.ApplyTheme(theme);
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
            int width = (int)Math.Round(IndicatorView.LogicalWidth(settings.Style, settings.ShowWeekly) * scale);
            int height = (int)Math.Round(IndicatorView.LogicalHeight * scale);
            Rectangle slot;
            if (!Placement.TrySlot(layout.Bar, layout.Tray, layout.OccupiedRight, width, height, (int)Math.Round(8 * scale), out slot))
            { PauseIndicator("Not enough verified space before the system tray.\nTry Essential or reduce the number of taskbar buttons."); return; }
            if (indicator == null || indicator.IsDisposed || (attachedParent != IntPtr.Zero && !Native.IsWindow(indicator.Handle))) RecreateIndicator();
            indicator.DisplayStyle = settings.Style;
            indicator.ShowWeekly = settings.ShowWeekly;
            indicator.PulseKey = pulse;
            indicator.ActivityKey = activityKey;
            indicator.Usage = usage;
            indicator.AccessibleName = IndicatorView.LiveAccessibleText(usage, settings.ShowWeekly);
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
            status.Text = (expected == IntPtr.Zero ? "Overlay active" : "Embedded child window active") + " · " + layout.Dpi + " DPI · " + (theme.Dark ? "Dark" : "Light") + " taskbar\n" + (usage.IsLive ? "Account data · -- means unavailable or stale" : "DEMO · Recent-history scenario: " + usage.ScenarioName) + (saveError == null ? "" : "\n" + saveError);
            // The alpha surface has real transparent gaps between glyphs. Track the
            // complete indicator rectangle so those gaps do not reset hover dwell.
            if (trayMenu.Visible) { hoverSince = null; hoverProvider = -1; return; }
            if (indicatorBounds.Contains(Cursor.Position))
            {
                if (settings.DetailsTrigger == DetailsTrigger.Hover)
                {
                    int provider = IndicatorView.ProviderAt(new Point(Cursor.Position.X - indicatorBounds.X, Cursor.Position.Y - indicatorBounds.Y), indicatorBounds.Size, settings.Style, settings.ShowWeekly);
                    if (provider != hoverProvider) { hoverProvider = provider; hoverSince = DateTime.UtcNow; }
                    if (!hoverSince.HasValue) hoverSince = DateTime.UtcNow;
                    if ((!details.Visible || selectedProvider != provider) && DateTime.UtcNow - hoverSince.Value > TimeSpan.FromMilliseconds(400)) ShowDetails(false, false, provider);
                }
                else { hoverSince = null; hoverProvider = -1; }
            }
            else
            {
                hoverSince = null;
                hoverProvider = -1;
            }
        }
        internal int VisibleDetailProvider { get { return details.Visible ? details.ProviderIndex : -1; } }
        internal int VisibleDetailCount { get { int count = 0; foreach (var panel in providerDetails) if (panel.Visible) count++; return count; } }
        internal void ShowDetails(bool focus, bool fromPreview, int provider = 0)
        {
            // Both native MouseHover and rectangle-based dwell share this guard.
            if (provider < 0 || provider > 1) return;
            if (!focus && (settings.DetailsTrigger != DetailsTrigger.Hover || (details.Visible && selectedProvider == provider))) return;
            if (selectedProvider != provider)
            {
                details.Hide();
                selectedProvider = provider;
                details = providerDetails[provider];
            }
            Rectangle anchor = fromPreview || indicatorBounds.IsEmpty ? RectangleToScreen(preview.Bounds) : indicatorBounds;
            var providerAnchor = IndicatorView.ProviderBounds(provider, anchor.Size, settings.Style, settings.ShowWeekly);
            anchor = new Rectangle(anchor.X + providerAnchor.X, anchor.Y, providerAnchor.Width, anchor.Height);
            var area = Screen.FromRectangle(anchor).WorkingArea;
            int x = Math.Max(area.Left, Math.Min(anchor.Right - details.Width, area.Right - details.Width));
            int y = anchor.Top - details.Height - 8;
            if (y < area.Top) y = Math.Min(area.Bottom - details.Height, anchor.Bottom + 8);
            details.Location = new Point(x, y);
            details.HoverAnchor = anchor;
            details.Show();
            if (focus) details.Activate();
        }
    }
}
