using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Automation;

namespace Taskcode.Probe
{
    internal sealed class LayoutSnapshot
    {
        internal IntPtr Taskbar;
        internal Rectangle Bar, Tray;
        internal int OccupiedRight, Dpi;
        internal DateTime MeasuredAt;
        internal string Error;
    }

    internal static class Placement
    {
        internal static readonly TimeSpan MeasurementLifetime = TimeSpan.FromSeconds(4);
        internal static LayoutSnapshot StabilizeMeasurement(LayoutSnapshot previous, LayoutSnapshot next)
        {
            // UI Automation can fail while Explorer updates its tree. Keep the last
            // verified bounds briefly; TickLayout still checks the live native bounds.
            if (next.Error != null && previous != null && previous.Error == null &&
                next.MeasuredAt >= previous.MeasuredAt && next.MeasuredAt - previous.MeasuredAt <= MeasurementLifetime)
                return previous;
            return next;
        }
        // Pure placement rule: never force a fit over existing controls.
        internal static bool TryViewport(Rectangle bar, Rectangle tray, int occupiedRight, int contentWidth, int height, int gap, out Rectangle slot)
        {
            int available = tray.Left - gap - Math.Max(bar.Left + gap, occupiedRight + gap);
            // Keep a usable viewport without covering any existing taskbar buttons.
            if (available < height) { slot = Rectangle.Empty; return false; }
            return TrySlot(bar, tray, occupiedRight, Math.Min(contentWidth, available), height, gap, out slot);
        }
        internal static bool TrySlot(Rectangle bar, Rectangle tray, int occupiedRight, int width, int height, int gap, out Rectangle slot)
        {
            slot = Rectangle.Empty;
            if (bar.Width <= bar.Height || tray.IsEmpty || !bar.IntersectsWith(tray)) return false;
            int right = tray.Left - gap;
            int left = right - width;
            if (left < Math.Max(bar.Left + gap, occupiedRight + gap) || height > bar.Height || width <= 0 || height <= 0) return false;
            slot = new Rectangle(left, bar.Top + (bar.Height - height) / 2, width, height);
            return true;
        }
    }

    internal sealed class TaskbarWatcher : IDisposable
    {
        private readonly object sync = new object();
        private readonly ManualResetEvent stop = new ManualResetEvent(false);
        private LayoutSnapshot latest;
        internal TaskbarWatcher()
        {
            var worker = new Thread(Watch) { IsBackground = true, Name = "Taskcode taskbar measurement" };
            worker.SetApartmentState(ApartmentState.MTA);
            worker.Start();
        }
        internal LayoutSnapshot Latest { get { lock (sync) return latest; } }
        private void Watch()
        {
            do
            {
                LayoutSnapshot result;
                try { result = Measure(); }
                catch (Exception ex) { result = new LayoutSnapshot { Error = "Taskbar measurement unavailable (" + ex.GetType().Name + ").", MeasuredAt = DateTime.UtcNow }; }
                lock (sync) latest = Placement.StabilizeMeasurement(latest, result);
            } while (!stop.WaitOne(1200));
        }
        private static LayoutSnapshot Measure()
        {
            var hwnd = Native.FindWindow("Shell_TrayWnd", null);
            Native.Rect bar;
            if (hwnd == IntPtr.Zero || !Native.GetWindowRect(hwnd, out bar) || !Native.IsWindowVisible(hwnd))
                return new LayoutSnapshot { Error = "Waiting for the primary taskbar.", MeasuredAt = DateTime.UtcNow };
            var result = new LayoutSnapshot { Taskbar = hwnd, Bar = bar.Bounds, Dpi = (int)Native.GetDpiForWindow(hwnd) };
            var tray = Native.FindWindowEx(hwnd, IntPtr.Zero, "TrayNotifyWnd", null);
            Native.Rect trayRect;
            if (tray == IntPtr.Zero || !Native.GetWindowRect(tray, out trayRect))
                throw new InvalidOperationException("System tray not found.");
            result.Tray = trayRect.Bounds;
            int pid = Process.GetCurrentProcess().Id;
            var root = AutomationElement.FromHandle(hwnd);
            var controls = root.FindAll(TreeScope.Descendants, new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem)));
            int measured = 0;
            int right = result.Bar.Left;
            foreach (AutomationElement element in controls)
            {
                var current = element.Current;
                if (current.ProcessId == pid || current.IsOffscreen) continue;
                var rect = current.BoundingRectangle;
                if (rect.IsEmpty || rect.Width <= 0 || rect.Right > result.Tray.Left || rect.Bottom <= result.Bar.Top || rect.Top >= result.Bar.Bottom) continue;
                right = Math.Max(right, (int)Math.Ceiling(rect.Right));
                measured++;
            }
            if (measured == 0) throw new InvalidOperationException("Could not verify taskbar button bounds.");
            result.OccupiedRight = right;
            result.MeasuredAt = DateTime.UtcNow;
            return result;
        }
        public void Dispose() { stop.Set(); }
    }
}
