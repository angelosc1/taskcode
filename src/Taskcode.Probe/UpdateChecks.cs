using System;
using System.IO;
using System.Security.Cryptography;

namespace Taskcode.Probe
{
    internal static class UpdateChecks
    {
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static object Fixture(string digest, string url, bool preview)
        {
            return AccountJson.Parse(AccountJson.Encode(new { tag_name = "v1.2.3", draft = false, prerelease = preview,
                assets = new[] { new { name = "Taskcode-Setup-1.2.3-win-x64.exe", browser_download_url = url, digest = digest, size = 4 } } }));
        }
        internal static void Run()
        {
            string url = AppInfo.Repository + "/releases/download/v1.2.3/Taskcode-Setup-1.2.3-win-x64.exe";
            byte[] bytes = { 1, 2, 3, 4 }; string hash;
            using (var algorithm = SHA256.Create()) hash = BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            var candidate = UpdateRelease.Parse(Fixture("sha256:" + hash, url, false), new Version(1, 0, 0));
            Check(candidate != null && candidate.Version == new Version(1, 2, 3), "A newer stable release must be offered with a verified asset identity.");
            Check(UpdateRelease.Parse(Fixture("sha256:" + hash, url, true), new Version(1, 0, 0)) == null, "Preview releases must not be silently offered as stable updates.");
            Check(UpdateRelease.Parse(Fixture("sha256:" + hash, url, false), new Version(1, 2, 3)) == null, "The current version must not reinstall itself.");
            foreach (object invalid in new[] { Fixture(null, url, false), Fixture("sha256:" + hash, "https://example.com/other.exe", false) })
            {
                bool rejected = false; try { UpdateRelease.Parse(invalid, new Version(1, 0, 0)); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "Updates with an absent checksum or a different download origin must be rejected.");
            }
            string file = Path.Combine(Path.GetTempPath(), "taskcode-update-" + Guid.NewGuid().ToString("N"));
            try
            {
                File.WriteAllBytes(file, bytes); Check(candidate.Matches(file), "A matching installer must pass size and SHA-256 verification.");
                File.WriteAllBytes(file, new byte[] { 4, 3, 2, 1 }); Check(!candidate.Matches(file), "Same-size tampering must fail SHA-256 verification.");
            }
            finally { if (File.Exists(file)) File.Delete(file); }
            Check(new Settings().AutomaticUpdateChecks, "Daily checks should be enabled by default, without enabling automatic installation.");
        }
    }
}
