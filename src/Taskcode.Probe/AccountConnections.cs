using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Taskcode.Probe
{
    internal sealed class AccountConnections : IDisposable
    {
        private readonly CodexAccountClient codex = new CodexAccountClient();
        private readonly ClaudeAccountClient claude = new ClaudeAccountClient();
        internal readonly ClaudeSignIn ClaudeAuth = new ClaudeSignIn();
        private readonly ConcurrentQueue<Action<UsageDemo>> updates = new ConcurrentQueue<Action<UsageDemo>>();
        private readonly LocalActivity activity = new LocalActivity();
        private int busy, activityBusy, codexRequest, claudeRefresh, codexGeneration, claudeGeneration;
        private volatile bool disposed;
        private DateTime nextPoll, nextCodex, nextActivity, loginDeadline;
        internal bool CodexEnabled = true, ClaudeEnabled = true;
        internal bool CodexPrivateProfile, ClaudePrivateProfile;
        internal volatile bool CodexSigningIn;
        internal string CodexAuthMessage = "Choose how to connect your ChatGPT account.";
        internal string CodexAuthUrl;
        internal Action<string> OpenLogin;
        internal void ConnectCodex(bool signIn = false)
        {
            CodexEnabled = true; CodexPrivateProfile = signIn; Interlocked.Increment(ref codexGeneration);
            CodexAuthUrl = null;
            CodexSigningIn = signIn; loginDeadline = DateTime.UtcNow.AddMinutes(15);
            CodexAuthMessage = signIn ? "Preparing browser sign-in..." : "Reading your existing Codex CLI account...";
            Interlocked.Exchange(ref codexRequest, signIn ? 2 : 1); nextPoll = nextCodex = DateTime.MinValue;
        }
        internal void ConnectClaude(bool signIn)
        {
            ClaudeEnabled = true; ClaudePrivateProfile = signIn; Interlocked.Increment(ref claudeGeneration);
            if (signIn) ClaudeAuth.Start(true, success => { Interlocked.Exchange(ref claudeRefresh, 1); });
            Interlocked.Exchange(ref claudeRefresh, 1); nextPoll = DateTime.MinValue;
        }
        internal void CancelCodex() { Interlocked.Exchange(ref codexRequest, 3); CodexSigningIn = false; CodexAuthUrl = null; CodexAuthMessage = "Sign-in cancelled."; nextPoll = nextCodex = DateTime.MinValue; }
        internal void Refresh() { Interlocked.Exchange(ref claudeRefresh, 1); nextPoll = nextCodex = DateTime.MinValue; }
        internal void Tick(UsageDemo usage)
        {
            Action<UsageDemo> apply;
            while (updates.TryDequeue(out apply)) apply(usage);
            if (disposed) return;
            if (DateTime.UtcNow >= nextActivity && Interlocked.CompareExchange(ref activityBusy, 1, 0) == 0)
            {
                nextActivity = DateTime.UtcNow.AddMilliseconds(750);
                bool claudeOn = ClaudeEnabled, codexOn = CodexEnabled;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        int key; try { key = activity.Read(DateTime.UtcNow, claudeOn, codexOn); } catch (Exception) { key = 0; }
                        updates.Enqueue(model => model.ActivityKey = key & (ClaudeEnabled ? 1 : 0) | key & (CodexEnabled ? 2 : 0));
                    }
                    finally { Interlocked.Exchange(ref activityBusy, 0); }
                });
            }
            if (CodexSigningIn && DateTime.UtcNow >= loginDeadline) CancelCodex();
            if (DateTime.UtcNow < nextPoll || Interlocked.CompareExchange(ref busy, 1, 0) != 0) return;
            nextPoll = DateTime.UtcNow.AddSeconds(1);
            bool readCodex = CodexEnabled, readClaude = ClaudeEnabled, privateCodex = CodexPrivateProfile, privateClaude = ClaudePrivateProfile, claudeActive = (usage.ActivityAt(DateTime.UtcNow) & 1) != 0;
            int codexVersion = codexGeneration, claudeVersion = claudeGeneration;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    int request = Interlocked.Exchange(ref codexRequest, 0);
                    if (request != 0) { privateCodex = CodexPrivateProfile; codexVersion = codexGeneration; }
                    if (readCodex && (request != 0 || DateTime.UtcNow >= nextCodex))
                    {
                        nextCodex = DateTime.UtcNow.AddSeconds(CodexSigningIn ? 2 : 10);
                        AccountSnapshot snapshot;
                        try
                        {
                            codex.UsePrivateProfile = privateCodex;
                            if (request == 2)
                            {
                                string url = codex.BeginLogin(); CodexAuthUrl = url;
                                updates.Enqueue(model => { if (!disposed && CodexEnabled && CodexSigningIn && codexVersion == codexGeneration && OpenLogin != null) OpenLogin(url); });
                                CodexAuthMessage = "Complete sign-in in your browser. This window will update automatically.";
                                snapshot = new AccountSnapshot { Status = "Waiting for browser sign-in..." };
                            }
                            else
                            {
                                if (request == 3) codex.CancelLogin();
                                snapshot = codex.Read();
                                if (CodexSigningIn && codex.LoginPending) snapshot = new AccountSnapshot { Status = "Waiting for browser sign-in..." };
                                else if (CodexSigningIn)
                                {
                                    CodexSigningIn = false;
                                    CodexAuthMessage = snapshot.SignedIn && !codex.LoginFailed ? "Signed in. Account connected." : "Sign-in did not complete. Try again.";
                                }
                                else if (snapshot.SignedIn) CodexAuthMessage = "Account connected.";
                            }
                        }
                        catch (Exception)
                        {
                            codex.Dispose(); nextCodex = DateTime.UtcNow.AddSeconds(60); CodexSigningIn = false;
                            CodexAuthMessage = CodexAccountClient.FindExecutable() == null ? "Codex CLI is missing. Click Install official CLI, then Sign in." : "Could not connect to Codex. Retry sign-in or check your network.";
                            snapshot = new AccountSnapshot { Status = CodexAuthMessage, Failed = true };
                        }
                        updates.Enqueue(model => { if (CodexEnabled && codexVersion == codexGeneration) model.Apply(1, snapshot); });
                    }
                    if (readClaude)
                    {
                        AccountSnapshot snapshot;
                        try
                        {
                            claude.UsePrivateProfile = privateClaude; claude.SessionActive = claudeActive;
                            if (Interlocked.Exchange(ref claudeRefresh, 0) != 0) claude.Refresh();
                            snapshot = ClaudeAuth.Busy ? new AccountSnapshot { Status = "Complete Claude sign-in in the browser or sign-in window.", Failed = true } : claude.Read();
                        }
                        catch (Exception) { snapshot = new AccountSnapshot { Status = "Claude account data is unavailable. Open Connect Claude to sign in or retry.", Failed = true }; }
                        updates.Enqueue(model =>
                        {
                            if (ClaudeEnabled && claudeVersion == claudeGeneration)
                            { model.Apply(0, snapshot); if (snapshot.SignedIn && !ClaudeAuth.Busy) ClaudeAuth.Message = "Account connected."; }
                        });
                    }
                    if (!readCodex) codex.Dispose();
                }
                finally { if (disposed) codex.Dispose(); Interlocked.Exchange(ref busy, 0); }
            });
        }
        public void Dispose()
        {
            disposed = true; ClaudeAuth.Dispose();
            if (Interlocked.CompareExchange(ref busy, 1, 0) == 0) { codex.Dispose(); Interlocked.Exchange(ref busy, 0); }
        }
    }
}
