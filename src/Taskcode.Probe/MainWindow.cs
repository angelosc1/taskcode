using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal sealed partial class MainWindow : Form
    {
        internal const string WindowTitle = "Taskcode settings";
        internal static readonly uint OpenSettingsMessage = Native.RegisterWindowMessage("Taskcode.OpenSettings");
        internal static readonly uint ExitMessage = Native.RegisterWindowMessage("Taskcode.Exit");
        internal static readonly uint PreviewAnimationMessage = Native.RegisterWindowMessage("Taskcode.PreviewAnimation");
        private readonly Settings settings = Settings.Load();
        private readonly TaskbarWatcher watcher = new TaskbarWatcher();
        private readonly Timer timer = new Timer { Interval = 250 };
        private readonly Timer hoverTimer = new Timer { Interval = 16 };
        private readonly Timer animationTimer = new Timer { Interval = 33 };
        private readonly IndicatorView preview = new IndicatorView();
        private readonly Label status = new Label();
        private readonly CheckBox enabled = new CheckBox();
        private IndicatorWindow indicator;
        private readonly UsageDemo usage = new UsageDemo();
        private readonly DetailWindow[] providerDetails;
        private DetailWindow details;
        private int selectedProvider;
        private IntPtr attachedParent;
        private Rectangle indicatorBounds;
        private string saveError;
        private TaskbarTheme theme = TaskbarTheme.Read();
        private DateTime nextThemeCheck;
        private readonly NotifyIcon tray;
        private readonly ContextMenuStrip trayMenu;
        private readonly Icon trayImage;
        private bool startInTray, exiting;
        private readonly AccountConnections connections;
        private readonly Label[] accountLabels = new Label[2];
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
            MaximizeBox = false;
            var menu = BuildSettingsMenu(connectAccounts);
            var style = menu.Style; var mode = menu.Placement; var trigger = menu.Trigger;
            var weekly = menu.Weekly; var startup = menu.Startup;
            var detailsButton = menu.Details; var exit = menu.Exit; var simulate = menu.Refresh;
            var claudeConnect = menu.Claude; var codexConnect = menu.Codex; var disconnect = menu.Disconnect;
            try { startup.Checked = StartupRegistration.IsEnabled(Application.ExecutablePath); }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)) throw;
                startup.Enabled = false;
                saveError = "Windows startup settings are unavailable.";
            }

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
            simulate.Click += delegate
            {
                if (connections != null) { connections.Refresh(); return; }
                usage.Cycle(DateTime.UtcNow);
                details.RefreshUsage();
                TickLayout();
                ShowDetails(true, true, selectedProvider);
            };
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
            preview.MouseEnter += delegate
            {
                int provider = IndicatorView.ProviderAt(preview.PointToClient(Cursor.Position), preview.ClientSize, settings.Style, settings.ShowWeekly);
                if (provider >= 0) ShowDetails(false, true, provider);
            };
            exit.Click += delegate { RequestExit(); };
            style.SelectedIndexChanged += delegate
            {
                settings.Style = (IndicatorStyle)style.SelectedIndex;
                preview.Style = settings.Style;
                ResizeIndicatorPreview();
                preview.Invalidate();
                Save();
                TickLayout();
            };
            weekly.CheckedChanged += delegate
            {
                settings.ShowWeekly = weekly.Checked;
                preview.ShowWeekly = settings.ShowWeekly;
                preview.AccessibleName = IndicatorView.LiveAccessibleText(usage, settings.ShowWeekly);
                ResizeIndicatorPreview();
                preview.Invalidate();
                Save();
                TickLayout();
            };
            mode.SelectedIndexChanged += delegate { settings.Mode = (HostMode)mode.SelectedIndex; RecreateIndicator(); Save(); TickLayout(); };
            trigger.SelectedIndexChanged += delegate
            {
                settings.DetailsTrigger = (DetailsTrigger)trigger.SelectedIndex;
                details.Hide();
                Save();
            };
            enabled.CheckedChanged += delegate { TickLayout(); };
            timer.Tick += delegate { TickLayout(); };
            hoverTimer.Tick += delegate { UpdateHover(); };
            preview.MouseMove += delegate { UpdateHover(); };
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
                timer.Stop(); timer.Dispose(); hoverTimer.Dispose(); animationTimer.Dispose(); watcher.Dispose();
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
            timer.Start(); hoverTimer.Start();
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
            accountWindows[provider].ApplyTheme(theme);
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
        internal void RequestExit() { Program.RecordLifecycle("Exit requested"); exiting = true; Close(); }
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
            indicator.MouseMove += delegate { UpdateHover(); };
            indicator.ContextRequested += delegate
            {
                details.Hide();
                if (!trayMenu.Visible) trayMenu.Show(Cursor.Position);
            };
            indicator.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                int provider = IndicatorView.ProviderAt(e.Location, indicator.ClientSize, settings.Style, settings.ShowWeekly);
                if (provider >= 0) ShowDetails(true, false, provider);
            };
            indicator.MouseEnter += delegate
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
            SetSettingsStatus(reason + (saveError == null ? "" : "\n" + saveError), true);
        }
        private void TickLayout()
        {
            if (connections != null)
            {
                connections.Tick(usage);
                UpdateAccountLabels();
                preview.AccessibleName = IndicatorView.LiveAccessibleText(usage, settings.ShowWeekly);
                preview.Invalidate();
            }
            usage.Tick(DateTime.UtcNow);
            int pulse = usage.PulseKey(DateTime.UtcNow, settings.ShowWeekly);
            int activityKey = CurrentActivity;
            animationTimer.Enabled = (pulse != 0 || activityKey != 0) && enabled.Checked && SpeedArrows.IsAnimated;
            if (preview.ActivityKey != activityKey)
            {
                preview.ActivityKey = activityKey; preview.Invalidate();
                foreach (var panel in providerDetails) if (panel.Visible) panel.RefreshUsage();
            }
            if (preview.PulseKey != pulse) { preview.PulseKey = pulse; preview.Invalidate(); }
            if (DateTime.UtcNow >= nextThemeCheck)
            {
                nextThemeCheck = DateTime.UtcNow.AddSeconds(1);
                var current = TaskbarTheme.Read();
                if (current.Dark != theme.Dark || current.HighContrast != theme.HighContrast) { theme = current; ApplySettingsTheme(theme); }
                preview.Theme = theme;
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
            indicatorBounds = indicator.RectangleToScreen(indicator.ClientRectangle);
            SetSettingsStatus((expected == IntPtr.Zero ? "Overlay active" : "Embedded child window active") + " · " + layout.Dpi + " DPI" + (saveError == null ? "" : "\n" + saveError), saveError != null);
            UpdateHover();
        }
        private void UpdateHover()
        {
            if (settings.DetailsTrigger != DetailsTrigger.Hover || trayMenu.Visible) return;
            var pointer = Cursor.Position;
            bool fromPreview = Visible && preview.Visible && preview.RectangleToScreen(preview.ClientRectangle).Contains(pointer);
            var bounds = fromPreview ? preview.RectangleToScreen(preview.ClientRectangle) : indicatorBounds;
            if (!fromPreview && (indicator == null || !indicator.Visible)) return;
            int provider = IndicatorView.ProviderAt(new Point(pointer.X - bounds.X, pointer.Y - bounds.Y), bounds.Size, settings.Style, settings.ShowWeekly);
            if (provider >= 0) ShowDetails(false, fromPreview, provider);
        }
        internal int VisibleDetailProvider { get { return details.Visible ? details.ProviderIndex : -1; } }
        internal int VisibleDetailCount { get { int count = 0; foreach (var panel in providerDetails) if (panel.Visible) count++; return count; } }
        internal void ShowDetails(bool focus, bool fromPreview, int provider = 0)
        {
            // Native entry and frame-rate pointer tracking share this guard.
            if (provider < 0 || provider > 1) return;
            if (!focus && (settings.DetailsTrigger != DetailsTrigger.Hover || (details.Visible && selectedProvider == provider))) return;
            if (selectedProvider != provider)
            {
                details.Hide();
                selectedProvider = provider;
                details = providerDetails[provider];
            }
            Rectangle anchor = fromPreview || indicatorBounds.IsEmpty ? preview.RectangleToScreen(preview.ClientRectangle) : indicatorBounds;
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
