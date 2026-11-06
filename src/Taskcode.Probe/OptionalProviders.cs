using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;

namespace Taskcode.Probe
{
    internal sealed class OptionalQuota
    {
        internal string Name;
        internal double Used;
        internal DateTime? StartUtc, ResetUtc;
    }
    internal sealed class OptionalSnapshot
    {
        internal string Status = "Not connected", Identity = "";
        internal DateTime ObservedUtc;
        internal readonly List<OptionalQuota> Quotas = new List<OptionalQuota>();
        internal bool Failed;
        internal string CountText;
        internal readonly List<string> Metrics = new List<string>();
    }
    internal sealed class OptionalProvider
    {
        internal readonly int Index;
        internal readonly string Name;
        internal readonly string Id;
        internal readonly Color Color;
        internal bool Enabled;
        internal int Generation;
        internal OptionalSnapshot Snapshot = new OptionalSnapshot();
        internal readonly Dictionary<string, QuotaTrend> Trends = new Dictionary<string, QuotaTrend>();
        internal void Apply(OptionalSnapshot snapshot)
        {
            Snapshot = snapshot;
            if (snapshot.Failed) { Trends.Clear(); return; }
            var keep = new HashSet<string>();
            foreach (var quota in snapshot.Quotas)
            {
                if (keep.Count >= 32) break;
                keep.Add(quota.Name);
                QuotaTrend trend;
                if (!Trends.TryGetValue(quota.Name, out trend)) Trends[quota.Name] = trend = new QuotaTrend();
                trend.Observe(snapshot.ObservedUtc, quota.Used, QuotaTrend.PeriodKey(quota.StartUtc, quota.ResetUtc));
            }
            foreach (string name in new List<string>(Trends.Keys)) if (!keep.Contains(name)) Trends.Remove(name);
        }
        internal OptionalProvider(int index, string id, string name, Color color) { Index = index; Id = id; Name = name; Color = color; }
        internal int DisplayUsed(DateTime now)
        {
            if (!Enabled || Snapshot.Failed || Snapshot.Quotas.Count == 0 || now - Snapshot.ObservedUtc > TimeSpan.FromMinutes(6)) return -1;
            var quota = Snapshot.Quotas[0];
            return quota.ResetUtc.HasValue && quota.ResetUtc.Value <= now ? -1 : (int)Math.Round(quota.Used);
        }
        internal string DisplayText(DateTime now)
        {
            if (Enabled && !Snapshot.Failed && now - Snapshot.ObservedUtc <= TimeSpan.FromMinutes(6) && Snapshot.CountText != null) return Snapshot.CountText;
            int used = DisplayUsed(now); return used < 0 ? "--" : used + "%";
        }
    }
    internal sealed class OptionalProviderConnections : IDisposable
    {
        private readonly ConcurrentQueue<Action> updates = new ConcurrentQueue<Action>();
        private readonly int[] busy = new int[ProviderCatalog.Count];
        private readonly DateTime[] next = new DateTime[ProviderCatalog.Count];
        private readonly DateTime[] cooldown = new DateTime[ProviderCatalog.Count];
        private readonly bool[] cooldownLoaded = new bool[ProviderCatalog.Count];
        private readonly bool useCooldownStore;
        private volatile bool disposed;
        private readonly Func<int, OptionalSnapshot> read;
        internal readonly OptionalProvider[] Providers = ProviderCatalog.Create();
        internal OptionalProviderConnections(Func<int, OptionalSnapshot> read = null)
        { this.read = read ?? ProviderCatalog.Read; useCooldownStore = read == null; }
        internal bool IsBusy(int index) { return Interlocked.CompareExchange(ref busy[index], 0, 0) != 0; }
        internal void CredentialsChanged(int index) { next[index] = cooldown[index]; }
        internal void SetEnabled(int index, bool enabled)
        {
            var provider = Providers[index]; provider.Enabled = enabled; provider.Generation++;
            provider.Snapshot = new OptionalSnapshot { Status = enabled ? "Reading local account..." : "Monitoring disabled" };
            provider.Trends.Clear();
            // Preserve the attempt floor when reconnecting or toggling monitoring.
        }
        internal void Refresh(int index)
        {
            // Tick is also the manual path: neither path bypasses provider backoff.
            if (Providers[index].Enabled) Tick();
        }
        internal void Tick()
        {
            Action update; while (updates.TryDequeue(out update)) update();
            if (disposed) return;
            foreach (var provider in Providers)
            {
                int index = provider.Index;
                if (provider.Enabled && useCooldownStore && !cooldownLoaded[index])
                { next[index] = cooldown[index] = ProviderCooldowns.Read(index); cooldownLoaded[index] = true; }
                if (!provider.Enabled || DateTime.UtcNow < next[index] || Interlocked.CompareExchange(ref busy[index], 1, 0) != 0) continue;
                int generation = provider.Generation;
                next[index] = DateTime.UtcNow.AddMinutes(5);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    OptionalSnapshot result;
                    try { result = read(index); }
                    catch (WebException error)
                    {
                        using (var response = error.Response as HttpWebResponse)
                        {
                            int code = response == null ? 0 : (int)response.StatusCode;
                            if (code == 429)
                            {
                                next[index] = cooldown[index] = OptionalHttp.RetryAt(response.Headers["Retry-After"], DateTime.UtcNow);
                                if (useCooldownStore) ProviderCooldowns.Write(index, cooldown[index]);
                            }
                            result = new OptionalSnapshot { Failed = true, Status = code == 401 || code == 403 ? "Session rejected. Sign in again in the official app." : code == 429 ? "Usage checks are rate limited. Waiting before retrying." : "Connection failed. Retrying automatically." };
                        }
                    }
                    catch (Exception) { result = new OptionalSnapshot { Failed = true, Status = "Usage unavailable. Open the official app, sign in and retry." }; }
                    updates.Enqueue(delegate
                    {
                        if (disposed || !provider.Enabled || provider.Generation != generation) return;
                        provider.Apply(result);
                    });
                    Interlocked.Exchange(ref busy[index], 0);
                });
            }
        }
        public void Dispose() { disposed = true; }
    }
    internal static class OptionalHttp
    {
        internal static DateTime? Date(object value)
        {
            DateTime parsed;
            return value is string && DateTime.TryParse((string)value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed) ? (DateTime?)parsed : null;
        }
        internal static DateTime RetryAt(string retry, DateTime now)
        {
            double seconds;
            if (double.TryParse(retry, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && !double.IsNaN(seconds) && !double.IsInfinity(seconds))
                return now.AddSeconds(Math.Max(300, Math.Min((DateTime.MaxValue - now).TotalSeconds - 1, seconds)));
            var date = Date(retry); return date.HasValue && date.Value > now.AddMinutes(5) ? date.Value : now.AddMinutes(5);
        }
        internal static HttpWebRequest Request(string url)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.AllowAutoRedirect = false; request.Timeout = request.ReadWriteTimeout = 10000;
            request.UserAgent = "Taskcode/" + AppInfo.VersionText; request.Accept = "application/json";
            return request;
        }
        internal static object Read(HttpWebRequest request, string body = null)
        {
            if (body != null)
            {
                request.Method = "POST"; request.ContentType = "application/json";
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(body); request.ContentLength = bytes.Length;
                using (var output = request.GetRequestStream()) output.Write(bytes, 0, bytes.Length);
            }
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if ((int)response.StatusCode != 200) throw new InvalidDataException();
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    char[] buffer = new char[1048577]; int count = reader.ReadBlock(buffer, 0, buffer.Length);
                    if (count > 1048576) throw new InvalidDataException();
                    return AccountJson.Parse(new string(buffer, 0, count));
                }
            }
        }
        internal static IEnumerable<object> Items(object value)
        { var items = value as System.Collections.IEnumerable; if (items != null && !(value is string) && !(value is System.Collections.IDictionary)) foreach (object item in items) yield return item; }
    }
}
