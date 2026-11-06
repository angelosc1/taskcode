using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Taskcode.Probe
{
    // Read-only protocol adapted from Codenotch (MIT). No tokens leave this adapter.
    internal static class CursorUsage
    {
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open_v2(byte[] path, out IntPtr database, int flags, IntPtr vfs);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_prepare_v2(IntPtr database, byte[] query, int length, out IntPtr statement, IntPtr tail);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_step(IntPtr statement);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_column_text(IntPtr statement, int column);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_column_bytes(IntPtr statement, int column);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_finalize(IntPtr statement);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close(IntPtr database);
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_busy_timeout(IntPtr database, int milliseconds);
        private static byte[] Utf8(string text) { return System.Text.Encoding.UTF8.GetBytes(text + "\0"); }
        private static string Column(IntPtr statement, int index)
        {
            int length = sqlite3_column_bytes(statement, index);
            if (length <= 0 || length > 65536) return null;
            byte[] bytes = new byte[length]; Marshal.Copy(sqlite3_column_text(statement, index), bytes, 0, length);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        internal static Dictionary<string, string> ReadSession(string path)
        { return ReadValues(path, "SELECT key,value FROM ItemTable WHERE key IN ('cursorAuth/accessToken','cursorAuth/stripeMembershipAuthId','cursorAuth/cachedEmail')"); }
        internal static Dictionary<string, string> ReadValues(string path, string query)
        {
            if (!File.Exists(path)) return new Dictionary<string, string>();
            // Normal read-only mode includes the editor's WAL. Immutable is only a fallback.
            try { return ReadStore(path, query, false); }
            catch (IOException) { return ReadStore(path, query, true); }
        }
        private static Dictionary<string, string> ReadStore(string path, string query, bool immutable)
        {
            IntPtr database = IntPtr.Zero, statement = IntPtr.Zero;
            var result = new Dictionary<string, string>();
            try
            {
                string source = immutable ? new Uri(Path.GetFullPath(path)).AbsoluteUri + "?immutable=1" : path;
                if (sqlite3_open_v2(Utf8(source), out database, 1 | (immutable ? 64 : 0), IntPtr.Zero) != 0) throw new IOException();
                sqlite3_busy_timeout(database, 100);
                if (sqlite3_prepare_v2(database, Utf8(query), -1, out statement, IntPtr.Zero) != 0) throw new IOException();
                int step;
                while ((step = sqlite3_step(statement)) == 100) { string key = Column(statement, 0), value = Column(statement, 1); if (key != null && value != null && !result.ContainsKey(key)) result[key] = value; }
                if (step != 101) throw new IOException();
                return result;
            }
            finally { if (statement != IntPtr.Zero) sqlite3_finalize(statement); if (database != IntPtr.Zero) sqlite3_close(database); }
        }
        internal static OptionalSnapshot Read()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Cursor\User\globalStorage\state.vscdb");
            var session = ReadSession(path);
            string token, authId, email;
            if (!session.TryGetValue("cursorAuth/accessToken", out token) || !session.TryGetValue("cursorAuth/stripeMembershipAuthId", out authId) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(authId))
                return new OptionalSnapshot { Status = "Open Cursor and sign in, then retry.", Failed = true };
            var request = OptionalHttp.Request("https://cursor.com/api/usage-summary");
            request.Headers["Cookie"] = "WorkosCursorSessionToken=" + authId + "::" + token;
            var snapshot = Parse(OptionalHttp.Read(request), DateTime.UtcNow);
            if (session.TryGetValue("cursorAuth/cachedEmail", out email)) snapshot.Identity = email;
            return snapshot;
        }
        internal static OptionalSnapshot Parse(object data, DateTime now)
        {
            var result = new OptionalSnapshot { ObservedUtc = now, Status = "Connected via Cursor" };
            var usage = AccountJson.Get(data, "individualUsage"); var plan = AccountJson.Get(usage, "plan");
            DateTime? start = OptionalHttp.Date(AccountJson.Get(data, "billingCycleStart")), end = OptionalHttp.Date(AccountJson.Get(data, "billingCycleEnd"));
            if (start.HasValue && end.HasValue && start >= end) start = null;
            Add(result, "Included usage", AccountJson.Number(AccountJson.Get(plan, "totalPercentUsed")), start, end);
            var api = AccountJson.Number(AccountJson.Get(plan, "apiPercentUsed"));
            if (api > 0) Add(result, "API usage", api, start, end);
            var demand = AccountJson.Get(usage, "onDemand");
            var limit = AccountJson.Number(AccountJson.Get(demand, "limit")); var used = AccountJson.Number(AccountJson.Get(demand, "used"));
            if (Equals(AccountJson.Get(demand, "enabled"), true) && limit > 0 && used >= 0) Add(result, "On demand", used / limit * 100, start, end);
            if (result.Quotas.Count == 0) result.Status = Equals(AccountJson.Get(data, "isUnlimited"), true) ? "Unlimited plan. No quota to display." : "Connected. No usage allowance reported by Cursor.";
            return result;
        }
        private static void Add(OptionalSnapshot snapshot, string name, double? used, DateTime? start, DateTime? end)
        {
            if (used.HasValue && used >= 0) snapshot.Quotas.Add(new OptionalQuota { Name = name, Used = Math.Min(100, used.Value), StartUtc = start, ResetUtc = end });
        }
    }
}
