using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace Taskcode.Probe
{
    internal sealed class AccountConnections : IDisposable
    {
        private readonly CodexAccountClient codex = new CodexAccountClient();
        private readonly ConcurrentQueue<Action<UsageDemo>> updates = new ConcurrentQueue<Action<UsageDemo>>();
        private int busy, loginRequested;
        private readonly LocalActivity activity = new LocalActivity();
        private int activityBusy;
        private DateTime nextActivity;
        private volatile bool disposed;
        private DateTime nextPoll, nextCodex;
        internal bool CodexEnabled = true, ClaudeEnabled = true;
        internal Action<string> OpenLogin;
        internal void ConnectCodex() { CodexEnabled = true; Interlocked.Exchange(ref loginRequested, 1); nextPoll = nextCodex = DateTime.MinValue; }
        internal void Refresh() { nextPoll = nextCodex = DateTime.MinValue; }
        internal void Tick(UsageDemo usage)
        {
            Action<UsageDemo> apply;
            while (updates.TryDequeue(out apply)) apply(usage);
            if (!disposed && DateTime.UtcNow >= nextActivity && Interlocked.CompareExchange(ref activityBusy, 1, 0) == 0)
            {
                nextActivity = DateTime.UtcNow.AddMilliseconds(750);
                bool claude = ClaudeEnabled, codexOn = CodexEnabled;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        int key;
                        try { key = activity.Read(DateTime.UtcNow, claude, codexOn); } catch (Exception) { key = 0; }
                        updates.Enqueue(model => model.ActivityKey = key & (ClaudeEnabled ? 1 : 0) | key & (CodexEnabled ? 2 : 0));
                    }
                    finally { Interlocked.Exchange(ref activityBusy, 0); }
                });
            }
            if (disposed || DateTime.UtcNow < nextPoll || Interlocked.CompareExchange(ref busy, 1, 0) != 0) return;
            nextPoll = DateTime.UtcNow.AddSeconds(1);
            bool readCodex = CodexEnabled, readClaude = ClaudeEnabled;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    if (readClaude)
                    {
                        AccountSnapshot snapshot;
                        try { snapshot = ClaudeBridge.Read(); }
                        catch (Exception ex) { snapshot = new AccountSnapshot { Status = SafeError("Claude", ex), Failed = true }; }
                        updates.Enqueue(model => { if (ClaudeEnabled) model.Apply(0, snapshot); });
                    }
                    if (readCodex && (DateTime.UtcNow >= nextCodex || Interlocked.CompareExchange(ref loginRequested, 0, 0) != 0))
                    {
                        nextCodex = DateTime.UtcNow.AddSeconds(10);
                        AccountSnapshot snapshot;
                        try
                        {
                            bool connect = Interlocked.Exchange(ref loginRequested, 0) != 0;
                            snapshot = codex.Read();
                            if (connect && snapshot.Status.StartsWith("Not signed in", StringComparison.Ordinal))
                            {
                                string url = codex.BeginLogin();
                                updates.Enqueue(model => { if (!disposed && CodexEnabled && OpenLogin != null) OpenLogin(url); });
                                snapshot.Status = "Finish signing in in your browser.";
                            }
                        }
                        catch (Exception ex)
                        {
                            codex.Dispose(); nextCodex = DateTime.UtcNow.AddSeconds(60);
                            snapshot = new AccountSnapshot { Status = SafeError("Codex", ex), Failed = true };
                        }
                        updates.Enqueue(model => { if (CodexEnabled) model.Apply(1, snapshot); });
                    }
                    if (!readCodex) codex.Dispose();
                }
                finally { if (disposed) codex.Dispose(); Interlocked.Exchange(ref busy, 0); }
            });
        }
        private static string SafeError(string provider, Exception error)
        {
            if (error is TimeoutException) return provider + " connection timed out. Retrying automatically.";
            if (error is System.ComponentModel.Win32Exception || error is InvalidOperationException) return provider + " is unavailable. Check the CLI installation and sign-in.";
            if (error is UnauthorizedAccessException) return provider + " local data is not accessible.";
            return provider + " data is unavailable. Retrying automatically.";
        }
        public void Dispose()
        {
            disposed = true;
            if (Interlocked.CompareExchange(ref busy, 1, 0) == 0) { codex.Dispose(); Interlocked.Exchange(ref busy, 0); }
        }
    }
}
