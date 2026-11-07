using System;
using System.Drawing;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal sealed class AccountConnectionWindow : Form
    {
        private readonly Timer timer = new Timer { Interval = 300 };
        private readonly Label progress = new Label(), identity = new Label();
        private readonly Button signIn = new Button(), cancel = new Button(), install = new Button(), local = new Button(), reopen = new Button();
        private readonly AccountConnections connections;
        private readonly UsageDemo usage;
        private readonly int provider;
        internal AccountConnectionWindow(int provider, AccountConnections connections, UsageDemo usage, Action<bool> connect)
        {
            this.provider = provider; this.connections = connections; this.usage = usage;
            string name = provider == 0 ? "Claude" : "Codex";
            Text = "Taskcode - Connect " + name; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(540, 432); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
            BackColor = Color.FromArgb(25, 28, 33); ForeColor = Color.FromArgb(237, 240, 244); Font = new Font("Segoe UI", 9);
            Controls.Add(new Label { Text = "Connect " + name, Location = new Point(22, 20), Size = new Size(496, 36), Font = new Font("Segoe UI", 14, FontStyle.Bold) });
            Controls.Add(new Label { Text = provider == 0 ? "Taskcode reads the account you already use in Claude Code, so it stays signed in automatically. If Claude Code is not signed in yet, run claude in a terminal first." :"Sign in with ChatGPT in your browser. You can choose your account without replacing your regular Codex CLI login.", Location = new Point(22, 67), Size = new Size(496, 62) });
            progress.Location = new Point(22, 141); progress.Size = new Size(496, 66); progress.ForeColor = provider == 0 ? IndicatorView.Claude : IndicatorView.Codex; Controls.Add(progress);
            identity.Location = new Point(22, 212); identity.Size = new Size(496, 28); identity.AutoEllipsis = true; Controls.Add(identity);
            signIn.Text = provider == 0 ? "Sign in with Claude" : "Sign in with ChatGPT"; signIn.Location = new Point(22, 254); signIn.Size = new Size(238, 36);
            local.Text = "Use existing CLI account"; local.Location = new Point(276, 254); local.Size = new Size(242, 36);
            // Claude only uses the regular CLI account: a separate Taskcode profile is never refreshed, so its token expires.
            if (provider == 0) { signIn.Visible = false; local.Location = new Point(22, 254); local.Size = new Size(496, 36); }
            install.Text = "Install official CLI"; install.Location = new Point(22, 305); install.Size = new Size(238, 34);
            reopen.Text = "Open sign-in page again"; reopen.Location = new Point(276, 305); reopen.Size = new Size(242, 34); reopen.Visible = provider == 1;
            cancel.Text = "Cancel sign-in"; cancel.Location = new Point(22, 370); cancel.Size = new Size(180, 34);
            var close = new Button { Text = "Close", Location = new Point(378, 370), Size = new Size(140, 34) };
            Controls.AddRange(new Control[] { signIn, local, install, reopen, cancel, close });
            signIn.Click += delegate { TryAction(() => connect(true)); RefreshState(); };
            local.Click += delegate { TryAction(() => connect(false)); RefreshState(); };
            install.Click += delegate { TryAction(() => connections.ClaudeAuth.Install(name, success => connections.Refresh())); RefreshState(); };
            reopen.Click += delegate { if (connections.CodexAuthUrl != null && connections.OpenLogin != null) connections.OpenLogin(connections.CodexAuthUrl); };
            cancel.Click += delegate { if (provider == 0 || connections.ClaudeAuth.Busy) connections.ClaudeAuth.Cancel(); else connections.CancelCodex(); RefreshState(); };
            close.Click += delegate { Close(); };
            timer.Tick += delegate { RefreshState(); }; Shown += delegate { timer.Start(); RefreshState(); };
            FormClosed += delegate { timer.Dispose(); }; ApplyTheme(TaskbarTheme.Read()); RefreshState(); L10n.Bind(this);
        }
        internal void ApplyTheme(TaskbarTheme palette)
        {
            palette = TaskbarTheme.AppPalette(palette);
            BackColor = palette.PreviewBackground; ForeColor = palette.Text;
            foreach (Control control in Controls)
            {
                var button = control as Button;
                if (button == null) continue;
                button.FlatStyle = FlatStyle.Flat; button.UseVisualStyleBackColor = false; button.FlatAppearance.BorderSize = 0; button.Tag = "action";
            }
            MainWindow.ApplyMenuColors(this, palette);
            progress.ForeColor = provider == 0 ? palette.Claude : palette.Codex;
            identity.ForeColor = palette.Muted;
        }
        private void TryAction(Action action)
        {
            try { action(); }
            catch (Exception) { MessageBox.Show(this, L10n.T("Could not start sign-in. Check the CLI installation and try again."), "Taskcode", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        }
        private void RefreshState()
        {
            bool busy = connections.ClaudeAuth.Busy || provider == 1 && connections.CodexSigningIn;
            signIn.Enabled = local.Enabled = install.Enabled = !busy; cancel.Enabled = busy;
            reopen.Enabled = provider == 1 && connections.CodexSigningIn && connections.CodexAuthUrl != null;
            string auth = provider == 0 || connections.ClaudeAuth.Busy ? connections.ClaudeAuth.Message : connections.CodexAuthMessage;
            progress.Text = auth + "\n" + usage.AccountStatus[provider];
            identity.Text = string.IsNullOrWhiteSpace(usage.AccountIdentity[provider]) ? "" : "Account: " + usage.AccountIdentity[provider];
        }
    }
}
