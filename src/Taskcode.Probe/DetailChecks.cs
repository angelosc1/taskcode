using System;
using System.Drawing;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal static class DetailChecks
    {
        internal static void Run()
        {
            var usage = new UsageDemo();
            var nowUtc = DateTime.UtcNow;
            Check(usage.DisplayUsed(0, nowUtc) == 32 && usage.DisplayUsed(3, nowUtc) == 37, "Demo percentages must show consumption.");
            usage.BeginLive();
            Check(usage.DisplayUsed(0, nowUtc) == -1, "Unknown consumption must stay unavailable, not become 101 percent.");
            foreach (int remaining in new[] { 100, 75, 0 })
            using (var bar = new QuotaBar { Size = new Size(280, 17), Provider = "claude", Theme = TaskbarTheme.Create(true), Period = new QuotaPeriod { Remaining = remaining } })
            using (var image = new Bitmap(280, 17))
            {
                bar.DrawToBitmap(image, bar.ClientRectangle);
                float boundary = 6 + 268 * (100 - remaining) / 100f;
                foreach (int x in new[] { 10, 60, 100, 270 })
                    Check(image.GetPixel(x, 11).ToArgb() == (x < boundary ? bar.Theme.Claude : bar.Theme.Track).ToArgb(), "Colored fill must grow with consumed quota, including empty and exhausted limits.");
            }
            foreach (int provider in new[] { 0, 1 })
            using (var panel = new DetailWindow(new UsageDemo(), provider) { Location = new Point(-30000, -30000), HoverAnchor = new Rectangle(-30000, -29740, 150, 40) })
            {
                panel.Show();
                Check(panel.TopMost && (Native.GetStyle(panel.Handle, -20).ToInt64() & 8L) != 0,
                    "Hover cards must appear above the foreground app and topmost taskbar without requiring click activation.");
                Check(panel.FormBorderStyle == FormBorderStyle.None && !panel.ControlBox && !panel.ShowInTaskbar, "Details must be a borderless card without native title/close controls.");
                Check(panel.ClientSize.Height <= 252, "The compact card must fit the restored budget and speed rows.");
                foreach (Control control in panel.Controls)
                {
                    var bar = control as QuotaBar;
                    if (bar != null)
                    {
                        Check(TextRenderer.MeasureText("100%", bar.ValueCaption.Font).Width <= bar.ValueCaption.Width, "The percentage must fit completely.");
                        Check(bar.BudgetCaption.Text.Contains("% used / ") && bar.BudgetCaption.Text.Contains("% time"), "Details must expose consumption versus elapsed time directly.");
                        Check(bar.SpeedCaption.Visible && bar.SpeedCaption.HasArrows, "Active sample histories must expose speed arrows and their time estimate.");
                        Check(panel.ClientRectangle.Contains(bar.SpeedCaption.Bounds), "The restored speed row must fit the card.");
                    }
                    var label = control as Label;
                    if (label == null) continue;
                    Check(label.Text != "Taskcode" && !label.Text.Contains("DATA /") && !label.Text.StartsWith("Gray:") && !label.Text.Contains("history"), "Details must not contain decorative titles, legends, or footer text.");
                    Check(panel.ClientRectangle.Contains(label.Bounds), "Every detail label must fit inside the compact card.");
                }
                var now = DateTime.UtcNow;
                panel.TrackPointer(new Point(panel.HoverAnchor.Left + 5, panel.HoverAnchor.Top + 5), now);
                panel.TrackPointer(new Point(panel.Left + 60, panel.Bottom + 4), now);
                Check(panel.Visible, "The direct bridge between trigger and card must remain traversable without a time delay.");
                panel.TrackPointer(new Point(panel.Left + 100, panel.Top + 70), now);
                Check(panel.Visible, "Pointer over a card's child controls must keep the card open.");
                panel.TrackPointer(new Point(-25000, -25000), now);
                Check(!panel.Visible, "Leaving card, trigger, and crossing must immediately dismiss every provider card.");
            }
            using (var panel = new DetailWindow(usage) { Location = new Point(-30000, -30000) })
            {
                panel.Show();
                foreach (Control control in panel.Controls)
                {
                    var bar = control as QuotaBar;
                    if (bar != null) Check(bar.SpeedCaption.Visible && !bar.SpeedCaption.HasArrows && bar.SpeedCaption.Estimate.State == SpeedState.Unavailable,
                        "Missing observations must show an honest status instead of hiding the row or fabricating arrows.");
                }
            }
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
