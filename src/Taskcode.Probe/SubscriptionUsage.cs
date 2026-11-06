using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace Taskcode.Probe
{
    // Wire formats adapted from Codenotch's MIT-licensed Windows providers.
    internal static class SubscriptionUsage
    {
        internal static DateTime? Timestamp(object value)
        {
            var iso = OptionalHttp.Date(value); if (iso.HasValue) return iso;
            double? number = AccountJson.Number(value); double parsed;
            if (!number.HasValue && value is string && double.TryParse((string)value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed)) number = parsed;
            if (!number.HasValue || double.IsNaN(number.Value) || double.IsInfinity(number.Value) || number <= 0) return null;
            double seconds = number > 10000000000 ? number.Value / 1000 : number.Value;
            return seconds < 4102444800 ? (DateTime?)AccountJson.Epoch.AddSeconds(seconds) : null;
        }
        internal static void Add(OptionalSnapshot result, string name, double? percent, DateTime? reset = null, DateTime? start = null)
        {
            if (!percent.HasValue || percent < 0 || double.IsNaN(percent.Value) || double.IsInfinity(percent.Value)) return;
            if (start.HasValue && reset.HasValue && start >= reset) start = null;
            result.Quotas.Add(new OptionalQuota { Name = name, Used = Math.Min(100, percent.Value), ResetUtc = reset, StartUtc = start });
        }
        internal static OptionalSnapshot Missing(string message) { return new OptionalSnapshot { Failed = true, Status = message }; }
        internal static OptionalSnapshot Connected(int index, DateTime now) { return new OptionalSnapshot { ObservedUtc = now, Status = "Connected via " + ProviderCatalog.Names[index] }; }
        private static object Get(string url, ProviderCredential credential, bool raw = false, bool commandCode = false, bool copilot = false, bool grok = false)
        {
            var request = OptionalHttp.Request(url);
            request.Headers[HttpRequestHeader.Authorization] = (raw ? "" : "Bearer ") + credential.Token;
            if (credential.Org != null) request.Headers["x-opencode-org-id"] = credential.Org;
            if (commandCode) { request.UserAgent = "command-code-desktop"; request.Headers["x-command-code-version"] = "desktop"; }
            if (copilot) request.Headers["X-GitHub-Api-Version"] = "2022-11-28";
            if (grok) request.Headers["X-XAI-Token-Auth"] = "xai-grok-cli";
            return OptionalHttp.Read(request);
        }
        internal static OptionalSnapshot Read(int index)
        {
            ProviderCredential credential;
            var now = DateTime.UtcNow;
            if (index == 2)
            {
                credential = ProviderCredentials.Glm();
                if (credential == null) return Missing("Connect a Z.ai Coding Plan key to read usage.");
                return ParseGlm(Get(credential.Host + "/api/monitor/usage/quota/limit", credential, true), now);
            }
            if (index == 3)
            {
                credential = ProviderCredentials.Grok(ProviderCredentials.ReadJson(Path.Combine(ProviderCredentials.Home, @".grok\auth.json")), now);
                if (credential == null) return Missing("Sign in with the official Grok CLI to read usage.");
                var result = ParseGrok(Get("https://cli-chat-proxy.grok.com/v1/billing?format=credits", credential, grok: true), now);
                result.Identity = credential.Identity; return result;
            }
            if (index == 4)
            {
                credential = ProviderCredentials.Copilot();
                if (credential == null) return Missing("Sign in with GitHub CLI or connect a GitHub token.");
                return ParseCopilot(Get("https://api.github.com/copilot_internal/user", credential, copilot: true), now);
            }
            if (index == 5)
            {
                credential = ProviderCredentials.OpenCode();
                if (credential == null) return Missing("Sign in to OpenCode or connect an OpenCode Go key.");
                try { return ParseOpenCode(Get(credential.OAuth ? "https://opencode.ai/inference/go/v1/usage" : "https://opencode.ai/zen/go/v1/usage", credential), now); }
                catch (WebException error)
                {
                    var response = error.Response as HttpWebResponse;
                    int code = response == null ? 0 : (int)response.StatusCode;
                    if (code != 401 && code != 403) throw;
                    if (response != null) response.Dispose();
                    if (code == 403) return new OptionalSnapshot { Status = "No OpenCode Go subscription on this account.", ObservedUtc = now };
                    if (credential.OAuth)
                    {
                        try { Get("https://opencode.ai/console/api/user", credential); return new OptionalSnapshot { Status = "No OpenCode Go subscription on this account.", ObservedUtc = now }; }
                        catch (WebException check) { if (check.Response != null) check.Response.Dispose(); }
                    }
                    return Missing("OpenCode rejected the session or Go key. Sign in again and check the Go plan.");
                }
            }
            if (index == 6)
            {
                credential = ProviderCredentials.Manual(6);
                if (credential == null)
                {
                    string token = ProviderCredentials.Text(Environment.GetEnvironmentVariable("COMMAND_CODE_API_KEY")) ?? ProviderCredentials.Text(AccountJson.Get(ProviderCredentials.ReadJson(Path.Combine(ProviderCredentials.Home, @".commandcode\auth.json")), "apiKey"));
                    if (token != null) credential = new ProviderCredential { Token = token };
                }
                if (credential == null) return Missing("Sign in to Command Code or connect a Command Code key.");
                const string root = "https://api.commandcode.ai/alpha";
                var who = Get(root + "/whoami", credential, commandCode: true);
                string org = ProviderCredentials.Text(AccountJson.Get(AccountJson.Get(who, "org"), "id"));
                string query = org == null ? "" : "?orgId=" + Uri.EscapeDataString(org);
                var credits = Get(root + "/billing/credits" + query, credential, commandCode: true);
                var subscription = Get(root + "/billing/subscriptions" + query, credential, commandCode: true);
                var sub = AccountJson.Get(subscription, "data") ?? subscription;
                string since = ProviderCredentials.Text(AccountJson.Get(sub, "currentPeriodStart"));
                var summary = Get(root + "/usage/summary" + query + (since == null ? "" : (query.Length == 0 ? "?" : "&") + "since=" + Uri.EscapeDataString(since)), credential, commandCode: true);
                var result = ParseCommandCode(summary, credits, subscription, now);
                result.Identity = ProviderCredentials.Text(AccountJson.Get(AccountJson.Get(who, "user"), "email")) ?? ""; return result;
            }
            throw new ArgumentOutOfRangeException("index");
        }
        internal static OptionalSnapshot ParseGlm(object data, DateTime now)
        {
            if (Equals(AccountJson.Get(data, "success"), false) || (AccountJson.Number(AccountJson.Get(data, "code")) >= 400)) return Missing("Z.ai rejected the key or quota request. Check the Coding Plan key.");
            var result = Connected(2, now);
            var session = new List<OptionalQuota>(); var other = new List<OptionalQuota>();
            foreach (var limit in OptionalHttp.Items(AccountJson.Get(AccountJson.Get(data, "data"), "limits")))
            {
                double? percent = AccountJson.Number(AccountJson.Get(limit, "percentage"));
                var unit = AccountJson.Number(AccountJson.Get(limit, "unit")); var number = AccountJson.Number(AccountJson.Get(limit, "number"));
                bool hourly = unit == 3 && number == 5, weekly = unit == 6 && number == 1;
                string type = ProviderCredentials.Text(AccountJson.Get(limit, "type"));
                var reset = Timestamp(AccountJson.Get(limit, "nextResetTime"));
                DateTime? start = reset.HasValue && number > 0 && (unit == 3 || unit == 6) ? (DateTime?)reset.Value.AddHours(-number.Value * (unit == 3 ? 1 : 168)) : null;
                Add(result, type == "TIME_LIMIT" ? "MCP (1 month)" : hourly ? "5 hours" : weekly ? "Weekly" : "Usage", percent, reset, start);
            }
            // Keep the headline on the session allowance independently of payload ordering.
            foreach (var quota in result.Quotas) if (quota.Name == "5 hours") session.Add(quota); else other.Add(quota);
            result.Quotas.Clear(); result.Quotas.AddRange(session); result.Quotas.AddRange(other);
            if (result.Quotas.Count == 0) result.Status = "Connected. No metered quotas reported.";
            return result;
        }
        internal static OptionalSnapshot ParseGrok(object data, DateTime now)
        {
            var result = Connected(3, now); var config = AccountJson.Get(data, "config"); var current = AccountJson.Get(config, "currentPeriod");
            var end = Timestamp(AccountJson.Get(current, "end")) ?? Timestamp(AccountJson.Get(config, "billingPeriodEnd"));
            var start = Timestamp(AccountJson.Get(current, "start")) ?? Timestamp(AccountJson.Get(config, "billingPeriodStart"));
            var total = AccountJson.Number(AccountJson.Get(config, "creditUsagePercent"));
            if (total.HasValue) Add(result, "Grok Build", total, end, start);
            else foreach (var product in OptionalHttp.Items(AccountJson.Get(config, "productUsage"))) Add(result, ProviderCredentials.Text(AccountJson.Get(product, "product")) ?? "Usage", AccountJson.Number(AccountJson.Get(product, "usagePercent")), end, start);
            string type = ProviderCredentials.Text(AccountJson.Get(current, "type")) ?? "";
            if (result.Quotas.Count == 0 && type.Contains("WEEKLY")) Add(result, "Weekly", 0, end, start);
            if (result.Quotas.Count == 0) result.Status = "Connected. No metered quotas reported.";
            return result;
        }
        internal static OptionalSnapshot ParseCopilot(object data, DateTime now)
        {
            var result = Connected(4, now); var quotas = AccountJson.Map(AccountJson.Get(data, "quota_snapshots"));
            var keys = new List<string> { "premium_interactions", "chat", "completions" }; foreach (string key in quotas.Keys) if (!keys.Contains(key)) keys.Add(key);
            foreach (string key in keys)
            {
                var quota = AccountJson.Get(quotas, key); if (Equals(AccountJson.Get(quota, "unlimited"), true)) continue;
                var limit = AccountJson.Number(AccountJson.Get(quota, "entitlement")); var used = AccountJson.Number(AccountJson.Get(quota, "used"));
                var remaining = AccountJson.Number(AccountJson.Get(quota, "remaining"));
                if (!used.HasValue && remaining.HasValue && limit.HasValue) used = Math.Max(0, limit.Value - remaining.Value);
                if (!(limit > 0) || !used.HasValue) continue;
                var reset = Timestamp(AccountJson.Get(quota, "reset_date")) ?? Timestamp(AccountJson.Get(quota, "reset_at")) ?? Timestamp(AccountJson.Get(quota, "resets_at")) ?? Timestamp(AccountJson.Get(data, "quota_reset_date"));
                DateTime? start = reset.HasValue && reset.Value.Day == 1 && reset.Value.TimeOfDay == TimeSpan.Zero ? (DateTime?)reset.Value.AddMonths(-1) : null;
                Add(result, key == "premium_interactions" ? "Premium requests" : key == "chat" ? "Chat requests" : key == "completions" ? "Completions" : key.Replace('_', ' '), used / limit * 100, reset, start);
            }
            result.Identity = ProviderCredentials.Text(AccountJson.Get(data, "login")) ?? ProviderCredentials.Text(AccountJson.Get(data, "copilot_plan")) ?? "";
            if (result.Quotas.Count == 0) result.Status = "Connected. No metered quotas reported.";
            return result;
        }
        internal static OptionalSnapshot ParseOpenCode(object data, DateTime now)
        {
            var result = Connected(5, now); var usage = AccountJson.Get(data, "usage");
            foreach (string key in new[] { "rolling", "weekly", "monthly" })
            {
                var window = AccountJson.Get(usage, key); var reset = Timestamp(AccountJson.Get(window, "resetsAt"));
                DateTime? start = reset.HasValue ? (DateTime?)(key == "rolling" ? reset.Value.AddHours(-5) : key == "weekly" ? reset.Value.AddDays(-7) : reset.Value.AddMonths(-1)) : null;
                Add(result, key == "rolling" ? "5 hours" : key == "weekly" ? "Weekly" : "Monthly", AccountJson.Number(AccountJson.Get(window, "percent")), reset, start);
            }
            if (result.Quotas.Count == 0) result.Status = "Connected. No metered quotas reported.";
            return result;
        }
        internal static OptionalSnapshot ParseCommandCode(object summary, object credits, object subscription, DateTime now)
        {
            var result = Connected(6, now); var sub = AccountJson.Get(subscription, "data") ?? subscription;
            var used = AccountJson.Number(AccountJson.Get(summary, "totalCost")); var remaining = AccountJson.Number(AccountJson.Get(AccountJson.Get(credits, "credits"), "monthlyCredits"));
            if (used >= 0 && remaining >= 0 && used + remaining > 0) Add(result, "Monthly", used / (used + remaining) * 100, Timestamp(AccountJson.Get(sub, "currentPeriodEnd")), Timestamp(AccountJson.Get(sub, "currentPeriodStart")));
            foreach (string key in new[] { "fiveHour", "weekly" })
            {
                var quota = AccountJson.Get(AccountJson.Get(credits, "windowLimits"), key); var cap = AccountJson.Number(AccountJson.Get(quota, "cap"));
                if (!(cap > 0)) continue;
                var reset = Timestamp(AccountJson.Get(quota, "resetAt"));
                Add(result, key == "fiveHour" ? "5 hours" : "Weekly", AccountJson.Number(AccountJson.Get(quota, "used")) / cap * 100, reset, reset.HasValue ? (DateTime?)reset.Value.AddHours(key == "fiveHour" ? -5 : -168) : null);
            }
            if (result.Quotas.Count == 0) result.Status = "Connected. No metered quotas reported.";
            return result;
        }
    }
}
