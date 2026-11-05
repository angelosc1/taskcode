using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal static class SignInChecks
    {
        internal static void Run(string directory)
        {
            using (var connections = new AccountConnections())
            {
                for (int provider = 0; provider < 2; provider++)
                {
                    bool? requested = null;
                    var usage = new UsageDemo(); usage.BeginLive(); usage.ClearProvider(provider, "Not connected. Choose an account option below.");
                    using (var window = new AccountConnectionWindow(provider, connections, usage, value => requested = value) { StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000) })
                    {
                        window.Show();
                        Button local = null, signIn = null;
                        foreach (Control control in window.Controls)
                        {
                            var button = control as Button;
                            if (button == null) continue;
                            if (button.Text == "Use existing CLI account") local = button;
                            if (button.Text.StartsWith("Sign in with", StringComparison.Ordinal)) signIn = button;
                        }
                        Check(local != null && signIn != null && window.Visible, "Each provider must open a visible connection window with explicit account choices.");
                        local.PerformClick(); Check(requested == false, "Existing-account choice must route to local credentials.");
                        signIn.PerformClick(); Check(requested == true, "Sign-in choice must route to the browser/official CLI flow even if an existing account is present.");
                        using (var bitmap = new Bitmap(window.Width, window.Height))
                        { window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(directory, provider == 0 ? "claude-connect-check.png" : "codex-connect-check.png"), System.Drawing.Imaging.ImageFormat.Png); }
                        window.Close();
                    }
                }
            }
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
