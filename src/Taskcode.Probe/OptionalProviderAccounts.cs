using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal sealed class CoreProviderControls
    {
        internal Func<int, bool> Enabled;
        internal Func<int, string> Status;
        internal Action<int, bool> Change;
        internal Action<int> Connect, Details;
        internal Action Refresh;
    }
    internal sealed class OptionalProvidersWindow : Form
    {
        private readonly Timer timer = new Timer { Interval = 1000 };
        private readonly OptionalProviderConnections connections;
        private readonly Action<int, bool> change;
        private readonly CoreProviderControls core;
        private readonly CheckBox monitoring = new MainWindow.SettingsToggle();
        private bool updating;
        private readonly ListBox providers = new ListBox();
        private readonly Label title = new Label(), help = new Label(), state = new Label(), loginState = new Label();
        private readonly Label keyPrompt = new Label();
        private readonly TextBox key = new TextBox { UseSystemPasswordChar = true };
        private readonly ComboBox region = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Button connect, disconnect, signIn, saveKey, forgetKey, setup, cancel;
        private Process login;
        private ProviderProcessJob loginJob;
        private int loginIndex;
        private DateTime loginDeadline;
        private int Selected { get { return Math.Max(0, providers.SelectedIndex) - (core == null ? 0 : 2); } }
        internal OptionalProvidersWindow(OptionalProviderConnections connections, Action<int, bool> change, Action<int> details, CoreProviderControls core = null)
        {
            this.connections = connections; this.change = change; this.core = core;
            Text = "Providers"; ClientSize = new Size(740, 512); FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f); BackColor = TaskbarTheme.Create(true).PreviewBackground; ForeColor = Color.WhiteSmoke;
            AddLabel(new Label(), "Claude and Codex start enabled. Other providers start disabled.", 18, 16, 704, 30);
            providers.Location = new Point(18, 55); providers.Size = new Size(178, 356); providers.BorderStyle = BorderStyle.FixedSingle;
            providers.BackColor = Color.FromArgb(39, 42, 47); providers.ForeColor = ForeColor; providers.ItemHeight = 27;
            if (core != null) providers.Items.AddRange(new object[] { "Claude", "Codex" });
            foreach (string name in ProviderCatalog.Names) providers.Items.Add(name); Controls.Add(providers);
            AddLabel(title, "", 214, 55, 506, 26); title.Font = new Font(Font.FontFamily, 12, FontStyle.Bold);
            AddLabel(help, "", 214, 92, 506, 66);
            AddLabel(state, "", 214, 160, 506, 26);
            monitoring.Text = "Enable monitoring"; monitoring.Location = new Point(214, 187); monitoring.Size = new Size(506, 26); monitoring.AccessibleName = "Enable selected provider";
            monitoring.CheckedChanged += delegate { if (!updating) ChangeSelected(monitoring.Checked); }; Controls.Add(monitoring);
            connect = AddButton("Connect", 214, 215, 115, () => ChangeSelected(true));
            signIn = AddButton("Sign in / Open app", 339, 215, 160, () => SignIn());
            AddButton("Details", 509, 215, 97, () => { if (Selected < 0) core.Details(Selected + 2); else details(Selected); });
            AddButton("Refresh usage", 616, 215, 104, () => { if (Selected < 0) core.Refresh(); else connections.Refresh(Selected); UpdateState(); });
            region.Items.AddRange(new object[] { new L10n.Choice("Z.ai"), new L10n.Choice("BigModel (China)") }); region.SelectedIndex = 0; region.Location = new Point(214, 258); region.Size = new Size(180, 25); Controls.Add(region);
            AddLabel(keyPrompt, "API key or token (optional)", 404, 261, 316, 24);
            key.Location = new Point(214, 292); key.Size = new Size(506, 25); key.MaxLength = 32768; key.AccessibleName = "Provider API key or token"; Controls.Add(key);
            saveKey = AddButton("Save key and connect", 214, 329, 180, () => SaveKey());
            forgetKey = AddButton("Forget saved key", 404, 329, 150, () => ForgetKey());
            setup = AddButton("Install Qoder connector", 214, 258, 220, () => InstallQoder());
            disconnect = AddButton("Disconnect", 214, 377, 116, () => ChangeSelected(false));
            cancel = AddButton("Cancel sign-in", 340, 377, 150, () => StopLogin());
            AddLabel(loginState, "", 214, 412, 506, 40);
            AddLabel(new Label(), "Disabling monitoring hides the provider and preserves its official login.", 18, 476, 704, 28);
            providers.SelectedIndexChanged += delegate { key.Clear(); UpdateState(); };
            providers.SelectedIndex = 0;
            timer.Tick += delegate { PollLogin(); UpdateState(); }; timer.Start();
            FormClosed += delegate { StopLogin(); timer.Dispose(); };
            L10n.Bind(this); UpdateState();
        }
        private void AddLabel(Label label, string text, int x, int y, int width, int height)
        { label.Text = text; label.Location = new Point(x, y); label.Size = new Size(width, height); Controls.Add(label); }
        private Button AddButton(string text, int x, int y, int width, Action action)
        {
            var button = new OptionalConnectionButton { Text = text, Location = new Point(x, y), Size = new Size(width, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(44, 48, 54), UseVisualStyleBackColor = false };
            button.Click += delegate { try { action(); } catch (Exception) { loginState.Text = L10n.T("Could not complete this action. Check the official app and retry."); } };
            Controls.Add(button); return button;
        }
        private void UpdateState()
        {
            int index = Selected;
            if (index < 0)
            {
                int selected = index + 2; bool active = core.Enabled(selected);
                title.Text = selected == 0 ? "Claude" : "Codex"; title.ForeColor = selected == 0 ? IndicatorView.Claude : IndicatorView.Codex;
                help.Text = L10n.T("Turn monitoring on or off without changing your official account login. Use Sign in to connect an account.");
                state.Text = L10n.T(active ? core.Status(selected) : "Monitoring disabled");
                connect.Enabled = !active; disconnect.Enabled = active;
                updating = true; monitoring.Checked = active; updating = false;
                key.Visible = keyPrompt.Visible = saveKey.Visible = forgetKey.Visible = region.Visible = setup.Visible = false;
                signIn.Enabled = true; cancel.Enabled = login != null; return;
            }
            var provider = connections.Providers[index];
            title.Text = provider.Name; title.ForeColor = provider.Color; help.Text = L10n.T(ProviderCatalog.Help[index]);
            state.Text = L10n.T(provider.Enabled ? provider.Snapshot.Status : "Monitoring disabled");
            connect.Enabled = !provider.Enabled; disconnect.Enabled = provider.Enabled;
            updating = true; monitoring.Checked = provider.Enabled; updating = false;
            bool acceptsKey = ProviderCatalog.AcceptsKey(index);
            key.Visible = keyPrompt.Visible = saveKey.Visible = forgetKey.Visible = acceptsKey; region.Visible = index == 2; setup.Visible = index == 7;
            signIn.Enabled = setup.Enabled = login == null; cancel.Enabled = login != null;
        }
        private void ChangeSelected(bool active)
        { if (Selected < 0) core.Change(Selected + 2, active); else change(Selected, active); UpdateState(); }
        private void SaveKey()
        {
            int index = Selected;
            ProviderCredentials.SaveKey(index, key.Text, index == 2 && region.SelectedIndex == 1); key.Clear();
            connections.CredentialsChanged(index); change(index, true); UpdateState();
            loginState.Text = L10n.T("Key saved with Windows user encryption. Reading usage...");
        }
        private void ForgetKey()
        {
            int index = Selected; ProviderCredentials.RemoveKey(index); key.Clear(); change(index, false);
            connections.CredentialsChanged(index); loginState.Text = L10n.T("Saved key removed. Official app logins were preserved."); UpdateState();
        }
        private void SignIn()
        {
            int index = Selected;
            if (index < 0) { core.Connect(index + 2); return; }
            string cli = index == 3 ? "grok" : index == 4 ? "gh" : index == 5 ? "opencode" : index == 7 ? "qodercli" : null;
            if (cli != null)
            {
                string executable = ProviderCredentials.FindCli(cli);
                if (executable == null && index == 7)
                {
                    string bundled = Path.Combine(ProviderSidecars.DirectoryFor("qoder-usage"), @"node_modules\@qoder-ai\qoder-agent-sdk\dist\_bundled\qodercli.exe");
                    if (File.Exists(bundled)) executable = bundled;
                }
                if (executable != null)
                {
                    string arguments = index == 4 ? "auth login --hostname github.com --web" : index == 5 ? "auth login" : "login";
                    StartOwned(executable, "& $env:TASKCODE_PROVIDER_CLI " + arguments + "; $result = $LASTEXITCODE; Start-Sleep -Seconds 2; exit $result", false); return;
                }
            }
            string app = FindApp(index);
            Process.Start(new ProcessStartInfo(app ?? ProviderCatalog.Websites[index]) { UseShellExecute = true });
            loginState.Text = L10n.T("Complete sign-in in the official app, then choose Connect or Refresh.");
        }
        private void InstallQoder()
        {
            string npm = ProviderCredentials.FindCli("npm"), node = ProviderCredentials.FindCli("node");
            if (npm == null || node == null) { Process.Start(new ProcessStartInfo("https://nodejs.org/en/download") { UseShellExecute = true }); loginState.Text = L10n.T("Install Node.js, restart Taskcode and try again."); return; }
            ProviderSidecars.Prepare("qoder-usage");
            const string script = "& $env:TASKCODE_PROVIDER_CLI install --prefix $env:TASKCODE_CONNECTOR_DIR --ignore-scripts '@qoder-ai/qoder-agent-sdk@1.0.59'; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }; $env:QODER_INSTALL_BUNDLED_CLI = '1'; & $env:TASKCODE_NODE (Join-Path $env:TASKCODE_CONNECTOR_DIR 'node_modules/@qoder-ai/qoder-agent-sdk/scripts/postinstall.cjs'); $result = $LASTEXITCODE; Start-Sleep -Seconds 2; exit $result";
            StartOwned(npm, script, true, node);
        }
        private void StartOwned(string executable, string script, bool setupAction, string node = null)
        {
            if (login != null) return;
            string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
            var info = new ProcessStartInfo(powershell, "-NoProfile -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script))) { UseShellExecute = false, CreateNoWindow = false };
            info.EnvironmentVariables["TASKCODE_PROVIDER_CLI"] = executable;
            info.EnvironmentVariables["TASKCODE_CONNECTOR_DIR"] = ProviderSidecars.DirectoryFor("qoder-usage");
            if (node != null) info.EnvironmentVariables["TASKCODE_NODE"] = node;
            login = Process.Start(info); loginIndex = Selected; loginDeadline = DateTime.UtcNow.AddMinutes(15);
            loginJob = new ProviderProcessJob(login);
            loginState.Text = L10n.T(setupAction ? "Finish the connector installation in the opened window." : "Complete sign-in in the opened official CLI window.");
            UpdateState();
        }
        private void PollLogin()
        {
            if (login == null) return;
            if (DateTime.UtcNow >= loginDeadline) { StopLogin(); loginState.Text = L10n.T("Sign-in timed out. Try again."); return; }
            if (!login.HasExited) return;
            bool success = login.ExitCode == 0; int index = loginIndex;
            StopLogin();
            if (success) { connections.CredentialsChanged(index); change(index, true); }
            loginState.Text = L10n.T(success ? "Official client finished. Checking the account..." : "The official client did not finish successfully. Try again.");
        }
        private void StopLogin()
        {
            if (loginJob != null) { loginJob.Dispose(); loginJob = null; }
            if (login != null) { if (!login.HasExited) try { login.Kill(); } catch (InvalidOperationException) { } login.Dispose(); login = null; }
            UpdateState();
        }
        internal static string FindApp(int index)
        {
            string name = index == 0 ? "Cursor" : index == 1 ? "Antigravity" : index == 6 ? "Command Code" : null;
            if (name == null) return null;
            foreach (string root in new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
            {
                string path = Path.Combine(root, name, name + ".exe"); if (File.Exists(path)) return path;
            }
            return null;
        }
    }
    internal sealed class OptionalConnectionButton : Button
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            var palette = TaskbarTheme.Create(true);
            e.Graphics.Clear(Enabled ? Color.FromArgb(44, 48, 54) : palette.PreviewBackground);
            using (var border = new Pen(Enabled ? Color.FromArgb(105, 112, 124) : palette.Divider)) e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? palette.Text : palette.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4));
        }
    }
}
