using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal static class CoreMonitoringChecks
    {
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        internal static void Run(string directory)
        {
            Check(new Settings().ClaudeEnabled && new Settings().CodexEnabled, "Claude and Codex must remain enabled by default.");
            using (var connections = new OptionalProviderConnections(index => new OptionalSnapshot()))
            {
                var model = new UsageDemo { OptionalProviders = connections.Providers }; model.BeginLive();
                foreach (IndicatorStyle style in Enum.GetValues(typeof(IndicatorStyle)))
                foreach (bool weekly in new[] { false, true })
                for (int mask = 0; mask < 4; mask++)
                foreach (bool cursor in new[] { false, true })
                {
                    model.CoreEnabled[0] = (mask & 1) != 0; model.CoreEnabled[1] = (mask & 2) != 0; connections.Providers[0].Enabled = cursor;
                    int count = ((mask & 1) != 0 ? 1 : 0) + ((mask & 2) != 0 ? 1 : 0) + (cursor ? 1 : 0);
                    int width = IndicatorView.LogicalWidth(style, weekly, model);
                    Check(model.VisibleProviderCount == count && (count == 0) == (width == 0), "An empty provider selection must use no taskbar width.");
                    foreach (double dpi in new[] { 1.0, 1.5, 2.0 })
                    {
                        var size = new Size((int)Math.Round(width * dpi), (int)Math.Round(IndicatorView.LogicalHeight * dpi));
                        for (int provider = 0; provider < 2 + ProviderCatalog.Count; provider++)
                        {
                            var bounds = IndicatorView.ProviderBounds(provider, size, style, weekly, model);
                            Check(bounds.IsEmpty == !model.ProviderEnabled(provider), "Hidden providers must have no clickable slot.");
                            if (!bounds.IsEmpty) Check(IndicatorView.ProviderAt(new Point(bounds.Left + bounds.Width / 2, size.Height / 2), size, style, weekly, model) == provider, "Reflowing visible providers must retain provider identity at every DPI.");
                        }
                    }
                    string accessible = IndicatorView.LiveAccessibleText(model, weekly);
                    Check(accessible.Contains("Claude") == model.CoreEnabled[0] && accessible.Contains("Codex") == model.CoreEnabled[1], "Disabled core providers must also disappear from accessibility text.");
                    using (var bitmap = new Bitmap(Math.Max(1, width), IndicatorView.LogicalHeight))
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        IndicatorView.Draw(graphics, bitmap.Size, style, TaskbarTheme.Create(true), weekly, 0, model, 3, 1);
                        if (!weekly && cursor) bitmap.Save(Path.Combine(directory, "provider-visibility-" + style + "-" + mask + ".png"));
                    }
                }
                bool[] enabled = { true, true };
                using (var accounts = new OptionalProvidersWindow(connections, (index, active) => connections.SetEnabled(index, active), index => { }, new CoreProviderControls
                {
                    Enabled = index => enabled[index], Status = index => "Connected", Change = (index, active) => enabled[index] = active,
                    Connect = index => { }, Details = index => { }, Refresh = () => { }
                }))
                {
                    accounts.Location = new Point(-30000, -30000); accounts.Show();
                    var list = accounts.Controls.OfType<ListBox>().Single(); var toggle = accounts.Controls.OfType<CheckBox>().Single();
                    Check(list.Items.Count == 11 && list.Items[0].ToString() == "Claude" && list.Items[1].ToString() == "Codex", "Manage providers must include Claude and Codex in the same list.");
                    Check(toggle.Checked, "Claude must initially display enabled."); toggle.Checked = false; Check(!enabled[0], "The shared monitoring toggle must disable Claude.");
                    list.SelectedIndex = 1; Check(toggle.Checked, "Codex activation must be independent of Claude."); toggle.Checked = false; Check(!enabled[1], "The shared monitoring toggle must disable Codex.");
                    list.SelectedIndex = 0; toggle.Checked = true; Check(enabled[0] && !enabled[1], "Re-enabling Claude must preserve Codex's disabled preference.");
                    using (var bitmap = new Bitmap(accounts.Width, accounts.Height)) { accounts.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(directory, "manage-core-providers.png")); }
                }
            }
            using (var accounts = new AccountConnections { ClaudePrivateProfile = true, CodexPrivateProfile = true })
            {
                accounts.SetMonitoring(0, false); accounts.SetMonitoring(1, false);
                Check(!accounts.ClaudeEnabled && !accounts.CodexEnabled, "Disabling monitoring must stop both account readers.");
                accounts.SetMonitoring(0, true); accounts.SetMonitoring(1, true);
                Check(accounts.ClaudeEnabled && accounts.CodexEnabled && accounts.ClaudePrivateProfile && accounts.CodexPrivateProfile, "Re-enabling must preserve both selected account profiles.");
            }
        }
    }
}
