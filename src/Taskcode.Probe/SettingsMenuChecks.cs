using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal static class SettingsMenuChecks
    {
        internal static IEnumerable<Control> Descendants(Control root)
        { foreach (Control control in root.Controls) { yield return control; foreach (var child in Descendants(control)) yield return child; } }
        internal static void Run(MainWindow window, string directory, bool startupExpected)
        {
            Check(window.FormBorderStyle == FormBorderStyle.None && window.Font.SizeInPoints <= 9, "Settings must use the compact borderless menu and smaller body text.");
            string[] pages = { "Appearance", "Accounts", "General" };
            int startups = 0, styles = 0, accounts = 0;
            CheckBox weeklyToggle = null, usageColors = null;
            foreach (Control control in Descendants(window))
            {
                var startup = control as CheckBox;
                if (startup != null && startup.AccessibleName == "Show weekly quota in the indicator") weeklyToggle = startup;
                if (startup != null && startup.AccessibleName == "Usage-based bar colors") usageColors = startup;
                if (startup != null && startup.Text == "Start with Windows") { startups++; Check(startup.Checked == startupExpected, "Refactoring settings must preserve Windows startup preference."); }
                var select = control as ComboBox;
                if (select != null && select.AccessibleName == "Indicator style") { styles++; Check(select.Items.Count == 3 && select.Items[0].ToString() == "Essential" && select.Items[1].ToString() == "Lines" && select.Items[2].ToString() == "Blocks" && new Settings().Style == IndicatorStyle.Essential, "The three styles must be ordered Essential, Lines, Blocks, with Essential as default."); }
                if (control is Button && (control.AccessibleName == "Connect Claude" || control.AccessibleName == "Connect Codex")) accounts++;
                if (control.Parent != null) Check(control.Parent.ClientRectangle.Contains(control.Bounds), "Settings control exceeds its parent: " + control.Text);
            }
            Check(startups == 1 && styles == 1 && accounts == 2, "Navigation must retain the original functional settings and both account connections.");
            Check(weeklyToggle != null, "The weekly switch must remain available.");
            bool weeklyBefore = weeklyToggle.Checked;
            try
            {
                Native.SendMessage(weeklyToggle.Handle, 0x00F5, IntPtr.Zero, IntPtr.Zero);
                Check(weeklyToggle.Checked != weeklyBefore, "A native click must toggle the custom-painted switch.");
                Check(weeklyToggle.AccessibilityObject.Role == AccessibleRole.CheckButton &&
                    ((weeklyToggle.AccessibilityObject.State & AccessibleStates.Checked) != 0) == weeklyToggle.Checked,
                    "Painted switches must retain their native accessible checked state.");
            }
            finally { weeklyToggle.Checked = weeklyBefore; }
            Check(usageColors != null && usageColors.Checked == Settings.Load().UsageBarColors, "Usage bar colors must reflect the saved preference.");
            bool colorsBefore = usageColors.Checked;
            try
            {
                Native.SendMessage(usageColors.Handle, 0x00F5, IntPtr.Zero, IntPtr.Zero);
                Check(usageColors.Checked != colorsBefore && Settings.Load().UsageBarColors == usageColors.Checked,
                    "Changing usage colors must apply and persist immediately.");
                foreach (Control control in Descendants(window))
                {
                    var preview = control as IndicatorView;
                    if (preview != null)
                    {
                        Check(preview.UsageBarColors == usageColors.Checked, "Preview must update without reopening settings.");
                        Check(preview.Parent.ClientRectangle.Contains(preview.Bounds), "Every style preview must stay inside its card after option changes.");
                    }
                }
            }
            finally { usageColors.Checked = colorsBefore; }
            using (var board = new Bitmap(window.Width, window.Height * 3))
            using (var graphics = Graphics.FromImage(board))
            {
                for (int palette = 0; palette < 2; palette++)
                {
                    var theme = TaskbarTheme.Create(palette == 0); window.ApplySettingsTheme(theme);
                    Check(window.BackColor == TaskbarTheme.Create(true).PreviewBackground && window.ForeColor == TaskbarTheme.Create(true).Text, "The menu must stay dark regardless of the taskbar palette.");
                    for (int page = 0; page < 3; page++)
                    {
                        window.ShowSettingsPage(page);
                        int visible = 0;
                        foreach (Control control in window.Controls)
                            if (control is Panel && control.AccessibleName != null && control.AccessibleName.EndsWith(" settings", StringComparison.Ordinal) && control.Visible)
                            { visible++; Check(control.AccessibleName == pages[page] + " settings", "Navigation must show the selected section."); }
                        Check(visible == 1, "Only one settings section may be visible.");
                        using (var bitmap = new Bitmap(window.Width, window.Height))
                        {
                            window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                            bitmap.Save(Path.Combine(directory, "settings-" + pages[page].ToLowerInvariant() + (palette == 0 ? "-dark.png" : "-light.png")), System.Drawing.Imaging.ImageFormat.Png);
                            if (palette == 0) graphics.DrawImageUnscaled(bitmap, 0, page * window.Height);
                        }
                    }
                }
                board.Save(Path.Combine(directory, "settings-menu-check.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            window.ApplySettingsTheme(TaskbarTheme.Read()); window.ShowSettingsPage(0);
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
