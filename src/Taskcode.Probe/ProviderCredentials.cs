using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Taskcode.Probe
{
    internal sealed class ProviderCredential
    {
        internal string Token, Host = "https://api.z.ai", Identity = "", Org;
        internal bool OAuth, Expired;
    }
    internal static class ProviderCredentials
    {
        internal static string Home { get { return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); } }
        private static string KeyPath(int index) { return Path.Combine(AppInfo.DataDirectory, "provider-keys", ProviderCatalog.Ids[index] + ".bin"); }
        internal static void SaveKey(int index, string token, bool bigModel)
        {
            if (!ProviderCatalog.AcceptsKey(index) || string.IsNullOrWhiteSpace(token) || token.Length > 32768 || token.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0) throw new ArgumentException();
            string path = KeyPath(index); Directory.CreateDirectory(Path.GetDirectoryName(path));
            byte[] encrypted = Seal(index, token, bigModel);
            string temporary = path + ".tmp";
            try { File.WriteAllBytes(temporary, encrypted); if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        internal static byte[] Seal(int index, string token, bool bigModel)
        {
            byte[] plain = Encoding.UTF8.GetBytes(AccountJson.Encode(new { token = token.Trim(), bigModel = bigModel }));
            try { return ProtectedData.Protect(plain, Encoding.UTF8.GetBytes(ProviderCatalog.Ids[index]), DataProtectionScope.CurrentUser); }
            finally { Array.Clear(plain, 0, plain.Length); }
        }
        internal static void RemoveKey(int index) { if (File.Exists(KeyPath(index))) File.Delete(KeyPath(index)); }
        internal static ProviderCredential Manual(int index)
        {
            string path = KeyPath(index); if (!File.Exists(path)) return null;
            return Unseal(index, File.ReadAllBytes(path));
        }
        internal static ProviderCredential Unseal(int index, byte[] encrypted)
        {
            byte[] plain = ProtectedData.Unprotect(encrypted, Encoding.UTF8.GetBytes(ProviderCatalog.Ids[index]), DataProtectionScope.CurrentUser);
            try { var data = AccountJson.Parse(Encoding.UTF8.GetString(plain)); return new ProviderCredential { Token = Text(AccountJson.Get(data, "token")), Host = Equals(AccountJson.Get(data, "bigModel"), true) ? "https://open.bigmodel.cn" : "https://api.z.ai" }; }
            finally { Array.Clear(plain, 0, plain.Length); }
        }
        internal static string Text(object value) { var text = value as string; return string.IsNullOrWhiteSpace(text) ? null : text.Trim(); }
        internal static object ReadJson(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 1048576) return null;
            return AccountJson.Parse(File.ReadAllText(path));
        }
        internal static string Key(object entry)
        {
            if (entry is string) return Text(entry);
            foreach (string field in new[] { "key", "apiKey", "api_key", "token", "accessToken", "auth_token" })
            { string key = Text(AccountJson.Get(entry, field)); if (key != null) return key; }
            return null;
        }
        internal static string FindCli(string name)
        {
            var directories = new List<string> { Path.Combine(Home, @".local\bin"), Path.Combine(Home, @".bun\bin"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GitHub CLI"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WinGet\Links") };
            directories.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));
            foreach (var directory in directories) foreach (string extension in new[] { ".exe", ".cmd" })
            {
                try { string path = Path.Combine(directory.Trim('"'), name + extension); if (Path.IsPathRooted(path) && File.Exists(path)) return path; }
                catch (ArgumentException) { }
            }
            return null;
        }
        internal static string RunHidden(string executable, string arguments, int timeout = 10000, string workingDirectory = null)
        {
            using (var process = new Process { StartInfo = new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true, WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executable) } })
            {
                process.Start();
                using (var job = new ProviderProcessJob(process))
                {
                    process.StandardInput.Close();
                    var output = Task.Factory.StartNew(() => { char[] buffer = new char[1048577]; int count = process.StandardOutput.ReadBlock(buffer, 0, buffer.Length); if (count > 1048576) throw new InvalidDataException(); return new string(buffer, 0, count); });
                    // Discard stderr without retaining raw SDK or authentication diagnostics.
                    var errors = Task.Factory.StartNew(() => { char[] buffer = new char[4096]; while (process.StandardError.Read(buffer, 0, buffer.Length) > 0) { } });
                    if (!process.WaitForExit(timeout)) { try { process.Kill(); } catch (InvalidOperationException) { } throw new TimeoutException(); }
                    if (!output.Wait(2000) || !errors.Wait(2000)) throw new TimeoutException();
                    return output.Result.Trim();
                }
            }
        }
        internal static ProviderCredential Glm()
        {
            var manual = Manual(2); if (manual != null) return manual;
            string token = Text(Environment.GetEnvironmentVariable("GLM_API_KEY")); if (token != null) return new ProviderCredential { Token = token };
            string claudeHome = AccountPaths.ClaudeHome(false);
            var env = AccountJson.Get(ReadJson(Path.Combine(claudeHome, "settings.json")), "env");
            Uri uri;
            if (Uri.TryCreate(Text(AccountJson.Get(env, "ANTHROPIC_BASE_URL")), UriKind.Absolute, out uri) && TrustedGlmHost(uri.Host))
            {
                token = Text(AccountJson.Get(env, "ANTHROPIC_AUTH_TOKEN")) ?? Text(AccountJson.Get(env, "ANTHROPIC_API_KEY"));
                if (token != null) return new ProviderCredential { Token = token, Host = GlmHost(uri.Host) };
            }
            var providers = AccountJson.Map(AccountJson.Get(ReadJson(Path.Combine(Home, @".zcode\v2\config.json")), "provider"));
            foreach (var provider in providers)
            {
                if (!provider.Key.Contains("coding-plan") || Equals(AccountJson.Get(provider.Value, "enabled"), false)) continue;
                var options = AccountJson.Get(provider.Value, "options"); token = Text(AccountJson.Get(options, "apiKey"));
                if (token != null && Uri.TryCreate(Text(AccountJson.Get(options, "baseURL")), UriKind.Absolute, out uri) && TrustedGlmHost(uri.Host)) return new ProviderCredential { Token = token, Host = GlmHost(uri.Host) };
            }
            foreach (string directory in OpenCodeDirectories())
            {
                object data = ReadJson(Path.Combine(directory, "auth.json"));
                foreach (string id in new[] { "zai-coding-plan", "zai", "z-ai", "z.ai", "glm", "zhipu", "zhipuai" })
                { token = Key(AccountJson.Get(data, id)); if (token != null) return new ProviderCredential { Token = token, Host = id.StartsWith("zhipu", StringComparison.Ordinal) ? "https://open.bigmodel.cn" : "https://api.z.ai" }; }
            }
            return null;
        }
        internal static bool TrustedGlmHost(string host) { return host == "api.z.ai" || host.EndsWith(".z.ai", StringComparison.Ordinal) || host == "open.bigmodel.cn" || host.EndsWith(".bigmodel.cn", StringComparison.Ordinal); }
        private static string GlmHost(string host) { return host.EndsWith("bigmodel.cn", StringComparison.Ordinal) ? "https://open.bigmodel.cn" : "https://api.z.ai"; }
        internal static ProviderCredential Grok(object data, DateTime now)
        {
            foreach (var item in AccountJson.Map(data))
            {
                if (item.Key.Split(new[] { "::" }, StringSplitOptions.None)[0] != "https://auth.x.ai" && Text(AccountJson.Get(item.Value, "oidc_issuer")) != "https://auth.x.ai") continue;
                string token = Text(AccountJson.Get(item.Value, "key")); var expiry = OptionalHttp.Date(AccountJson.Get(item.Value, "expires_at"));
                if (token != null && (!expiry.HasValue || expiry > now)) return new ProviderCredential { Token = token, Identity = Text(AccountJson.Get(item.Value, "email")) ?? "" };
            }
            return null;
        }
        // An xAI session exists but has expired: the official CLI renews it the next time it runs.
        internal static bool GrokExpired(object data, DateTime now)
        {
            foreach (var item in AccountJson.Map(data))
            {
                if (item.Key.Split(new[] { "::" }, StringSplitOptions.None)[0] != "https://auth.x.ai" && Text(AccountJson.Get(item.Value, "oidc_issuer")) != "https://auth.x.ai") continue;
                var expiry = OptionalHttp.Date(AccountJson.Get(item.Value, "expires_at"));
                if (Text(AccountJson.Get(item.Value, "key")) != null && expiry.HasValue && expiry <= now) return true;
            }
            return false;
        }
        internal static ProviderCredential Copilot()
        {
            var manual = Manual(4); if (manual != null) return manual;
            string token = Text(Environment.GetEnvironmentVariable("GH_TOKEN")) ?? Text(Environment.GetEnvironmentVariable("GITHUB_TOKEN"));
            if (token != null) return new ProviderCredential { Token = token };
            string gh = FindCli("gh");
            if (gh != null && gh.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            { token = Text(RunHidden(gh, "auth token --hostname github.com")); if (token != null) return new ProviderCredential { Token = token }; }
            return null;
        }
        internal static IEnumerable<string> OpenCodeDirectories()
        {
            string xdg = Text(Environment.GetEnvironmentVariable("XDG_DATA_HOME")); if (xdg != null) yield return Path.Combine(xdg, "opencode");
            yield return Path.Combine(Home, @".local\share\opencode");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "opencode");
        }
        internal static ProviderCredential OpenCode()
        {
            var manual = Manual(5); if (manual != null) return manual;
            string env = Text(Environment.GetEnvironmentVariable("OPENCODE_APIKEY")); if (env != null) return new ProviderCredential { Token = env };
            foreach (var directory in OpenCodeDirectories())
            {
                try
                {
                    var values = CursorUsage.ReadValues(Path.Combine(directory, "opencode.db"), "SELECT integration_id,value FROM credential WHERE integration_id IN ('opencode-go','opencode') AND COALESCE(active,1)!=0 ORDER BY time_updated DESC");
                    foreach (string id in new[] { "opencode-go", "opencode" })
                    { string value; if (values.TryGetValue(id, out value)) { var credential = OpenCodeEntry(AccountJson.Parse(value)); if (credential != null) return credential; } }
                }
                catch (IOException) { }
                object data = ReadJson(Path.Combine(directory, "auth.json"));
                foreach (string id in new[] { "opencode-go", "opencode" }) { var credential = OpenCodeEntry(AccountJson.Get(data, id)); if (credential != null) return credential; }
            }
            return null;
        }
        internal static ProviderCredential OpenCodeEntry(object entry)
        {
            if (Text(AccountJson.Get(entry, "type")) == "oauth")
            {
                string token = Text(AccountJson.Get(entry, "access")); if (token == null) return null;
                var server = Text(AccountJson.Get(AccountJson.Get(entry, "metadata"), "server")); Uri uri;
                if (server != null && (!Uri.TryCreate(server, UriKind.Absolute, out uri) || uri.Scheme != "https" || uri.Host != "opencode.ai")) return null;
                var expires = SubscriptionUsage.Timestamp(AccountJson.Get(entry, "expires"));
                return new ProviderCredential { Token = token, OAuth = true, Expired = expires.HasValue && expires <= DateTime.UtcNow, Org = Text(AccountJson.Get(AccountJson.Get(entry, "metadata"), "orgID")) };
            }
            string key = Key(entry); return key == null ? null : new ProviderCredential { Token = key };
        }
    }
}
