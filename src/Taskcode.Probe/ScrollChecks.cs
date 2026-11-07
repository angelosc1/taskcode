using System;
using System.Drawing;
using System.IO;

namespace Taskcode.Probe
{
    internal static class ScrollChecks
    {
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        internal static void Run(string directory)
        {
            Rectangle slot;
            var bar = new Rectangle(0, 0, 1920, 48); var tray = new Rectangle(1650, 0, 270, 48);
            Check(Placement.TryViewport(bar, tray, 1000, 2400, 40, 8, out slot) && slot.Width == 634 && slot.Right == 1642 && slot.Left == 1008, "All enabled providers must use the maximum verified gap rather than hiding or overlapping buttons.");
            Check(Placement.TryViewport(bar, tray, 1000, 248, 40, 8, out slot) && slot.Width == 248, "Fitting content must retain its normal width.");
            Check(!Placement.TryViewport(bar, tray, 1630, 2400, 40, 8, out slot), "A truly unusable gap must never cover Windows controls.");
            Check(!Placement.TryViewport(bar, Rectangle.Empty, 1000, 2400, 40, 8, out slot), "A missing tray must still reject placement.");
            var usage = new UsageDemo { OptionalProviders = ProviderSamples.Create(DateTime.UtcNow) };
            foreach (var provider in usage.OptionalProviders) provider.Enabled = true;
            using (var board = new Bitmap(640, 7 * 100))
            using (var graphics = Graphics.FromImage(board))
            {
                graphics.Clear(Color.FromArgb(28, 30, 33));
                foreach (IndicatorStyle style in Enum.GetValues(typeof(IndicatorStyle)))
                foreach (bool weekly in new[] { false, true })
                foreach (float scale in new[] { 1f, 1.25f, 1.5f, 2f })
                using (var view = new IndicatorWindow { Usage = usage, Theme = TaskbarTheme.Create(true), DisplayStyle = style, ShowWeekly = weekly })
                {
                    view.ClientSize = new Size((int)(280 * scale), (int)Math.Round(IndicatorView.LogicalHeight * scale));
                    view.ContentWidth = (int)Math.Round(IndicatorView.LogicalWidth(style, weekly, usage) * scale);
                    Check(view.IsScrolling, "All eleven providers must overflow a bounded viewport in every style and DPI.");
                    int width = view.Width, total = view.ContentWidth, cycle = view.ScrollCycleWidth;
                    Check(view.ScrollGap >= 20 * scale && cycle > total, "The final and first providers must have a DPI-scaled gap between cycles.");
                    view.AdvanceScroll(1);
                    Check(view.ScrollOffset > 0 && view.Width == width, "Providers must move right to left without resizing or shrinking the viewport.");
                    view.ScrollPaused = true; int paused = view.ScrollOffset; view.AdvanceScroll(2);
                    Check(view.ScrollOffset == paused, "Reading and hover pauses must freeze the visible content.");
                    view.ScrollPaused = false;
                    for (int provider = 0; provider < usage.OptionalProviders.Length + 2; provider++)
                    {
                        var cell = IndicatorView.ProviderBounds(provider, new Size(total, view.Height), style, weekly, usage);
                        double target = (cell.Left + cell.Width / 2.0 - width / 2.0 + cycle) % cycle;
                        double advance = (target - view.ScrollOffset + cycle) % cycle;
                        view.AdvanceScroll(advance / (view.Height * .65));
                        Check(view.ProviderAt(new Point(width / 2, view.Height / 2)) == provider, "Clicks and hover must route to each scrolled provider, including optional providers and the wrap boundary.");
                        var visible = view.ProviderBounds(provider);
                        Check(visible.Width > 0 && view.ClientRectangle.Contains(visible), "Details anchors must use the visible portion of the provider.");
                    }
                    using (var bitmap = new Bitmap(view.Width, view.Height))
                    {
                        using (var canvas = Graphics.FromImage(bitmap)) view.DrawViewport(canvas);
                        Check(bitmap.GetPixel(0, 0).A <= 1, "Scrolling must preserve transparent capsule corners and the native input surface.");
                        if (scale == 1f) graphics.DrawImageUnscaled(bitmap, weekly ? 335 : 15, (int)style * 100 + 25);
                    }
                    double gapTarget = (total + view.ScrollGap / 2.0 - width / 2.0 + cycle) % cycle;
                    view.AdvanceScroll(((gapTarget - view.ScrollOffset + cycle) % cycle) / (view.Height * .65));
                    Check(view.ProviderAt(new Point(width / 2, view.Height / 2)) == -1, "The spacing between the last and first providers must not open any provider's Details.");
                    view.ContentWidth = width;
                    Check(!view.IsScrolling && view.ScrollOffset == 0, "Content that fits again must stop scrolling and return to its origin.");
                }
                board.Save(Path.Combine(directory, "provider-scroll-check.png"));
            }
        }
    }
}
