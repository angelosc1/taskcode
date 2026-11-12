using System;
using System.Drawing;

namespace Taskcode.Probe
{
    internal static class PlacementChecks
    {
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        internal static void Run(string directory = null)
        {
            var now = new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
            var verified = new LayoutSnapshot { Taskbar = new IntPtr(1), MeasuredAt = now, OccupiedRight = 800 };
            var failed = new LayoutSnapshot { Error = "UI Automation unavailable", MeasuredAt = now.AddSeconds(1.2) };
            Check(Placement.StabilizeMeasurement(null, failed) == failed, "Startup requires a verified taskbar measurement.");
            Check(Placement.StabilizeMeasurement(verified, failed) == verified, "A single transient measurement failure must not hide a verified indicator.");
            failed.MeasuredAt = now.AddSeconds(3.6);
            Check(Placement.StabilizeMeasurement(verified, failed) == verified && verified.MeasuredAt == now, "Retries must not extend the lifetime of the last verified measurement.");
            failed.MeasuredAt = now.AddSeconds(4.8);
            Check(Placement.StabilizeMeasurement(verified, failed) == failed, "Persistent failures must expire verified bounds.");
            var recovered = new LayoutSnapshot { Taskbar = new IntPtr(2), MeasuredAt = now.AddSeconds(6), OccupiedRight = 1600 };
            Check(Placement.StabilizeMeasurement(verified, recovered) == recovered, "A new successful measurement must replace cached taskbar geometry immediately.");
            Rectangle slot;
            Check(!Placement.TrySlot(new Rectangle(0, 0, 1920, 48), new Rectangle(1650, 0, 270, 48),
                recovered.OccupiedRight, 248, 34, 8, out slot), "A verified crowded layout must never reuse the previous free slot.");
            var bar = new Rectangle(0, 1032, 1920, 48); var tray = new Rectangle(1650, 1032, 270, 48);
            // Left-aligned apps with the Widgets button just before the tray (Windows 11 moves it there).
            var widgets = new[] { new Point(0, 600), new Point(1500, 1640) };
            Check(Placement.TryPlace(bar, tray, widgets, 248, 40, 8, out slot) && slot.Right == 1492 && slot.Left >= 608,
                "Widgets next to the tray must not hide the indicator; use the gap before Widgets.");
            // Centered apps: the tray-side gap is too small, the left gap is large.
            var centered = new[] { new Point(0, 160), new Point(700, 1480) };
            Check(Placement.TryPlace(bar, tray, centered, 248, 40, 8, out slot) && slot.Left == 168 && slot.Width == 248,
                "Centered taskbars must use the larger left gap without scrolling.");
            Check(Placement.TryPlace(bar, tray, centered, 140, 40, 8, out slot) && slot.Right == 1642,
                "The tray-side gap stays preferred whenever the whole indicator fits there.");
            Check(Placement.TryPlace(bar, tray, centered, 2000, 40, 8, out slot) && slot.Left == 168 && slot.Width == 524,
                "When nothing fits, scroll inside the largest gap.");
            var side = new Rectangle(0, 0, 48, 1080); var sideTray = new Rectangle(0, 800, 48, 280);
            Check(Placement.TryPlace(side, sideTray, new[] { new Point(0, 400) }, 200, 40, 8, out slot) && slot.Bottom == 792 && slot.Width == 40 && slot.Left == 4,
                "Vertical taskbars must stack the indicator above the tray.");
            Check(Placement.TryPlace(bar, tray, centered, 140, 40, 8, out slot, IndicatorAnchor.Start) && slot.Left == 168,
                "Dragging to the left must stick the indicator to the first gap from the left edge.");
            Check(Placement.TryPlace(bar, tray, widgets, 248, 40, 8, out slot, IndicatorAnchor.Start) && slot.Left == 608,
                "On a left-aligned taskbar the left anchor sits right after the apps.");
            Check(Placement.TryPlace(side, sideTray, new[] { new Point(0, 400) }, 200, 40, 8, out slot, IndicatorAnchor.Start) && slot.Top == 408,
                "On vertical taskbars the start anchor is the top of the free gap.");
            Check(!Placement.TryPlace(side, sideTray, new[] { new Point(0, 400) }, 200, 60, 8, out slot), "A pill wider than a vertical taskbar must be refused.");
            foreach (IndicatorStyle style in Enum.GetValues(typeof(IndicatorStyle)))
                Check(IndicatorView.VerticalStyle(style) == (style == IndicatorStyle.Donuts || style == IndicatorStyle.ColorStrip ? style : IndicatorStyle.Essential), "Only Essential, Donuts and Color Strip stack vertically.");
            var usage = new UsageDemo();
            const int previewScale = 3;
            using (var board = new Bitmap(6 * (IndicatorView.VerticalLogicalWidth * previewScale + 16) + 16, IndicatorView.VerticalLogicalHeight(IndicatorStyle.Donuts, true, usage) * previewScale + 32))
            using (var canvas = Graphics.FromImage(board))
            {
                canvas.Clear(Color.FromArgb(32, 32, 32)); int left = 16;
                foreach (var style in new[] { IndicatorStyle.Essential, IndicatorStyle.Donuts, IndicatorStyle.ColorStrip })
                foreach (bool weekly in new[] { false, true })
                {
                    var size = new Size(IndicatorView.VerticalLogicalWidth * previewScale, IndicatorView.VerticalLogicalHeight(style, weekly, usage) * previewScale);
                    var state = canvas.Save(); canvas.TranslateTransform(left, 16);
                    IndicatorView.DrawVertical(canvas, size, style, TaskbarTheme.Create(true), weekly, usage, true);
                    canvas.Restore(state); left += size.Width + 16;
                }
                if (directory != null) board.Save(System.IO.Path.Combine(directory, "vertical-preview.png"));
            }
            foreach (bool weekly in new[] { false, true })
            foreach (var style in new[] { IndicatorStyle.Essential, IndicatorStyle.Donuts, IndicatorStyle.ColorStrip })
            {
                var size = new Size(IndicatorView.VerticalLogicalWidth, IndicatorView.VerticalLogicalHeight(style, weekly, usage));
                Check(size.Height > size.Width, "The vertical pill must be taller than wide.");
                Check(IndicatorView.VerticalProviderAt(new Point(size.Width / 2, 4), size, style, weekly, usage) == 0 &&
                    IndicatorView.VerticalProviderAt(new Point(size.Width / 2, size.Height - 4), size, style, weekly, usage) == 1, "Vertical hit testing must follow the stacked providers.");
                using (var bitmap = new Bitmap(size.Width, size.Height))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    IndicatorWindow.DrawVerticalInputSurface(graphics, size, style, TaskbarTheme.Create(true), weekly, usage);
                    int opaque = 0;
                    for (int y = 0; y < size.Height; y += 2) for (int x = 0; x < size.Width; x += 2) if (bitmap.GetPixel(x, y).A > 200) opaque++;
                    Check(opaque > size.Width * size.Height / 8, "The vertical pill must render visible content.");
                }
            }
        }
    }
}
