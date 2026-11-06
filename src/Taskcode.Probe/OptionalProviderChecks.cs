using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Taskcode.Probe
{
    internal static class OptionalProviderChecks
    {
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static object Json(string text) { return AccountJson.Parse(text); }
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open(byte[] name, out IntPtr database);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_exec(IntPtr database, byte[] sql, IntPtr callback, IntPtr argument, IntPtr error);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close(IntPtr database);
        private static byte[] Utf8(string text) { return System.Text.Encoding.UTF8.GetBytes(text + "\0"); }
        internal static void Run(string directory)
        {
            var now = new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
            Check(!new Settings().CursorEnabled && !new Settings().AntigravityEnabled, "Optional providers must default off.");
            var cursor = CursorUsage.Parse(Json("{\"billingCycleStart\":\"2026-11-01T00:00:00Z\",\"billingCycleEnd\":\"2026-12-01T00:00:00Z\",\"individualUsage\":{\"plan\":{\"totalPercentUsed\":10.5,\"apiPercentUsed\":20},\"onDemand\":{\"enabled\":true,\"used\":5,\"limit\":10}}}"), now);
            Check(cursor.Quotas.Count == 3 && cursor.Quotas[0].Used == 10.5 && cursor.Quotas[2].Used == 50 && cursor.Quotas[0].ResetUtc.Value.Month == 12, "Cursor must retain all quota categories and provider billing dates.");
            Check(CursorUsage.Parse(Json("{\"individualUsage\":{\"plan\":{\"totalPercentUsed\":0,\"used\":0,\"limit\":0}}}"), now).Quotas[0].Used == 0, "Cursor zero is a valid reading even without a monetary allowance.");
            Check(CursorUsage.Parse(Json("{}"), now).Quotas.Count == 0 && CursorUsage.Parse(Json("{\"isUnlimited\":true}"), now).Status.StartsWith("Unlimited", StringComparison.Ordinal), "Missing and unlimited quotas must not become zero use.");
            var antigravity = AntigravityUsage.Parse(Json("{\"response\":{\"groups\":[{\"displayName\":\"Gemini Models\",\"buckets\":[{\"bucketId\":\"gemini-weekly\",\"remainingFraction\":0.7},{\"bucketId\":\"gemini-5h\",\"remainingFraction\":0.8,\"resetTime\":\"2026-11-06T14:00:00Z\"}]},{\"displayName\":\"Claude and GPT\",\"buckets\":[{\"bucketId\":\"3p-weekly\",\"remainingFraction\":1},{\"bucketId\":\"3p-5h\",\"remainingFraction\":0.5},{\"remainingFraction\":2}]}]}}"), now);
            Check(antigravity.Quotas.Count == 4 && antigravity.Quotas[0].Name == "Gemini Models / 5 hours" && Math.Abs(antigravity.Quotas[0].Used - 20) < .0001 && antigravity.Quotas[2].Name == "Claude and GPT / 5 hours", "Antigravity must preserve model groups, invert remaining quota and order session before weekly.");
            Check(antigravity.Quotas[0].StartUtc.Value == now.AddHours(-3) && antigravity.Quotas[1].StartUtc == null, "Only provider reset timestamps may anchor Antigravity windows.");
            Check(AntigravityUsage.Csrf("agy.exe --hub --csrf_token=fixture") == "fixture" && AntigravityUsage.Csrf("language_server --csrf_token \"fixture value\"") == "fixture value" && AntigravityUsage.Csrf("agy.exe --not_csrf_token nope") == null, "Local discovery must parse exact CSRF flags and quoted values.");
            Check(OptionalHttp.RetryAt("172800", now) == now.AddDays(2) && OptionalHttp.RetryAt(now.AddDays(3).ToString("r"), now) == now.AddDays(3), "Manual refresh must honor the full Retry-After cooldown.");
            string path = Path.Combine(directory, "cursor-session-fixture.sqlite");
            IntPtr database;
            Check(sqlite3_open(Utf8(path), out database) == 0, "SQLite fixture must open.");
            try
            {
                Check(sqlite3_exec(database, Utf8("PRAGMA journal_mode=WAL; CREATE TABLE IF NOT EXISTS ItemTable(key TEXT PRIMARY KEY,value TEXT); INSERT OR REPLACE INTO ItemTable VALUES('cursorAuth/accessToken','fixture-only'),('cursorAuth/stripeMembershipAuthId','fixture-id');"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) == 0, "SQLite fixture must populate.");
                Check(CursorUsage.ReadSession(path)["cursorAuth/accessToken"] == "fixture-only", "Read-only Cursor access must see live WAL data while the writer remains open.");
            }
            finally { sqlite3_close(database); }
            using (var entered = new ManualResetEvent(false))
            using (var release = new ManualResetEvent(false))
            using (var completed = new ManualResetEvent(false))
            using (var connections = new OptionalProviderConnections(index => { entered.Set(); release.WaitOne(5000); completed.Set(); return cursor; }))
            {
                connections.Tick(); Check(!entered.WaitOne(50), "Disabled providers must never read credentials or poll.");
                connections.SetEnabled(0, true); connections.Tick(); Check(entered.WaitOne(5000), "Connecting must start a background quota read.");
                connections.SetEnabled(0, false); release.Set(); Check(completed.WaitOne(5000), "Fixture read must complete.");
                Check(SpinWait.SpinUntil(() => !connections.IsBusy(0), 5000), "Background reader must release its busy guard."); connections.Tick();
                Check(connections.Providers[0].Snapshot.Quotas.Count == 0 && !connections.Providers[0].Enabled, "Disconnect must discard in-flight results and forget readings.");
            }
            using (var connections = new OptionalProviderConnections())
            {
                var usage = new UsageDemo { OptionalProviders = connections.Providers };
                foreach (IndicatorStyle style in Enum.GetValues(typeof(IndicatorStyle)))
                foreach (bool weekly in new[] { false, true })
                {
                    int baseline = IndicatorView.LogicalWidth(style, weekly);
                    Check(IndicatorView.LogicalWidth(style, weekly, usage) == baseline, "Disabled optional providers must not change taskbar width.");
                    connections.SetEnabled(0, true); connections.SetEnabled(1, true);
                    connections.Providers[0].Snapshot = cursor; connections.Providers[1].Snapshot = antigravity;
                    var size = new Size(IndicatorView.LogicalWidth(style, weekly, usage), IndicatorView.LogicalHeight);
                    for (int provider = 0; provider < 4; provider++)
                    {
                        var bounds = IndicatorView.ProviderBounds(provider, size, style, weekly, usage);
                        Check(bounds.Width > 0 && IndicatorView.ProviderAt(new Point(bounds.Left + bounds.Width / 2, size.Height / 2), size, style, weekly, usage) == provider, "Every visible provider must route to its own details.");
                    }
                    using (var bitmap = new Bitmap(size.Width, size.Height))
                    using (var graphics = Graphics.FromImage(bitmap))
                    { IndicatorView.Draw(graphics, size, style, TaskbarTheme.Create(true), weekly, 0, usage); bitmap.Save(Path.Combine(directory, "optional-indicator-" + style + "-" + weekly + ".png")); }
                    connections.SetEnabled(0, false);
                    Check(IndicatorView.ProviderBounds(2, size, style, weekly, usage).IsEmpty && !IndicatorView.ProviderBounds(3, size, style, weekly, usage).IsEmpty, "Antigravity-only routing must retain its provider identity.");
                    connections.SetEnabled(1, false);
                }
                connections.Providers[0].Enabled = true; connections.Providers[0].Snapshot = cursor;
                Check(connections.Providers[0].DisplayUsed(now.AddMinutes(7)) == -1, "Expired observations must not appear as live quota.");
            }
            ExtendedProviderChecks.Run(directory, now);
        }
    }
}
