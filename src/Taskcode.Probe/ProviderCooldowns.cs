using System;
using System.Collections.Generic;
using System.IO;

namespace Taskcode.Probe
{
    // Only retry deadlines are persisted, never account payloads or credentials.
    internal static class ProviderCooldowns
    {
        private static readonly object Gate = new object();
        private static string Path { get { return System.IO.Path.Combine(AppInfo.DataDirectory, "provider-cooldowns.json"); } }
        internal static DateTime Read(int index)
        {
            lock (Gate)
            {
                try { return OptionalHttp.Date(AccountJson.Get(ProviderCredentials.ReadJson(Path), ProviderCatalog.Ids[index])) ?? DateTime.MinValue; }
                catch (IOException) { return DateTime.MinValue; } catch (UnauthorizedAccessException) { return DateTime.MinValue; } catch (ArgumentException) { return DateTime.MinValue; }
            }
        }
        internal static void Write(int index, DateTime until)
        {
            lock (Gate)
            {
                try
                {
                    var data = new Dictionary<string, object>();
                    for (int provider = 0; provider < ProviderCatalog.Count; provider++)
                    { DateTime current = provider == index ? until : Read(provider); if (current > DateTime.UtcNow) data[ProviderCatalog.Ids[provider]] = current.ToString("o"); }
                    Directory.CreateDirectory(AppInfo.DataDirectory); ClaudeBridge.AtomicWrite(Path, AccountJson.Encode(data));
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }
}
