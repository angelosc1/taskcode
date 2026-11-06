using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal static class DetailOpeningChecks
    {
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Pump(int milliseconds)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(5); }
        }
        internal static void Run()
        {
            var policy = new DetailInteraction();
            var card = new Rectangle(100, 100, 300, 276); var anchor = new Rectangle(160, 384, 120, 40); var away = new Point(700, 700);
            policy.Reset(false, false);
            Check(!policy.ShouldDismiss(card, anchor, new Point(180, 380), false) && policy.ShouldDismiss(card, anchor, away, false), "Hover cards must retain their physical gap and dismiss immediately on pointer exit.");
            policy.Reset(true, true);
            Check(!policy.ShouldDismiss(card, anchor, away, true), "The opening click must never dismiss a pinned card.");
            Check(!policy.ShouldDismiss(card, anchor, away, false) && !policy.ShouldDismiss(card, anchor, away, false), "Explicit details must survive pointer movement away from a hidden preview.");
            Check(!policy.ShouldDismiss(card, anchor, new Point(120, 120), true), "Clicks inside the pinned card must remain available.");
            policy.ShouldDismiss(card, anchor, away, false);
            Check(policy.ShouldDismiss(card, anchor, away, true), "A new outside click must close pinned details.");
            byte[] saved = File.Exists(Settings.Path) ? File.ReadAllBytes(Settings.Path) : null;
            string language = L10n.Language;
            MainWindow window = null;
            try
            {
                L10n.SetLanguage("en");
                window = new MainWindow(false, false) { StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000) };
                window.OpenSettings(); window.ShowSettingsPage(0);
                var showIndicator = SettingsMenuChecks.Descendants(window).OfType<CheckBox>().Single(control => control.Text == "Show indicator");
                showIndicator.Checked = false; // Forces the layout's pause path during the real timer loop.
                window.ShowSettingsPage(1); // The appearance preview is now hidden, as in the reported regression.
                var foreground = Native.GetForegroundWindow();
                window.OpenDetailsFromSettings(0); Pump(400);
                Check(window.VisibleDetailProvider == 0 && window.VisibleDetailCount == 1, "Settings must keep explicit Claude details open across dismissal and paused-layout timers, with the preview hidden.");
                Check(!window.DetailsHaveForeground && Native.GetForegroundWindow() == foreground, "Explicit details must remain non-activating while pinned.");
                window.ShowDetails(false, true, 1); Pump(50);
                Check(window.VisibleDetailProvider == 0, "Incidental hover must not replace an explicitly opened panel.");
                window.OpenDetailsFromSettings(1); Pump(400);
                Check(window.VisibleDetailProvider == 1 && window.VisibleDetailCount == 1, "Explicit Codex details must replace Claude and remain open through the message loop.");
                var panel = Application.OpenForms.OfType<DetailWindow>().Single(control => control.Visible);
                var close = panel.Controls.OfType<Button>().Single(control => control.AccessibleName == "Close details");
                Check(close.Visible, "Pinned details must expose a close action."); close.PerformClick(); Pump(50);
                Check(window.VisibleDetailCount == 0, "The close action must dismiss explicit details.");
                var optional = ProviderCatalog.Create()[0]; optional.Enabled = true;
                using (var view = new OptionalProviderDetails(optional) { Location = new Point(-30000, -30000), RequiresIndicator = false })
                {
                    view.SetInteraction(true); view.Show(); Pump(100);
                    Check(view.Visible && view.Pinned, "Optional-provider explicit details must also survive the dismissal timer.");
                    view.SetInteraction(false); view.TrackPointer(away);
                    Check(!view.Visible, "Optional-provider hover details must still dismiss on exit.");
                }
            }
            finally
            {
                if (window != null) { window.RequestExit(); window.Dispose(); }
                if (saved != null) File.WriteAllBytes(Settings.Path, saved); else if (File.Exists(Settings.Path)) File.Delete(Settings.Path);
                L10n.SetLanguage(language);
            }
        }
    }
}
