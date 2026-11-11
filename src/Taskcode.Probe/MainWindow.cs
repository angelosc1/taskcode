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
        private readonly Settings settings = Settings.Load();
        private readonly TaskbarWatcher watcher = new TaskbarWatcher();
        private readonly Timer timer = new Timer { Interval = 250 };
        private readonly Timer hoverTimer = new Timer { Interval = 16 };
        private double lastLogoFrame = SpeedArrows.Seconds;
        private readonly Timer animationTimer = new Timer { Interval = 33 };
        private readonly IndicatorView preview = new IndicatorView();
        private readonly Label status = new Label();
        private readonly CheckBox enabled = new SettingsToggle();
        private IndicatorWindow indicator;
        private readonly UsageDemo usage = new UsageDemo();
        private readonly DetailWindow[] providerDetails;
        private DetailWindow details;
        private int selectedProvider;
        private IntPtr attachedParent;
        private Rectangle indicatorBounds;
        private string indicatorState;
        private string saveError;
        private TaskbarTheme theme = TaskbarTheme.Read();
        private DateTime nextThemeCheck;
        private readonly NotifyIcon tray;
        private readonly ContextMenuStrip trayMenu;
        private readonly Icon trayImage;
        private bool startInTray, exiting;
        private readonly AccountConnections connections;
        private readonly OptionalProviderConnections optionalConnections = new OptionalProviderConnections();
        private OptionalProvidersWindow optionalAccounts;
        private readonly OptionalProviderDetails[] optionalDetails = new OptionalProviderDetails[ProviderCatalog.Count];
        private readonly UpdateService updateService = new UpdateService();
        private Button updateCheckButton;
        private Button providerDetailsButton;
        private Label updateStatus;
        private string notifiedUpdate;
        private readonly Label[] accountLabels = new Label[2];
        private readonly AccountConnectionWindow[] accountWindows = new AccountConnectionWindow[2];
        private int CurrentActivity { get { return usage.ActivityAt(DateTime.UtcNow); } }

        internal MainWindow(bool startInTray = false, bool connectAccounts = true)
        {
            if (connectAccounts) L10n.SetLanguage(settings.Language);
            usage.OptionalProviders = optionalConnections.Providers;
            if (connectAccounts)
            {
                for (int index = 0; index < ProviderCatalog.Count; index++) optionalConnections.SetEnabled(index, settings.OptionalProvidersEnabled[index]);
            }
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
                usage.CoreEnabled[0] = settings.ClaudeEnabled; usage.CoreEnabled[1] = settings.CodexEnabled;
            }
            providerDetails = new[] { new DetailWindow(usage, 0) { UsageBarColors = settings.UsageBarColors }, new DetailWindow(usage, 1) { UsageBarColors = settings.UsageBarColors } };
            IndicatorView.Marker = settings.TimeMarker; IndicatorView.PaceAlert = settings.PaceAlert;
            details = providerDetails[0];
            Text = WindowTitle;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            var menu = BuildSettingsMenu(connectAccounts);
            var style = menu.Style; var mode = menu.Placement; var trigger = menu.Trigger;
            var weekly = menu.Weekly; var startup = menu.Startup;
            var detailsButton = menu.Details; var closeSettings = menu.Close; var simulate = menu.Refresh;
            providerDetailsButton = detailsButton;
            var claudeConnect = menu.Claude; var codexConnect = menu.Codex; var disconnect = menu.Disconnect;
            try { startup.Checked = StartupRegistration.IsEnabled(Application.ExecutablePath); }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)) throw;
                startup.Enabled = false;
                saveError = "Windows startup settings are unavailable.";
            }

            bool restoringStartup = false;
            menu.AutomaticUpdates.CheckedChanged += delegate { settings.AutomaticUpdateChecks = menu.AutomaticUpdates.Checked; Save(); };
            updateCheckButton.Click += delegate
            {
                if (updateService.Available == null) updateService.CheckNow();
                else if (AppInfo.IsInstalled) updateService.Download();
                else
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppInfo.Repository + "/releases/latest") { UseShellExecute = true }); }
                    catch (System.ComponentModel.Win32Exception) { updateService.LaunchFailed(); }
                }
                RefreshUpdateUi();
            };
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
                try { ClaudeBridge.Uninstall(); SetCoreMonitoring(0, false); }
                catch (Exception) { MessageBox.Show(this, L10n.T("Could not restore Claude Code's status line. Settings were left connected."), L10n.T("Disconnect Claude"), MessageBoxButtons.OK, MessageBoxIcon.Information); }
            });
            disconnectMenu.Items.Add("Disconnect Codex from Taskcode", null, delegate { SetCoreMonitoring(1, false); });
            foreach (ToolStripItem item in disconnectMenu.Items) L10n.Bind(item, this);
            disconnect.Click += delegate { disconnectMenu.Show(disconnect, new Point(0, disconnect.Height)); };
            var detailsMenu = new ContextMenuStrip();
            detailsMenu.Items.Add("Claude details", null, delegate { OpenDetailsFromSettings(0); });
            detailsMenu.Items.Add("Codex details", null, delegate { OpenDetailsFromSettings(1); });
            foreach (ToolStripItem item in detailsMenu.Items) L10n.Bind(item, this);
            detailsButton.Click += delegate { detailsMenu.Show(detailsButton, new Point(0, detailsButton.Height)); };
            preview.Click += delegate
            {
                int provider = IndicatorView.ProviderAt(preview.PointToClient(Cursor.Position), preview.ClientSize, settings.Style, settings.ShowWeekly, usage);
                ShowDetails(true, true, provider < 0 ? selectedProvider : provider);
            };
            preview.MouseEnter += delegate
            {
                int provider = IndicatorView.ProviderAt(preview.PointToClient(Cursor.Position), preview.ClientSize, settings.Style, settings.ShowWeekly, usage);
                if (provider >= 0) ShowDetails(false, true, provider);
            };
            closeSettings.Click += delegate { Hide(); };
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
            menu.UsageColors.CheckedChanged += delegate
            {
                settings.UsageBarColors = menu.UsageColors.Checked;
                preview.UsageBarColors = settings.UsageBarColors; preview.Invalidate();
                foreach (var panel in providerDetails) panel.UsageBarColors = settings.UsageBarColors;
                foreach (var panel in optionalDetails) if (panel != null) panel.UsageBarColors = settings.UsageBarColors;
                Save(); TickLayout();
            };
            mode.SelectedIndexChanged += delegate { settings.Mode = (HostMode)mode.SelectedIndex; RecreateIndicator(); Save(); TickLayout(); };
            menu.TimeMarker.SelectedIndexChanged += delegate
            {
                settings.TimeMarker = IndicatorView.Marker = (TimeMarker)menu.TimeMarker.SelectedIndex;
                preview.Invalidate(); Save(); TickLayout();
            };
            menu.PaceColors.CheckedChanged += delegate
            {
                settings.PaceAlert = IndicatorView.PaceAlert = menu.PaceColors.Checked;
                preview.Invalidate(); Save(); TickLayout();
            };
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
                double nowSeconds = SpeedArrows.Seconds;
                LogoMotion.Update(usage, DateTime.UtcNow);
                LogoMotion.Advance(nowSeconds - lastLogoFrame); lastLogoFrame = nowSeconds;
                int key = 0;
                int activity = CurrentActivity;
                InvalidateLogoControls(this);
                preview.PulseKey = key;
                preview.ActivityKey = activity;
                preview.Invalidate();
                if (indicator != null && !indicator.IsDisposed && indicator.Visible)
                { indicator.PulseKey = key; indicator.ActivityKey = activity; indicator.RenderAlpha(); }
                if (!LogoMotion.IsMoving) animationTimer.Stop();
            };
            trayImage = TrayArtwork.Create();
            Icon = trayImage;
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Open settings", null, delegate { OpenSettings(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Exit", null, delegate { RequestExit(); });
            foreach (ToolStripItem item in trayMenu.Items) L10n.Bind(item, this);
            trayMenu.Closed += delegate
            {
                if (indicator != null && !indicator.IsDisposed) indicator.InvalidateSurface();
            };
            tray = new NotifyIcon { Icon = trayImage, Text = L10n.T("Taskcode - AI quota monitor"), ContextMenuStrip = trayMenu, Visible = true };
            L10n.Bind(this, false);
            tray.BalloonTipClicked += delegate { OpenSettings(); ShowSettingsPage(2); };
            tray.MouseClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) OpenSettings(); };
            Resize += delegate { if (WindowState == FormWindowState.Minimized) Hide(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            };
            FormClosed += delegate
            {
                timer.Stop(); timer.Dispose(); updateService.Dispose(); hoverTimer.Dispose(); animationTimer.Dispose(); watcher.Dispose();
                if (connections != null) connections.Dispose();
                optionalConnections.Dispose();
                if (optionalAccounts != null) optionalAccounts.Dispose();
                foreach (var panel in optionalDetails) if (panel != null) panel.Dispose();
                foreach (var accountWindow in accountWindows) if (accountWindow != null && !accountWindow.IsDisposed) accountWindow.Dispose();
                disconnectMenu.Dispose();
                if (indicator != null && !indicator.IsDisposed) indicator.Dispose();
                foreach (var panel in providerDetails) panel.Dispose();
                detailsMenu.Dispose();
                tray.Visible = false; tray.Dispose(); trayMenu.Dispose(); trayImage.Dispose();
            };
            // Keep the host alive even when startup suppresses the settings window.
            CreateHandle();
            ResizeIndicatorPreview();
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
                        ClaudeBridge.Install(); settings.ClaudeEnabled = true; settings.ClaudePrivateProfile = false;
                        connections.ConnectClaude(false);
                    }
                    else { settings.CodexEnabled = true; settings.CodexPrivateProfile = signIn; connections.ConnectCodex(signIn); }
                    usage.CoreEnabled[provider] = true;
                    Save(); ResizeIndicatorPreview(); TickLayout();
                });
            accountWindows[provider].ApplyTheme(theme);
            accountWindows[provider].Show(this); accountWindows[provider].Activate();
        }
        internal void OpenSettings()
        {
            startInTray = false;
            WindowState = FormWindowState.Normal;
            Show();
            Activate();
        }
        internal void RequestExit() { Program.RecordLifecycle("Exit requested"); exiting = true; Close(); }
        private static void InvalidateLogoControls(Control root)
        {
            foreach (Control control in root.Controls)
            {
                if (!control.Visible) continue;
                if (control is ProviderMenuMark) control.Invalidate();
                if (control.HasChildren) InvalidateLogoControls(control);
            }
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
            indicator.MouseMove += delegate { UpdateHover(); };
            indicator.ContextRequested += delegate
            {
                details.Hide();
                if (!trayMenu.Visible) trayMenu.Show(Cursor.Position);
            };
            indicator.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                int provider = indicator.ProviderAt(e.Location);
                if (provider >= 0) ShowDetails(true, false, provider);
            };
            indicator.MouseEnter += delegate
            {
                int provider = IndicatorView.ProviderAt(indicator.PointToClient(Cursor.Position), indicator.ClientSize, settings.Style, settings.ShowWeekly, usage);
                if (provider >= 0) ShowDetails(false, false, provider);
            };
        }
        private void PauseIndicator(string reason)
        {
            RecordIndicatorState("Paused: " + reason);
            transientSince = DateTime.MinValue;
            if (indicator != null && !indicator.IsDisposed) indicator.Hide();
            foreach (var panel in optionalDetails) if (panel != null && panel.RequiresIndicator && !panel.Pinned) panel.Hide();
            if (details.RequiresIndicator && !details.Pinned) details.Hide();
            indicatorBounds = Rectangle.Empty;
            SetSettingsStatus(reason + (saveError == null ? "" : "\n" + saveError), true);
        }
        private void RecordIndicatorState(string state)
        {
            if (indicatorState == state) return;
            indicatorState = state;
            Program.RecordLifecycle("Indicator " + state.Replace('\n', ' '));
        }
        private void RefreshUpdateUi()
        {
            updateCheckButton.Enabled = !updateService.Busy;
            var available = updateService.Available;
            updateCheckButton.Text = available == null ? "Check for updates" : AppInfo.IsInstalled ? "Install v" + available.Version.ToString(3) : "Get update";
            updateStatus.Text = updateService.Message;
        }
        private void TickLayout()
        {
            if (connections != null)
            {
                updateService.Tick(settings.AutomaticUpdateChecks, settings.LastUpdateCheckUtc);
                RefreshUpdateUi();
                if (updateService.CheckedUtc > settings.LastUpdateCheckUtc) { settings.LastUpdateCheckUtc = updateService.CheckedUtc; Save(); }
                var available = updateService.Available;
                if (available != null && notifiedUpdate != available.Version.ToString(3))
                {
                    notifiedUpdate = available.Version.ToString(3);
                    tray.ShowBalloonTip(5000, L10n.T("Taskcode update"), L10n.T("Version " + notifiedUpdate + " is available. Open General to install."), ToolTipIcon.Info);
                }
                string installer = updateService.TakeInstaller();
                if (installer != null)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(installer, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /UPDATE=1") { UseShellExecute = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden });
                        RequestExit(); return;
                    }
                    catch (System.ComponentModel.Win32Exception) { updateService.LaunchFailed(); }
                }
                connections.Tick(usage);
                optionalConnections.Tick();
                UpdateAccountLabels();
                preview.AccessibleName = IndicatorView.LiveAccessibleText(usage, settings.ShowWeekly);
                preview.Invalidate();
            }
            usage.Tick(DateTime.UtcNow);
            preview.Usage = usage;
            int pulse = 0;
            int activityKey = CurrentActivity;
            LogoMotion.Update(usage, DateTime.UtcNow);
            bool animateLogos = LogoMotion.IsMoving && enabled.Checked;
            if (animateLogos && !animationTimer.Enabled) lastLogoFrame = SpeedArrows.Seconds;
            animationTimer.Enabled = animateLogos;
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
            if (usage.VisibleProviderCount == 0) { PauseIndicator("No providers enabled."); return; }
            var layout = watcher.Latest;
            if (layout == null) { PauseIndicator("Measuring available taskbar space..."); return; }
            // Slow or failed UI Automation reads are usually momentary. Keep the indicator where it
            // is for a short grace period instead of hiding it (it used to blink on and off).
            if (DateTime.UtcNow - layout.MeasuredAt > Placement.MeasurementLifetime) { if (!HoldIndicator(pulse, activityKey)) PauseIndicator("Waiting for a fresh taskbar measurement."); return; }
            if (layout.Error != null) { if (!HoldIndicator(pulse, activityKey)) PauseIndicator(layout.Error); return; }
            if (!Native.IsWindow(layout.Taskbar)) { PauseIndicator("Waiting for Explorer to recreate the taskbar."); return; }
            Native.Rect liveBar, liveTray;
            var liveTrayHandle = Native.FindWindowEx(layout.Taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
            if (!Native.GetWindowRect(layout.Taskbar, out liveBar) || liveBar.Bounds != layout.Bar || !Native.GetWindowRect(liveTrayHandle, out liveTray))
            { if (!HoldIndicator(pulse, activityKey)) PauseIndicator("Taskbar layout changed; measuring again."); return; }
            // Only the tray resized (a tray icon appeared, the clock changed width...): follow the live
            // tray edge right away. TryViewport below still refuses to cover taskbar buttons.
            var trayBounds = liveTray.Bounds;
            var screen = Screen.FromRectangle(layout.Bar);
            if (Rectangle.Intersect(screen.Bounds, layout.Bar).Height < layout.Bar.Height / 2)
            { PauseIndicator("Taskbar is hidden."); return; }
            float scale = Math.Max(96, layout.Dpi) / 96f;
            int width = (int)Math.Round(IndicatorView.LogicalWidth(settings.Style, settings.ShowWeekly, usage) * scale);
            int height = (int)Math.Round(IndicatorView.LogicalHeight * scale);
            Rectangle slot;
            if (!Placement.TryViewport(layout.Bar, trayBounds, layout.OccupiedRight, width, height, (int)Math.Round(8 * scale), out slot))
            { PauseIndicator("Not enough verified space before the system tray.\nTry Essential or reduce the number of taskbar buttons."); return; }
            if (indicator == null || indicator.IsDisposed || (attachedParent != IntPtr.Zero && !Native.IsWindow(indicator.Handle))) RecreateIndicator();
            indicator.ContentWidth = width;
            SyncIndicator(pulse, activityKey);
            IntPtr expected = settings.Mode == HostMode.Embedded ? layout.Taskbar : IntPtr.Zero;
            bool parentChanged = attachedParent != expected || Native.GetParent(indicator.Handle) != expected;
            if (parentChanged)
            {
                indicator.Hide();
                long nativeStyle = Native.GetStyle(indicator.Handle, Native.GwlStyle).ToInt64();
                Native.SetStyle(indicator.Handle, Native.GwlStyle, new IntPtr(expected == IntPtr.Zero ? (nativeStyle & ~Native.Child) | Native.Popup : (nativeStyle & ~Native.Popup) | Native.Child));
                Native.SetParent(indicator.Handle, expected);
                if (Native.GetParent(indicator.Handle) != expected)
                { PauseIndicator("Embedding failed. Select Overlay to test the fallback."); return; }
                attachedParent = expected;
            }
            if (!indicator.Place(slot, expected, parentChanged))
            { PauseIndicator("Windows rejected indicator placement."); return; }
            indicator.Show();
            if (!indicator.RenderAlpha())
            { PauseIndicator("Windows rejected the transparent surface. Try the other placement mode."); return; }
            indicatorBounds = indicator.RectangleToScreen(indicator.ClientRectangle);
            transientSince = DateTime.MinValue;
            RecordIndicatorState(expected == IntPtr.Zero ? "Overlay active" : "Embedded active");
            SetSettingsStatus((expected == IntPtr.Zero ? "Overlay active" : "Embedded child window active") + " · " + layout.Dpi + " DPI" + (saveError == null ? "" : "\n" + saveError), saveError != null);
            UpdateHover();
        }
        private static readonly TimeSpan TransientGrace = TimeSpan.FromSeconds(15);
        private DateTime transientSince = DateTime.MinValue;
        private void SyncIndicator(int pulse, int activityKey)
        {
            indicator.DisplayStyle = settings.Style;
            indicator.ShowWeekly = settings.ShowWeekly;
            indicator.UsageBarColors = settings.UsageBarColors;
            indicator.PulseKey = pulse;
            indicator.ActivityKey = activityKey;
            indicator.Usage = usage;
            indicator.AccessibleName = IndicatorView.LiveAccessibleText(usage, settings.ShowWeekly);
            indicator.Theme = theme;
        }
        // Keeps a visible indicator in its last verified place while the taskbar measurement catches up.
        private bool HoldIndicator(int pulse, int activityKey)
        {
            if (indicator == null || indicator.IsDisposed || !indicator.Visible || indicatorBounds.IsEmpty) return false;
            var now = DateTime.UtcNow;
            if (transientSince == DateTime.MinValue) transientSince = now;
            if (now - transientSince > TransientGrace) return false;
            SyncIndicator(pulse, activityKey);
            indicator.RenderAlpha();
            UpdateHover();
            return true;
        }
        private void UpdateHover()
        {
            var pointer = Cursor.Position;
            if (indicator != null && !indicator.IsDisposed)
            {
                bool readingDetails = false;
                foreach (var panel in providerDetails) readingDetails |= panel.Visible && panel.RequiresIndicator;
                foreach (var panel in optionalDetails) readingDetails |= panel != null && panel.Visible && panel.RequiresIndicator;
                indicator.ScrollPaused = trayMenu.Visible || readingDetails || (indicator.Visible && indicatorBounds.Contains(pointer));
            }
            if (settings.DetailsTrigger != DetailsTrigger.Hover || trayMenu.Visible) return;
            bool fromPreview = Visible && preview.Visible && preview.RectangleToScreen(preview.ClientRectangle).Contains(pointer);
            var bounds = fromPreview ? preview.RectangleToScreen(preview.ClientRectangle) : indicatorBounds;
            if (!fromPreview && (indicator == null || !indicator.Visible)) return;
            int provider = fromPreview ? IndicatorView.ProviderAt(new Point(pointer.X - bounds.X, pointer.Y - bounds.Y), bounds.Size, settings.Style, settings.ShowWeekly, usage) : indicator.ProviderAt(new Point(pointer.X - bounds.X, pointer.Y - bounds.Y));
            if (provider >= 0) ShowDetails(false, fromPreview, provider);
        }
        internal void OpenDetailsFromSettings(int provider)
        { ShowDetails(true, true, provider, providerDetailsButton.RectangleToScreen(providerDetailsButton.ClientRectangle)); }
        internal int VisibleDetailProvider
        { get { if (details.Visible) return details.ProviderIndex; for (int index = 0; index < optionalDetails.Length; index++) if (optionalDetails[index] != null && optionalDetails[index].Visible) return index + 2; return -1; } }
        internal int VisibleDetailCount
        { get { int count = 0; foreach (var panel in providerDetails) if (panel.Visible) count++; foreach (var panel in optionalDetails) if (panel != null && panel.Visible) count++; return count; } }
        internal bool DetailsHaveForeground
        {
            get { var foreground = Native.GetForegroundWindow(); foreach (var panel in providerDetails) if (panel.IsHandleCreated && panel.Handle == foreground) return true; foreach (var panel in optionalDetails) if (panel != null && panel.IsHandleCreated && panel.Handle == foreground) return true; return false; }
        }
        internal void ShowDetails(bool explicitOpen, bool fromPreview, int provider = 0, Rectangle? sourceAnchor = null)
        {
            if (!usage.ProviderEnabled(provider)) return;
            if (!explicitOpen && settings.DetailsTrigger != DetailsTrigger.Hover) return;
            if (!explicitOpen)
            {
                if (details.Visible && details.Pinned) return;
                foreach (var panel in optionalDetails) if (panel != null && panel.Visible && panel.Pinned) return;
            }
            if (provider >= 2) { ShowOptionalDetails(provider - 2, fromPreview, sourceAnchor, explicitOpen); return; }
            foreach (var panel in optionalDetails) if (panel != null) panel.Hide();
            // Native entry and frame-rate pointer tracking share this guard.
            if (provider < 0 || provider > 1) return;
            if (!explicitOpen && settings.DetailsTrigger != DetailsTrigger.Hover) return;
            bool alreadyShown = !explicitOpen && details.Visible && selectedProvider == provider;
            if (selectedProvider != provider)
            {
                details.Hide();
                selectedProvider = provider;
                details = providerDetails[provider];
            }
            Rectangle anchor = sourceAnchor ?? (fromPreview || indicatorBounds.IsEmpty ? preview.RectangleToScreen(preview.ClientRectangle) : indicatorBounds);
            if (!sourceAnchor.HasValue)
            {
                var providerAnchor = !fromPreview && indicator != null && !indicator.IsDisposed ? indicator.ProviderBounds(provider, indicator.PointToClient(Cursor.Position)) : IndicatorView.ProviderBounds(provider, anchor.Size, settings.Style, settings.ShowWeekly, usage);
                anchor = new Rectangle(anchor.X + providerAnchor.X, anchor.Y, providerAnchor.Width, anchor.Height);
            }
            // Already open for this provider: keep the card still, but follow the indicator if it
            // moved or scrolled, so the hover area never drifts away from the pointer.
            if (alreadyShown) { details.HoverAnchor = anchor; return; }
            var area = Screen.FromRectangle(anchor).WorkingArea;
            int x = Math.Max(area.Left, Math.Min(anchor.Right - details.Width, area.Right - details.Width));
            int y = anchor.Top - details.Height - 8;
            if (y < area.Top) y = Math.Min(area.Bottom - details.Height, anchor.Bottom + 8);
            details.Location = new Point(x, y);
            details.HoverAnchor = anchor;
            details.RequiresIndicator = !sourceAnchor.HasValue && !fromPreview && !indicatorBounds.IsEmpty;
            details.SetInteraction(explicitOpen);
            details.Show();
            // Show() does not raise a window that is already visible; keep the card above the taskbar and other topmost windows.
            Native.SetWindowPos(details.Handle, Native.TopMost, 0, 0, 0, 0, Native.NoMove | Native.NoSize | Native.NoActivate);
        }
    }
}
