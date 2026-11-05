using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Taskcode.Probe
{
    internal static class AccountPaths
    {
        internal static string PrivateHome(string provider) { return Path.Combine(ClaudeBridge.DataDirectory, "accounts", provider); }
        internal static string ClaudeHome(bool own)
        { return own ? PrivateHome("claude") : Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude"); }
        internal static string FindClaude()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var directories = new System.Collections.Generic.List<string> {
                Path.Combine(home, @".local\bin"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "pnpm"), Path.Combine(home, @".volta\bin") };
            directories.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));
            foreach (string directory in directories) foreach (string name in new[] { "claude.exe", "claude.cmd" })
            {
                try
                {
                    string path = Path.Combine(directory.Trim('"'), name);
                    string normalized = path.Replace('/', '\\').ToLowerInvariant();
                    if (normalized.Contains(@"\anthropicclaude\") || normalized.Contains(@"\claude\claude-code\") || normalized.Contains(@"\windowsapps\")) continue;
                    if (Path.IsPathRooted(path) && File.Exists(path)) return path;
                }
                catch (ArgumentException) { }
            }
            return null;
        }
    }

    // Adapted from Codenotch's MIT-licensed Windows CLI sign-in approach; see notices.
    internal sealed class ClaudeSignIn : IDisposable
    {
        private Process process;
        private int generation;
        internal volatile bool Busy;
        internal string Message = "Choose Sign in to open the official Claude login.";
        private const string LoginScript = "$Host.UI.RawUI.WindowTitle = 'Taskcode - Claude sign-in'; Write-Host 'Complete Claude sign-in in your browser. If a code is requested, paste it in this window.'; & $env:TASKCODE_AUTH_CLI auth login --claudeai; $result = $LASTEXITCODE; if ($result -eq 0) { Write-Host 'Signed in. Return to Taskcode.' } else { Write-Host 'Sign-in failed or was cancelled. Return to Taskcode to retry.' }; Start-Sleep -Seconds 2; exit $result";
        internal static ProcessStartInfo LoginInfo(string executable, string directory)
        {
            var info = PowerShellInfo(LoginScript);
            info.EnvironmentVariables["TASKCODE_AUTH_CLI"] = executable;
            foreach (string key in new System.Collections.Generic.List<string>(info.EnvironmentVariables.Keys.CastStrings()))
                if (key == "CLAUDECODE" || key.StartsWith("CLAUDE_CODE_", StringComparison.OrdinalIgnoreCase) || key == "ANTHROPIC_API_KEY" || key == "ANTHROPIC_AUTH_TOKEN") info.EnvironmentVariables.Remove(key);
            info.EnvironmentVariables["CLAUDE_CONFIG_DIR"] = directory;
            return info;
        }
        internal static ProcessStartInfo PowerShellInfo(string script)
        {
            return new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"), "-NoLogo -NoProfile -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)))
            { UseShellExecute = false, CreateNoWindow = false, WindowStyle = ProcessWindowStyle.Normal, WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) };
        }
        internal void Start(bool own, Action<bool> finished)
        {
            if (Busy) return;
            string executable = AccountPaths.FindClaude();
            if (executable == null) { Message = "Claude Code CLI is missing. Click Install official CLI, then Sign in."; return; }
            Directory.CreateDirectory(AccountPaths.ClaudeHome(own));
            Launch(LoginInfo(executable, AccountPaths.ClaudeHome(own)), finished, "Complete sign-in in the browser or Claude sign-in window.");
        }
        internal void Install(string provider, Action<bool> finished)
        {
            if (Busy) return;
            string script = provider == "Claude" ? "& ([scriptblock]::Create((Invoke-WebRequest -UseBasicParsing 'https://claude.ai/install.ps1').Content)); if ($LASTEXITCODE) { exit $LASTEXITCODE }" : "& npm.cmd install -g @openai/codex; exit $LASTEXITCODE";
            Launch(PowerShellInfo("$ErrorActionPreference = 'Stop'; " + script), finished, "Installing the official CLI. Follow the installation window.");
        }
        private void Launch(ProcessStartInfo info, Action<bool> finished, string message)
        {
            int run = Interlocked.Increment(ref generation);
            process = Process.Start(info); Busy = true; Message = message;
            var owned = process;
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool success = false;
                try { if (owned.WaitForExit(900000)) success = owned.ExitCode == 0; else { try { owned.Kill(); } catch (InvalidOperationException) { } } }
                catch (Exception) { }
                if (run == generation)
                {
                    Message = success ? "Completed. Refreshing account data..." : "Cancelled, failed, or timed out. You can retry.";
                    Busy = false; finished(success);
                }
                owned.Dispose();
            });
        }
        internal void Cancel()
        {
            Interlocked.Increment(ref generation);
            var owned = process; process = null;
            if (owned != null) try
            {
                if (!owned.HasExited)
                {
                    var stop = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "taskkill.exe"), "/PID " + owned.Id + " /T /F") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
                    using (var cleanup = Process.Start(stop)) { cleanup.BeginOutputReadLine(); cleanup.BeginErrorReadLine(); cleanup.WaitForExit(2000); }
                }
            }
            catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
            Busy = false; Message = "Sign-in cancelled.";
        }
        public void Dispose() { Cancel(); }
    }
    internal static class StringKeys
    {
        internal static System.Collections.Generic.IEnumerable<string> CastStrings(this ICollection keys) { foreach (object key in keys) yield return (string)key; }
    }
}
