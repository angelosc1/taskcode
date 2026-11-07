using System;
using System.Drawing;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal sealed partial class MainWindow
    {
        private void OpenOptionalProviders()
        {
            if (optionalAccounts == null || optionalAccounts.IsDisposed)
                optionalAccounts = new OptionalProvidersWindow(optionalConnections, delegate(int index, bool active)
                {
                    optionalConnections.SetEnabled(index, active);
                    settings.OptionalProvidersEnabled[index] = active;
                    if (!active && optionalDetails[index] != null) optionalDetails[index].Hide();
                    Save(); ResizeIndicatorPreview(); preview.Invalidate(); TickLayout();
                }, index => ShowOptionalDetails(index, true, optionalAccounts.Bounds), new CoreProviderControls
                {
                    Enabled = index => usage.CoreEnabled[index],
                    Status = index => usage.AccountStatus[index],
                    Change = SetCoreMonitoring,
                    Connect = OpenAccountConnection,
                    Details = index => ShowDetails(true, true, index, optionalAccounts.Bounds),
                    Refresh = () => { if (connections != null) connections.Refresh(); }
                });
            optionalAccounts.Show(this); optionalAccounts.Activate();
        }
        private void SetCoreMonitoring(int provider, bool active)
        {
            if (provider == 0) settings.ClaudeEnabled = active; else settings.CodexEnabled = active;
            usage.CoreEnabled[provider] = active;
            if (connections != null) connections.SetMonitoring(provider, active);
            usage.ClearProvider(provider, active ? "Connecting..." : "Monitoring disabled");
            if (!active) providerDetails[provider].Hide();
            Save(); ResizeIndicatorPreview(); preview.Invalidate(); TickLayout();
        }
        private void ShowOptionalDetails(int index, bool fromPreview, Rectangle? accountsAnchor = null, bool explicitOpen = true)
        {
            if (index < 0 || index >= optionalDetails.Length) return;
            foreach (var panel in providerDetails) panel.Hide();
            for (int other = 0; other < optionalDetails.Length; other++) if (other != index && optionalDetails[other] != null) optionalDetails[other].Hide();
            if (optionalDetails[index] == null || optionalDetails[index].IsDisposed) optionalDetails[index] = new OptionalProviderDetails(usage.OptionalProviders[index]);
            var view = optionalDetails[index];
            view.UsageBarColors = settings.UsageBarColors;
            if (view.Visible) { if (explicitOpen) view.SetInteraction(true); view.RefreshUsage(); return; }
            Rectangle anchor = accountsAnchor ?? (fromPreview ? preview.RectangleToScreen(preview.ClientRectangle) : indicatorBounds);
            if (!fromPreview && !accountsAnchor.HasValue && indicator != null)
            {
                var visible = indicator.ProviderBounds(index + 2);
                if (!visible.IsEmpty) anchor = new Rectangle(anchor.X + visible.X, anchor.Y, visible.Width, anchor.Height);
            }
            if (anchor.IsEmpty) anchor = Bounds;
            var area = Screen.FromRectangle(anchor).WorkingArea;
            view.Location = new Point(Math.Max(area.Left, Math.Min(anchor.Right - view.Width, area.Right - view.Width)), Math.Max(area.Top, anchor.Top - view.Height - 8));
            view.HoverAnchor = anchor;
            view.RequiresIndicator = !fromPreview && !accountsAnchor.HasValue && !indicatorBounds.IsEmpty;
            view.SetInteraction(explicitOpen);
            view.Show(); view.RefreshUsage();
        }
    }
}
