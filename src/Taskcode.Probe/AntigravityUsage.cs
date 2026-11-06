using System;
using System.Collections.Generic;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Taskcode.Probe
{
    // Read-only local bridge protocol adapted from Codenotch (MIT).
    internal static class AntigravityUsage
    {
        [DllImport("iphlpapi.dll")] private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order, int family, int tableClass, uint reserved);
        internal static string Csrf(string command)
        {
            var match = Regex.Match(command ?? "", "(?:^|\\s)--csrf_token(?:=|\\s+)(?:\"([^\"]+)\"|([^\\s]+))", RegexOptions.CultureInvariant);
            return match.Success ? match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value : null;
        }
        private static IEnumerable<int> Ports(int pid)
        {
            int size = 0; GetExtendedTcpTable(IntPtr.Zero, ref size, true, 2, 3, 0);
            if (size <= 0 || size > 16777216) yield break;
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (GetExtendedTcpTable(buffer, ref size, true, 2, 3, 0) != 0) yield break;
                int count = Marshal.ReadInt32(buffer);
                for (int index = 0; index < count && 4 + (index + 1) * 24 <= size; index++)
                {
                    IntPtr row = IntPtr.Add(buffer, 4 + index * 24);
                    if (Marshal.ReadInt32(row) != 2 || Marshal.ReadInt32(row, 20) != pid) continue;
                    uint address = unchecked((uint)Marshal.ReadInt32(row, 4));
                    if (address != 0 && address != 0x0100007f) continue;
                    int port = (ushort)IPAddress.NetworkToHostOrder(Marshal.ReadInt16(row, 8));
                    if (port > 0) yield return port;
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        internal static OptionalSnapshot Read()
        {
            using (var query = new ManagementObjectSearcher("SELECT ProcessId,CommandLine FROM Win32_Process WHERE Name LIKE '%language_server%' OR Name = 'agy.exe'"))
            using (var processes = query.Get())
            {
                foreach (ManagementObject process in processes)
                using (process)
                {
                    string csrf = Csrf(process["CommandLine"] as string);
                    if (string.IsNullOrWhiteSpace(csrf)) continue;
                    int pid = Convert.ToInt32(process["ProcessId"]);
                    int attempts = 0;
                    foreach (int port in Ports(pid))
                    {
                        if (++attempts > 4) break;
                        var request = OptionalHttp.Request("https://127.0.0.1:" + port + "/exa.language_server_pb.LanguageServerService/RetrieveUserQuotaSummary");
                        request.Proxy = null; request.Timeout = request.ReadWriteTimeout = 2000;
                        // The exception is per request and restricted to this loopback endpoint.
                        request.ServerCertificateValidationCallback = delegate(object sender, System.Security.Cryptography.X509Certificates.X509Certificate cert, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors errors)
                        { var source = sender as HttpWebRequest; return source != null && source.RequestUri.Host == "127.0.0.1" && source.RequestUri.Port == port; };
                        request.Headers["x-codeium-csrf-token"] = csrf;
                        try
                        {
                            var result = Parse(OptionalHttp.Read(request, "{\"forceRefresh\":true}"), DateTime.UtcNow);
                            if (result.Quotas.Count > 0) return result;
                        }
                        catch (WebException error) { if (error.Response != null) error.Response.Dispose(); }
                    }
                }
            }
            return new OptionalSnapshot { Failed = true, Status = "Open Antigravity and sign in to read its local quotas." };
        }
        internal static OptionalSnapshot Parse(object data, DateTime now)
        {
            var result = new OptionalSnapshot { ObservedUtc = now, Status = "Connected via Antigravity" };
            foreach (var group in OptionalHttp.Items(AccountJson.Get(AccountJson.Get(data, "response"), "groups")))
            {
                var quotas = new List<OptionalQuota>();
                string groupName = AccountJson.Get(group, "displayName") as string ?? "Usage";
                foreach (var bucket in OptionalHttp.Items(AccountJson.Get(group, "buckets")))
                {
                    var remaining = AccountJson.Number(AccountJson.Get(bucket, "remainingFraction"));
                    if (!remaining.HasValue || remaining < 0 || remaining > 1) continue;
                    string id = AccountJson.Get(bucket, "bucketId") as string ?? "";
                    string label = AccountJson.Get(bucket, "displayName") as string ?? id;
                    string cadence = (id + " " + label).ToLowerInvariant();
                    bool weekly = cadence.Contains("weekly"), hourly = cadence.Contains("5h") || cadence.Contains("five hour") || cadence.Contains("hourly");
                    var reset = OptionalHttp.Date(AccountJson.Get(bucket, "resetTime"));
                    quotas.Add(new OptionalQuota { Name = groupName + " / " + (weekly ? "Weekly" : hourly ? "5 hours" : label), Used = (1 - remaining.Value) * 100,
                        ResetUtc = reset, StartUtc = reset.HasValue && (weekly || hourly) ? (DateTime?)reset.Value.AddHours(weekly ? -168 : -5) : null });
                }
                quotas.Sort((a, b) => (a.Name.EndsWith("5 hours", StringComparison.Ordinal) ? 0 : 1).CompareTo(b.Name.EndsWith("5 hours", StringComparison.Ordinal) ? 0 : 1));
                result.Quotas.AddRange(quotas);
            }
            if (result.Quotas.Count == 0) result.Status = "Connected. No usage allowance reported by Antigravity.";
            return result;
        }
    }
}
