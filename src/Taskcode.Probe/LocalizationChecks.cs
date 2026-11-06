using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal static class LocalizationChecks
    {
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        internal static void Run(MainWindow window, string directory)
        {
            var english = L10n.Load("en");
            Check(english.Count > 100 && new Settings().Language == "auto", "Localization must ship a source catalog and default to System.");
            foreach (string code in L10n.Languages.Where(c => c != "auto"))
            {
                CultureInfo.GetCultureInfo(code);
                foreach (var entry in L10n.Load(code))
                {
                    Check(english.ContainsKey(entry.Key), "Unknown translation key: " + code + "/" + entry.Key);
                    if (string.IsNullOrWhiteSpace(entry.Value)) continue;
                    Check(L10n.SamePlaceholders(entry.Key, entry.Value), "Translation placeholders differ: " + code + "/" + entry.Key);
                    try { string.Format(CultureInfo.InvariantCulture, entry.Value, Enumerable.Repeat((object)"sample", 16).ToArray()); }
                    catch (FormatException) { throw new InvalidOperationException("Invalid translation format: " + code + "/" + entry.Key); }
                }
            }
            Check(L10n.Resolve("auto", "pt-BR") == "pt-BR" && L10n.Resolve("auto", "en-US") == "en" && L10n.Resolve("auto", "fr-FR") == "en" &&
                L10n.Resolve("en", "pt-BR") == "en" && L10n.Normalize("unknown") == "auto", "System detection, explicit overrides and unsupported-language fallback must work.");
            Check(!L10n.SamePlaceholders("Value {0}", "Valor {1}"), "Placeholder mismatches must be rejected.");
            var selector = SettingsMenuChecks.Descendants(window).OfType<ComboBox>().Single(c => c.AccessibleName == "Language");
            int original = selector.SelectedIndex;
            byte[] saved = File.Exists(Settings.Path) ? File.ReadAllBytes(Settings.Path) : null;
            try
            {
                selector.SelectedIndex = Array.IndexOf(L10n.Languages, "pt-BR");
                Check(L10n.Language == "pt-BR" && Settings.Load().Language == "pt-BR", "Language selection must apply immediately and persist.");
                Check(L10n.T("Resets in 4h 54m") == "Renova em 4h 54m" && L10n.T("54% used / 94% time") == "54% de uso / 94% do tempo", "Dynamic reset and budget strings must preserve their real values.");
                Check(L10n.T("~22h 54m left · period avg") == "~22h 54m restantes · média do período" && L10n.T("Connected via Codex CLI / Updated 12s ago") == "Conectado pelo Codex CLI / Atualizado há 12s", "Nested connection and forecast templates must translate.");
                Check(L10n.T("New untranslated message") == "New untranslated message", "Missing translations must fall back to English.");
                Check(SettingsMenuChecks.Descendants(window).Any(c => c.Text == "Aparência") && SettingsMenuChecks.Descendants(window).Any(c => c.Text == "Fechar"), "Existing controls must update without reconnecting accounts.");
                var style = SettingsMenuChecks.Descendants(window).OfType<ComboBox>().Single(c => c.AccessibleName == "Indicator style");
                int styleIndex = style.SelectedIndex;
                Check(style.Items[0].ToString() == "Essencial", "Choice display must translate independently of enum IDs.");
                using (var board = new Bitmap(window.Width, window.Height * 3))
                using (var graphics = Graphics.FromImage(board))
                {
                    for (int page = 0; page < 3; page++)
                    {
                        window.ShowSettingsPage(page);
                        foreach (Control control in SettingsMenuChecks.Descendants(window))
                            if (control.Parent != null) Check(control.Parent.ClientRectangle.Contains(control.Bounds), "Localized control exceeds its parent: " + control.Text);
                        using (var bitmap = new Bitmap(window.Width, window.Height))
                        { window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); graphics.DrawImageUnscaled(bitmap, 0, page * window.Height); }
                    }
                    board.Save(Path.Combine(directory, "settings-pt-BR.png"));
                }
                RenderDetails(directory);
                selector.SelectedIndex = Array.IndexOf(L10n.Languages, "en");
                Check(L10n.Language == "en" && style.SelectedIndex == styleIndex && style.Items[0].ToString() == "Essential", "Switching back must preserve selected style and restore English.");
            }
            finally
            {
                selector.SelectedIndex = original;
                L10n.SetLanguage("en"); window.ShowSettingsPage(0);
                if (saved == null) { if (File.Exists(Settings.Path)) File.Delete(Settings.Path); }
                else File.WriteAllBytes(Settings.Path, saved);
            }
        }
        private static void RenderDetails(string directory)
        {
            using (var board = new Bitmap(600, 276))
            using (var graphics = Graphics.FromImage(board))
            for (int provider = 0; provider < 2; provider++)
            using (var details = new DetailWindow(new UsageDemo(), provider) { Location = new Point(-30000, -30000) })
            using (var bitmap = new Bitmap(300, 276))
            {
                details.Show(); details.RefreshUsage(); details.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); details.Hide();
                graphics.DrawImageUnscaled(bitmap, provider * 300, 0);
                if (provider == 1) board.Save(Path.Combine(directory, "details-pt-BR.png"));
            }
        }
    }
}
