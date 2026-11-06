using System;
using System.Drawing;

namespace Taskcode.Probe
{
    internal static class PlacementChecks
    {
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        internal static void Run()
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
        }
    }
}
