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
                RenderChecks.Run(AppDomain.CurrentDomain.BaseDirectory);
                File.WriteAllText(output, "PASS: 7 placement assertions.\nPASS: all 3 styles in light/dark palettes, transparent backgrounds, visible vector/text content, and palette contrast.\n");
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(output, "FAIL: " + ex.Message); return 1; }
        }
        private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
