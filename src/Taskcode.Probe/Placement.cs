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
        // Occupied spans along the taskbar's main axis (X = start, Y = end), before the tray.
        internal System.Collections.Generic.List<Point> Occupied = new System.Collections.Generic.List<Point>();
        internal bool Vertical { get { return Bar.Height > Bar.Width; } }
        internal DateTime MeasuredAt;
        internal string Error;
    }

    // Which end of the taskbar the user dragged the indicator to: End = next to the tray (default),
    // Start = the left edge (top on vertical taskbars).
    internal enum IndicatorAnchor { End, Start }

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
            if (bar.Width <= bar.Height) { slot = Rectangle.Empty; return false; }
            return TryPlace(bar, tray, new[] { new Point(bar.Left, occupiedRight) }, contentWidth, height, gap, out slot);
        }
        // Finds every free gap between measured taskbar items (Start, apps, Widgets...) along the bar's
        // main axis. Prefers the gap next to the tray when the whole indicator fits there; otherwise the
        // largest gap that fits completely (a centered taskbar leaves its biggest gap on the left); and only
        // when nothing fits, the largest usable gap as a scrolling viewport. Works for horizontal and vertical bars.
        internal static bool TryPlace(Rectangle bar, Rectangle tray, System.Collections.Generic.IEnumerable<Point> occupied, int length, int thickness, int gap, out Rectangle slot, IndicatorAnchor anchor = IndicatorAnchor.End)
        {
            slot = Rectangle.Empty;
            bool vertical = bar.Height > bar.Width;
            int cross = vertical ? bar.Width : bar.Height;
            if (tray.IsEmpty || !bar.IntersectsWith(tray) || length <= 0 || thickness <= 0 || thickness > cross) return false;
            int start = vertical ? bar.Top : bar.Left, end = vertical ? tray.Top : tray.Left;
            if (end <= start) return false;
            var spans = new System.Collections.Generic.List<Point>();
            if (occupied != null) foreach (var span in occupied)
            {
                int a = Math.Max(start, span.X), b = Math.Min(end, span.Y);
                if (b > a) spans.Add(new Point(a, b));
            }
            spans.Sort(delegate(Point x, Point y) { return x.X.CompareTo(y.X); });
            var gaps = new System.Collections.Generic.List<Point>();
            int cursor = start;
            foreach (var span in spans)
            {
                if (span.X > cursor) gaps.Add(new Point(cursor, span.X));
                cursor = Math.Max(cursor, span.Y);
            }
            gaps.Add(new Point(cursor, end));
            int middle = start + (end - start) / 2;
            int chosen = -1; bool fits = false;
            bool fromStart = anchor == IndicatorAnchor.Start;
            if (fromStart)
            {
                // Dragged to the left/top: the first gap from that edge where everything fits.
                for (int index = 0; chosen < 0 && index < gaps.Count; index++)
                    if (Usable(gaps[index], gap) >= length) { chosen = index; fits = true; }
            }
            else if (Usable(gaps[gaps.Count - 1], gap) >= length) { chosen = gaps.Count - 1; fits = true; }
            if (chosen < 0)
                for (int index = 0; index < gaps.Count; index++)
                    if (Usable(gaps[index], gap) >= length && (chosen < 0 || Usable(gaps[index], gap) > Usable(gaps[chosen], gap))) { chosen = index; fits = true; }
            if (chosen < 0)
                for (int index = 0; index < gaps.Count; index++)
                    if (Usable(gaps[index], gap) >= thickness && (chosen < 0 || Usable(gaps[index], gap) > Usable(gaps[chosen], gap))) chosen = index;
            if (chosen < 0) return false;
            var free = gaps[chosen];
            int size = fits ? length : Usable(free, gap);
            // Gaps against the tray or in the far half hug their far edge; gaps in the near half hug the bar's edge.
            bool hugEnd = fromStart ? false : chosen == gaps.Count - 1 || free.X + (free.Y - free.X) / 2 >= middle;
            int from = hugEnd ? free.Y - gap - size : free.X + gap;
            slot = vertical ? new Rectangle(bar.Left + (bar.Width - thickness) / 2, from, thickness, size)
                : new Rectangle(from, bar.Top + (bar.Height - thickness) / 2, size, thickness);
            return true;
        }
        private static int Usable(Point gapSpan, int gap) { return gapSpan.Y - gapSpan.X - 2 * gap; }
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
            bool vertical = result.Vertical;
            foreach (AutomationElement element in controls)
            {
                var current = element.Current;
                if (current.ProcessId == pid || current.IsOffscreen) continue;
                var rect = current.BoundingRectangle;
                if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0) continue;
                if (vertical)
                {
                    if (rect.Bottom > result.Tray.Top || rect.Right <= result.Bar.Left || rect.Left >= result.Bar.Right) continue;
                    result.Occupied.Add(new Point((int)Math.Floor(rect.Top), (int)Math.Ceiling(rect.Bottom)));
                    right = Math.Max(right, (int)Math.Ceiling(rect.Bottom));
                }
                else
                {
                    if (rect.Right > result.Tray.Left || rect.Bottom <= result.Bar.Top || rect.Top >= result.Bar.Bottom) continue;
                    result.Occupied.Add(new Point((int)Math.Floor(rect.Left), (int)Math.Ceiling(rect.Right)));
                    right = Math.Max(right, (int)Math.Ceiling(rect.Right));
                }
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
