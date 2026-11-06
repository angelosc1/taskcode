using System;
using System.IO;
using System.Reflection;

namespace Taskcode.Probe
{
    internal static class AppInfo
    {
        internal const string Repository = "https://github.com/angelosc1/taskcode";
        internal static readonly string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Taskcode");
        internal static Version Version
        {
            get { var version = Assembly.GetExecutingAssembly().GetName().Version; return new Version(version.Major, version.Minor, Math.Max(0, version.Build)); }
        }
        internal static string VersionText { get { return Version.ToString(3); } }
        internal static bool IsInstalled { get { return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "unins000.exe")); } }
    }
}
