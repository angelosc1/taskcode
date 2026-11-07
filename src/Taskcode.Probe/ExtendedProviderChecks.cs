using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal static class ExtendedProviderChecks
    {
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static object Json(string text) { return AccountJson.Parse(text); }
        internal static void Run(string directory, DateTime now)
        {
            CoreMonitoringChecks.Run(directory);
            Check(new Settings().OptionalProvidersEnabled.All(value => !value) && ProviderCatalog.Count == 9, "All nine additional Windows providers must be opt-in.");
            byte[] protectedKey = ProviderCredentials.Seal(2, "fixture-only", true);
            Check(ProviderCredentials.Unseal(2, protectedKey).Token == "fixture-only" && ProviderCredentials.Unseal(2, protectedKey).Host == "https://open.bigmodel.cn", "Encrypted explicit keys must preserve the selected provider region.");
            bool wrongProviderRejected = false;
            try { ProviderCredentials.Unseal(4, protectedKey); } catch (System.Security.Cryptography.CryptographicException) { wrongProviderRejected = true; }
            Check(wrongProviderRejected, "Encrypted keys must not be reusable under a different provider identity.");
            string node = ProviderCredentials.FindCli("node");
            if (node != null)
            {
                Check(ProviderCredentials.RunHidden(node, "-e \"process.stdout.write('fixture-only')\"") == "fixture-only", "Bounded helper transport must capture a successful response.");
                bool timedOut = false;
                try { ProviderCredentials.RunHidden(node, "-e \"setInterval(()=>{},1000)\"", 200); } catch (TimeoutException) { timedOut = true; }
                Check(timedOut, "An unresponsive helper must be terminated at its deadline.");
            }
            var glm = SubscriptionUsage.ParseGlm(Json("{\"success\":true,\"data\":{\"limits\":[{\"type\":\"CREDIT_LIMIT\",\"unit\":6,\"number\":1,\"percentage\":20},{\"type\":\"CREDIT_LIMIT\",\"unit\":3,\"number\":5,\"percentage\":10},{\"type\":\"TIME_LIMIT\",\"percentage\":30}]}}"), now);
            Check(glm.Quotas.Count == 3 && glm.Quotas[0].Name == "5 hours" && glm.Quotas[2].Name == "MCP (1 month)", "GLM must identify window duration independently of token/credit plan names.");
            Check(SubscriptionUsage.ParseGlm(Json("{\"success\":false,\"code\":401,\"data\":{\"limits\":[{\"percentage\":20}]}}"), now).Failed, "An HTTP-200 GLM error envelope must not publish quota.");
            Check(ProviderCredentials.TrustedGlmHost("api.z.ai") && !ProviderCredentials.TrustedGlmHost("api.z.ai.attacker.invalid"), "Borrowed GLM credentials must never be sent to an unrelated console.");
            var grok = SubscriptionUsage.ParseGrok(Json("{\"config\":{\"currentPeriod\":{\"type\":\"USAGE_PERIOD_TYPE_WEEKLY\",\"start\":\"2026-11-02T00:00:00Z\",\"end\":\"2026-11-09T00:00:00Z\"}}}"), now);
            Check(grok.Quotas.Count == 1 && grok.Quotas[0].Used == 0, "A provider-reported fresh Grok weekly period must display zero rather than unknown.");
            Check(ProviderCredentials.Grok(Json("{\"https://auth.x.ai.attacker.invalid::id\":{\"key\":\"foreign-fixture\"}}"), now) == null && ProviderCredentials.Grok(Json("{\"https://auth.x.ai::id\":{\"key\":\"fixture\",\"expires_at\":\"2020-01-01T00:00:00Z\"}}"), now) == null, "Grok must reject foreign issuers and expired sessions before any public request.");
            var copilot = SubscriptionUsage.ParseCopilot(Json("{\"copilot_plan\":\"individual\",\"quota_reset_date\":\"2026-12-01\",\"quota_snapshots\":{\"premium_interactions\":{\"entitlement\":300,\"remaining\":240},\"chat\":{\"entitlement\":50,\"used\":1,\"unlimited\":true},\"completions\":{\"entitlement\":0}}}"), now);
            Check(copilot.Quotas.Count == 1 && copilot.Quotas[0].Used == 20 && copilot.Quotas[0].StartUtc.Value.Month == 11, "Copilot must meter entitled quotas and ignore unlimited allowances.");
            Check(ProviderCredentials.OpenCodeEntry(Json("{\"type\":\"oauth\",\"access\":\"fixture\",\"metadata\":{\"server\":\"https://wrong.example/console\"}}")) == null, "OpenCode must not forward private server tokens to the public service.");
            Check(ProviderCredentials.GrokExpired(Json("{\"https://auth.x.ai::id\":{\"key\":\"fixture\",\"expires_at\":\"2020-01-01T00:00:00Z\"}}"), now) && !ProviderCredentials.GrokExpired(Json("{\"https://auth.x.ai.attacker.invalid::id\":{\"key\":\"fixture\",\"expires_at\":\"2020-01-01T00:00:00Z\"}}"), now), "Expired Grok sessions must be reported as expired, not as missing.");
            Check(ProviderCredentials.OpenCodeEntry(Json("{\"type\":\"oauth\",\"access\":\"fixture\",\"expires\":1577836800000}")).Expired && !ProviderCredentials.OpenCodeEntry(Json("{\"type\":\"oauth\",\"access\":\"fixture\",\"expires\":4070908800000}")).Expired, "Expired OpenCode sessions must be detected before any request.");
            var opencode = SubscriptionUsage.ParseOpenCode(Json("{\"usage\":{\"rolling\":{\"percent\":10},\"weekly\":{\"percent\":20},\"monthly\":{\"percent\":30,\"resetsAt\":\"2026-12-01T00:00:00Z\"}}}"), now);
            Check(opencode.Quotas.Count == 3 && opencode.Quotas[2].StartUtc.Value.Month == 11, "OpenCode must expose all three Go windows without inventing missing reset times.");
            var command = SubscriptionUsage.ParseCommandCode(Json("{\"totalCost\":25}"), Json("{\"credits\":{\"monthlyCredits\":75},\"windowLimits\":{\"fiveHour\":{\"cap\":10,\"used\":2},\"weekly\":{\"cap\":100,\"used\":40}}}"), Json("{\"data\":{\"currentPeriodStart\":\"2026-11-01T00:00:00Z\",\"currentPeriodEnd\":\"2026-12-01T00:00:00Z\"}}"), now);
            Check(command.Quotas.Count == 3 && command.Quotas[0].Used == 25 && command.Quotas[1].Used == 20, "Command Code must distinguish monthly credit spend from rolling limits.");
            var qoder = QoderUsage.Parse(Json("{\"userQuota\":{\"total\":100,\"used\":1,\"percentage\":1},\"addOnQuota\":{\"total\":10,\"used\":2},\"session\":{\"used\":999},\"expiresAt\":253402214400000}"), now);
            Check(qoder.Quotas.Count == 2 && qoder.Quotas[0].Used == 1 && qoder.Quotas[0].ResetUtc == null, "Qoder must prefer counts, ignore its fresh helper session and omit no-renewal sentinels.");
            Check(QoderUsage.Parse(Json("{\"userQuota\":{\"total\":0,\"used\":0,\"percentage\":0},\"addOnQuota\":{\"total\":10,\"used\":2}}"), now).Quotas.Count == 1, "Qoder must skip an empty plan bucket instead of displaying a misleading zero.");
            var deepseek = DeepSeekUsage.Parse(Json("{\"tokens\":12000,\"sessions\":3}"), now);
            Check(deepseek.CountText == "12k" && deepseek.Quotas.Count == 0 && deepseek.Metrics.Count == 3, "DeepSeek counts must not be presented as a provider quota or invented cost.");
            string originalLanguage = L10n.Language;
            try
            {
                L10n.SetLanguage("pt-BR");
                using (var connections = new OptionalProviderConnections(index => new OptionalSnapshot()))
                using (var accounts = new OptionalProvidersWindow(connections, (index, enabled) => connections.SetEnabled(index, enabled), index => { }))
                {
                    accounts.Location = new Point(-30000, -30000); accounts.Show();
                    var list = accounts.Controls.OfType<ListBox>().Single();
                    Check(list.Items.Count == 9, "The connection window must list all Windows providers.");
                    using (var board = new Bitmap(accounts.Width * 3, accounts.Height * 3))
                    using (var graphics = Graphics.FromImage(board))
                    {
                        for (int index = 0; index < 9; index++)
                        {
                            list.SelectedIndex = index;
                            foreach (Control control in accounts.Controls) Check(accounts.ClientRectangle.Contains(control.Bounds), "An optional connection control exceeds the window: " + control.Text);
                            using (var bitmap = new Bitmap(accounts.Width, accounts.Height))
                            { accounts.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); graphics.DrawImageUnscaled(bitmap, index % 3 * accounts.Width, index / 3 * accounts.Height); if (index == 2) bitmap.Save(Path.Combine(directory, "optional-connection-glm.png")); }
                        }
                        board.Save(Path.Combine(directory, "optional-accounts-pt-BR.png"));
                    }
                    var snapshots = new[] { glm, grok, copilot, opencode, command, qoder, deepseek };
                    foreach (var snapshot in snapshots) snapshot.ObservedUtc = DateTime.UtcNow;
                    using (var board = new Bitmap(380 * 7, 420))
                    using (var graphics = Graphics.FromImage(board))
                    {
                        for (int index = 0; index < snapshots.Length; index++)
                        {
                            var provider = connections.Providers[index + 2]; provider.Enabled = true; provider.Snapshot = snapshots[index];
                            using (var view = new OptionalProviderDetails(provider))
                            using (var bitmap = new Bitmap(380, 420))
                            { view.Location = new Point(-30000, -30000); view.Show(); view.RefreshUsage(); view.DrawToBitmap(bitmap, view.ClientRectangle); graphics.DrawImageUnscaled(bitmap, index * 380, 0); }
                        }
                        board.Save(Path.Combine(directory, "optional-details-pt-BR.png"));
                    }
                    var usage = new UsageDemo { OptionalProviders = connections.Providers };
                    foreach (var provider in connections.Providers) provider.Enabled = true;
                    foreach (IndicatorStyle style in Enum.GetValues(typeof(IndicatorStyle)))
                    {
                        var size = new Size(IndicatorView.LogicalWidth(style, false, usage), IndicatorView.LogicalHeight);
                        foreach (var provider in connections.Providers)
                        {
                            var bounds = IndicatorView.ProviderBounds(provider.Index + 2, size, style, false, usage);
                            Check(IndicatorView.ProviderAt(new Point(bounds.Left + bounds.Width / 2, size.Height / 2), size, style, false, usage) == provider.Index + 2, "All optional providers must route correctly when enabled together.");
                        }
                        using (var bitmap = new Bitmap(size.Width, size.Height))
                        using (var graphics = Graphics.FromImage(bitmap))
                        { IndicatorView.Draw(graphics, size, style, TaskbarTheme.Create(true), false, 0, usage); bitmap.Save(Path.Combine(directory, "all-providers-" + style + ".png")); }
                    }
                }
            }
            finally { L10n.SetLanguage(originalLanguage); }
        }
    }
}
