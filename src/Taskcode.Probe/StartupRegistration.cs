using System;
using System.IO;
using Microsoft.Win32;

namespace Taskcode.Probe
{
    internal static class StartupRegistration
    {
        private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Taskcode";
        internal static string CommandFor(string executable)
        {
            if (string.IsNullOrEmpty(executable) || !Path.IsPathRooted(executable) || executable.IndexOf('"') >= 0)
                throw new ArgumentException("A valid absolute executable path is required.");
            string command = "\"" + executable + "\" --tray";
            if (command.Length > 260) throw new IOException("The startup command is too long. Move Taskcode to a shorter path.");
            return command;
        }
        internal static bool IsEnabled(string executable)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(KeyPath))
                return key != null && string.Equals(key.GetValue(ValueName) as string, CommandFor(executable), StringComparison.OrdinalIgnoreCase);
        }
        internal static void SetEnabled(string executable, bool enabled)
        {
            if (enabled)
            {
                using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    if (key == null) throw new IOException("Windows startup settings are unavailable.");
                    key.SetValue(ValueName, CommandFor(executable), RegistryValueKind.String);
                }
            }
            else
            {
                using (var key = Registry.CurrentUser.OpenSubKey(KeyPath, true))
                    if (key != null) key.DeleteValue(ValueName, false);
            }
        }
    }
}
