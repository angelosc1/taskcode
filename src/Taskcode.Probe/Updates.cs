using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;

namespace Taskcode.Probe
{
    internal sealed class UpdateRelease
    {
        internal Version Version;
        internal string Url, Name, Sha256;
        internal long Size;
        internal const long MaximumSize = 64 * 1024 * 1024;
        internal static UpdateRelease Parse(object root, Version current)
        {
            if (!object.Equals(AccountJson.Get(root, "draft"), false) || !object.Equals(AccountJson.Get(root, "prerelease"), false)) return null;
            string tag = AccountJson.Get(root, "tag_name") as string;
            if (tag == null || !Regex.IsMatch(tag, @"^v\d+\.\d+\.\d+$")) throw new InvalidDataException();
            Version version;
            if (!System.Version.TryParse(tag.Substring(1), out version)) throw new InvalidDataException();
            if (version <= current) return null;
            string name = "Taskcode-Setup-" + version.ToString(3) + "-win-x64.exe";
            string url = AppInfo.Repository + "/releases/download/" + tag + "/" + name;
            foreach (var item in AccountJson.Get(root, "assets") as IEnumerable ?? new object[0])
            {
                if ((AccountJson.Get(item, "name") as string) != name) continue;
                string digest = AccountJson.Get(item, "digest") as string;
                var size = AccountJson.Number(AccountJson.Get(item, "size"));
                if ((AccountJson.Get(item, "browser_download_url") as string) != url || digest == null ||
                    !Regex.IsMatch(digest, @"^sha256:[a-fA-F0-9]{64}$") || !size.HasValue || size <= 0 || size > MaximumSize || size != Math.Floor(size.Value))
                    throw new InvalidDataException();
                return new UpdateRelease { Version = version, Name = name, Url = url, Sha256 = digest.Substring(7), Size = (long)size.Value };
            }
            throw new InvalidDataException();
        }
        internal bool Matches(string file)
        {
            using (var input = File.OpenRead(file))
            using (var algorithm = SHA256.Create())
                return input.Length == Size && string.Equals(BitConverter.ToString(algorithm.ComputeHash(input)).Replace("-", ""), Sha256, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class UpdateService : IDisposable
    {
        private readonly object sync = new object();
        private DateTime nextAuto = DateTime.UtcNow.AddSeconds(10);
        private bool busy, disposed;
        private string message = "Checks GitHub Releases", installer;
        private UpdateRelease release;
        private DateTime checkedUtc;
        internal bool Busy { get { lock (sync) return busy; } }
        internal string Message { get { lock (sync) return message; } }
        internal UpdateRelease Available { get { lock (sync) return release; } }
        internal DateTime CheckedUtc { get { lock (sync) return checkedUtc; } }
        internal void Tick(bool automatic, DateTime lastChecked)
        {
            if (automatic && DateTime.UtcNow >= nextAuto && DateTime.UtcNow - lastChecked >= TimeSpan.FromHours(24)) CheckNow();
        }
        private bool Begin(string status)
        {
            lock (sync)
            {
                if (busy || disposed) return false;
                busy = true; message = status; nextAuto = DateTime.UtcNow.AddHours(1); return true;
            }
        }
        internal void CheckNow()
        {
            if (!Begin("Checking for updates...")) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    string json;
                    using (var response = Open("https://api.github.com/repos/angelosc1/taskcode/releases/latest"))
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        var buffer = new char[1048577]; int count = reader.ReadBlock(buffer, 0, buffer.Length);
                        if (count > 1048576) throw new InvalidDataException();
                        json = new string(buffer, 0, count);
                    }
                    var available = UpdateRelease.Parse(AccountJson.Parse(json), AppInfo.Version);
                    lock (sync)
                    {
                        release = available; checkedUtc = DateTime.UtcNow;
                        message = available == null ? "You're up to date" : "Version " + available.Version.ToString(3) + " available";
                    }
                }
                catch (Exception error) { Fail("Could not check. Try again.", error); }
                finally { lock (sync) busy = false; }
            });
        }
        internal void Download()
        {
            var candidate = Available;
            if (candidate == null || !Begin("Downloading update...")) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string partial = null;
                try
                {
                    string directory = Path.Combine(AppInfo.DataDirectory, "updates"); Directory.CreateDirectory(directory);
                    string destination = Path.Combine(directory, candidate.Name); partial = destination + ".part";
                    using (var response = Open(candidate.Url))
                    using (var input = response.GetResponseStream())
                    using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        if (response.ContentLength > UpdateRelease.MaximumSize) throw new InvalidDataException();
                        var buffer = new byte[65536]; int count, previous = -1; long total = 0;
                        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            lock (sync) if (disposed) return;
                            total += count; if (total > candidate.Size) throw new InvalidDataException();
                            output.Write(buffer, 0, count);
                            int percent = (int)(total * 100 / candidate.Size);
                            if (percent != previous) { lock (sync) message = "Downloading update... " + percent + "%"; previous = percent; }
                        }
                    }
                    if (!candidate.Matches(partial)) throw new InvalidDataException();
                    if (File.Exists(destination)) File.Delete(destination);
                    File.Move(partial, destination); partial = null;
                    lock (sync) { if (!disposed) { installer = destination; message = "Installing update..."; } }
                }
                catch (Exception error) { Fail("Update failed. Try again.", error); }
                finally
                {
                    if (partial != null) try { File.Delete(partial); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    lock (sync) busy = false;
                }
            });
        }
        internal string TakeInstaller()
        {
            lock (sync) { string ready = installer; installer = null; return ready; }
        }
        internal void LaunchFailed() { lock (sync) message = "Could not start setup. Try again."; }
        private void Fail(string status, Exception error)
        {
            lock (sync) message = status;
            Program.RecordLifecycle("Update request failed: " + error.GetType().Name);
        }
        private static HttpWebResponse Open(string address)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var uri = new Uri(address);
            for (int redirects = 0; redirects <= 5; redirects++)
            {
                if (uri.Scheme != "https" || !(uri.Host == "api.github.com" || uri.Host == "github.com" || uri.Host == "release-assets.githubusercontent.com" || uri.Host == "objects.githubusercontent.com")) throw new InvalidDataException();
                var request = (HttpWebRequest)WebRequest.Create(uri);
                request.UserAgent = "Taskcode/" + AppInfo.VersionText; request.Timeout = request.ReadWriteTimeout = 15000; request.AllowAutoRedirect = false;
                var response = (HttpWebResponse)request.GetResponse();
                int status = (int)response.StatusCode;
                if (status >= 300 && status < 400)
                {
                    string location = response.Headers["Location"]; response.Dispose();
                    if (location == null) throw new InvalidDataException();
                    uri = new Uri(uri, location); continue;
                }
                return response;
            }
            throw new InvalidDataException();
        }
        public void Dispose() { lock (sync) disposed = true; }
    }
}
