using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (Array.IndexOf(args, "--self-test") >= 0) return SelfTest();
            bool created;
            using (var instance = new Mutex(true, "Local\\Taskcode.PositioningProbe", out created))
            {
                if (!created) { MessageBox.Show("The Taskcode prototype is already running.", "Taskcode"); return 0; }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainWindow());
            }
            return 0;
        }
        private static int SelfTest()
        {
            var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "self-test.txt");
            try
            {
                Rectangle slot;
                var bar = new Rectangle(0, 1032, 1920, 48);
                var tray = new Rectangle(1650, 1032, 270, 48);
                Assert(Placement.TrySlot(bar, tray, 1200, 248, 34, 8, out slot), "Available gap must fit.");
                Assert(slot.Right == 1642 && slot.Top == 1039, "Slot must align immediately before the tray.");
                Assert(!Placement.TrySlot(bar, tray, 1500, 248, 34, 8, out slot), "Crowded taskbar must hide the indicator.");
                Assert(!Placement.TrySlot(bar, Rectangle.Empty, 1200, 248, 34, 8, out slot), "Missing tray must refuse placement.");
                Assert(!Placement.TrySlot(new Rectangle(0, 0, 48, 1080), tray, 0, 248, 34, 8, out slot), "Vertical taskbars are not supported by this probe.");
                Assert(Placement.TrySlot(new Rectangle(-1920, 0, 1920, 48), new Rectangle(-270, 0, 270, 48), -1000, 248, 34, 8, out slot) && slot.Left == -526, "Negative monitor coordinates must work.");
                Assert(Placement.TrySlot(new Rectangle(0, 1560, 2880, 60), new Rectangle(2475, 1560, 405, 60), 1800, 310, 43, 10, out slot) && slot.Right == 2465, "Scaled coordinates must preserve the tray gap.");
                var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var period = new QuotaPeriod { StartUtc = start, EndUtc = start.AddHours(5), Remaining = 50 };
                Assert(period.ElapsedAt(start.AddHours(-1)) == 0, "Time before reset must clamp to the start.");
                Assert(period.ElapsedAt(start) == 0, "Time marker must start at zero on reset.");
                Assert(period.ElapsedAt(start.AddHours(2.5)) == 0.5, "Half the period must put the time marker in the middle.");
                Assert(period.ElapsedAt(start.AddHours(5)) == 1, "Time marker must reach the end at renewal.");
                Assert(period.ElapsedAt(start.AddHours(6)) == 1, "Expired periods must clamp to the end.");
                Assert(period.PaceAt(start.AddHours(2.5)) == "On budget pace", "Matching consumption and time must be on pace.");
                period.Remaining = 20;
                Assert(period.PaceAt(start.AddHours(2.5)) == "Above budget pace", "Faster consumption must be above budget pace.");
                period.Remaining = 80;
                Assert(period.PaceAt(start.AddHours(2.5)) == "Below budget pace", "Slower consumption must be below budget pace.");
                Assert(period.ElapsedAt(DateTime.SpecifyKind(start, DateTimeKind.Unspecified)) == null, "Ambiguous timestamps must not generate a marker.");
                period.EndUtc = start;
                Assert(period.ElapsedAt(start) == null, "Invalid period bounds must hide the marker.");
                SpeedChecks.Run();
                RenderChecks.Run(AppDomain.CurrentDomain.BaseDirectory);
                File.WriteAllText(output, "PASS: 7 placement assertions.\nPASS: 10 time-progress and budget-pace assertions.\nPASS: 26 recent-speed, projection, and sequential-fade assertions.\nPASS: all 3 styles in light/dark palettes, transparent backgrounds, visible vector/text content, and palette contrast.\n");
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(output, "FAIL: " + ex.Message); return 1; }
        }
        private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
