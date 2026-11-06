using System;
using System.IO;
using System.Reflection;

namespace Taskcode.Probe
{
    internal static class ProviderSidecars
    {
        internal static string DirectoryFor(string name) { return Path.Combine(AppInfo.DataDirectory, name); }
        internal static string Prepare(string name)
        {
            string directory = DirectoryFor(name); Directory.CreateDirectory(directory);
            string target = Path.Combine(directory, name + ".cjs");
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Taskcode." + name + ".cjs"))
            using (var reader = new StreamReader(stream))
            {
                string source = reader.ReadToEnd();
                if (!File.Exists(target) || File.ReadAllText(target) != source) ClaudeBridge.AtomicWrite(target, source);
            }
            return target;
        }
        internal static string Read(string name, int timeout)
        {
            string node = ProviderCredentials.FindCli("node"); if (node == null) return "{\"error\":\"node_missing\"}";
            string script = Prepare(name);
            return ProviderCredentials.RunHidden(node, "\"" + script + "\"", timeout, Path.GetDirectoryName(script));
        }
    }
    internal static class QoderUsage
    {
        internal static OptionalSnapshot Read()
        {
            object response = AccountJson.Parse(ProviderSidecars.Read("qoder-usage", 120000));
            string error = ProviderCredentials.Text(AccountJson.Get(response, "error"));
            if (error != null) return SubscriptionUsage.Missing(error == "node_missing" ? "Install Node.js to use this connector." : error == "sdk_missing" ? "Install the Qoder connector, then sign in to qodercli." : "Qoder could not read the account. Sign in to qodercli and retry.");
            return Parse(AccountJson.Get(response, "usage"), DateTime.UtcNow);
        }
        private static double? Percentage(object bucket, string cap = "total")
        {
            var used = AccountJson.Number(AccountJson.Get(bucket, "used")); var total = AccountJson.Number(AccountJson.Get(bucket, cap));
            if (used.HasValue && total > 0) return used / total * 100;
            var percent = AccountJson.Number(AccountJson.Get(bucket, "percentage"));
            return percent.HasValue ? (double?)(percent.Value > 1.5 ? percent.Value : percent.Value * 100) : null;
        }
        private static bool HasSignal(object bucket)
        { return AccountJson.Number(AccountJson.Get(bucket, "total")) > 0 || AccountJson.Number(AccountJson.Get(bucket, "used")) > 0 || AccountJson.Number(AccountJson.Get(bucket, "percentage")) > 0; }
        internal static OptionalSnapshot Parse(object usage, DateTime now)
        {
            var result = SubscriptionUsage.Connected(7, now); var quota = AccountJson.Get(usage, "userQuota");
            var reset = SubscriptionUsage.Timestamp(AccountJson.Get(usage, "expiresAt"));
            if (HasSignal(quota)) SubscriptionUsage.Add(result, "Plan usage", Percentage(quota), reset);
            else
            {
                var total = AccountJson.Number(AccountJson.Get(usage, "totalUsagePercentage"));
                if (total.HasValue) SubscriptionUsage.Add(result, "Total usage", total.Value > 1.5 ? total.Value : total.Value * 100, reset);
            }
            var addon = AccountJson.Get(usage, "addOnQuota"); if (HasSignal(addon)) SubscriptionUsage.Add(result, "Add-on credits", Percentage(addon));
            var org = AccountJson.Get(usage, "orgResourcePackage");
            if (!Equals(AccountJson.Get(org, "available"), false)) SubscriptionUsage.Add(result, "Organization package", Percentage(org, "cap"));
            result.Identity = ProviderCredentials.Text(AccountJson.Get(usage, "userType")) ?? "";
            if (result.Quotas.Count == 0) result.Status = "Connected. No metered quotas reported.";
            // The SDK's fresh daemon session is intentionally never counted as IDE usage.
            return result;
        }
    }
    internal static class DeepSeekUsage
    {
        internal static OptionalSnapshot Read()
        {
            object data = AccountJson.Parse(ProviderSidecars.Read("deepseek-usage", 30000));
            string error = ProviderCredentials.Text(AccountJson.Get(data, "error"));
            if (error != null) return SubscriptionUsage.Missing(error == "node_missing" || error == "zstd_missing" ? "Install Node.js 24+ to read DeepSeek Harness sessions." : error == "not_found" ? "No DeepSeek Harness sessions found in .dsh/sessions." : "DeepSeek local usage could not be read completely. Retry after the session is saved.");
            return Parse(data, DateTime.UtcNow);
        }
        internal static OptionalSnapshot Parse(object data, DateTime now)
        {
            var result = SubscriptionUsage.Connected(8, now);
            var tokens = AccountJson.Number(AccountJson.Get(data, "tokens"));
            if (!tokens.HasValue || tokens < 0) return SubscriptionUsage.Missing("No DeepSeek Harness token counts available.");
            result.CountText = tokens >= 1000000 ? (tokens.Value / 1000000).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "M" : tokens >= 1000 ? (tokens.Value / 1000).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "k" : tokens.Value.ToString("0");
            result.Metrics.Add("Tokens today: " + tokens.Value.ToString("N0"));
            result.Metrics.Add("Sessions today: " + (AccountJson.Number(AccountJson.Get(data, "sessions")) ?? 0).ToString("0"));
            result.Metrics.Add("Local counts. No official quota or cost estimate.");
            return result;
        }
    }
}
