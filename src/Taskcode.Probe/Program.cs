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
            if (Array.IndexOf(args, "--tray-test") >= 0) return TraySelfTest();
            if (Array.IndexOf(args, "--accounts-check") >= 0) return AccountChecks.Probe(false);
            if (Array.IndexOf(args, "--activity-check") >= 0) return AccountChecks.ProbeActivity();
            if (Array.IndexOf(args, "--connect-claude") >= 0) return AccountChecks.Probe(true);
            if (Array.IndexOf(args, "--exit") >= 0)
            {
                var running = Native.FindWindow(null, MainWindow.WindowTitle);
                if (running != IntPtr.Zero) Native.PostMessage(running, MainWindow.ExitMessage, IntPtr.Zero, IntPtr.Zero);
                return 0;
            }
            bool startInTray = Array.IndexOf(args, "--tray") >= 0;
            bool created;
            using (var instance = new Mutex(true, "Local\\Taskcode.PositioningProbe", out created))
            {
                if (!created)
                {
                    var running = Native.FindWindow(null, MainWindow.WindowTitle);
                    if (!startInTray && running != IntPtr.Zero) Native.PostMessage(running, MainWindow.OpenSettingsMessage, IntPtr.Zero, IntPtr.Zero);
                    return 0;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainWindow(startInTray, Array.IndexOf(args, "--demo") < 0));
            }
            return 0;
        }
        private static int TraySelfTest()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool success = false;
            string result = "FAIL: tray lifecycle test did not run.";
            bool startupBefore = StartupRegistration.IsEnabled(Application.ExecutablePath);
            var window = new MainWindow(true, false) { StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000) };
            using (var check = new System.Windows.Forms.Timer { Interval = 200 })
            {
                check.Tick += delegate
                {
                    check.Stop();
                    try
                    {
                        Assert(!window.Visible && !window.ShowInTaskbar && window.TrayVisible, "Startup must keep settings hidden and the tray icon available.");
                        window.OpenSettings();
                        Assert(window.Visible && !window.ShowInTaskbar, "Settings must open without creating a taskbar button.");
                        Assert((Native.GetStyle(window.Handle, -20).ToInt64() & 0x40000L) == 0, "The native settings window must not request a taskbar app button.");
                        foreach (Control control in window.Controls)
                        {
                            var startup = control as CheckBox;
                            if (startup != null && startup.Text == "Start with Windows")
                                Assert(startup.Checked == startupBefore, "Startup checkbox must reflect the existing registration without enabling it.");
                        }
                        using (var preview = new Bitmap(window.Width, window.Height))
                        {
                            window.DrawToBitmap(preview, new Rectangle(Point.Empty, preview.Size));
                            preview.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings-check.png"), System.Drawing.Imaging.ImageFormat.Png);
                        }
                        using (var icon = window.Icon.ToBitmap())
                            icon.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tray-icon-check.png"), System.Drawing.Imaging.ImageFormat.Png);
                        window.ShowDetails(true, true, 0);
                        Assert(window.VisibleDetailProvider == 0 && window.VisibleDetailCount == 1, "Selecting Claude must open only Claude details.");
                        window.ShowDetails(true, true, 1);
                        Assert(window.VisibleDetailProvider == 1 && window.VisibleDetailCount == 1, "Selecting Codex must replace Claude details without showing both.");
                        window.Close();
                        Assert(!window.Visible && !window.IsDisposed && window.TrayVisible, "Closing settings must hide them while keeping the tray application alive.");
                        Assert(startupBefore == StartupRegistration.IsEnabled(Application.ExecutablePath), "Opening and closing the app must not enable startup.");
                        Assert(StartupRegistration.CommandFor(@"C:\Program Files\Taskcode\Taskcode.exe") == "\"C:\\Program Files\\Taskcode\\Taskcode.exe\" --tray", "Startup must quote paths containing spaces and launch in tray mode.");
                        result = "PASS: tray lifecycle, unchanged startup registration, quoted startup command, and isolated Claude/Codex details.";
                        success = true;
                    }
                    catch (Exception ex) { result = "FAIL: " + ex.Message; }
                    finally { window.RequestExit(); }
                };
                check.Start();
                Application.Run(window);
            }
            Assert(window.IsDisposed, "Exit must dispose the application window.");
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tray-test.txt"), result);
            return success ? 0 : 1;
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
                foreach (IndicatorStyle style in Enum.GetValues(typeof(IndicatorStyle)))
                foreach (bool weekly in new[] { false, true })
                {
                    var size = new Size((int)Math.Round(IndicatorView.LogicalWidth(style, weekly) * 1.25), 50);
                    Assert(IndicatorView.ProviderAt(new Point(4, 25), size, style, weekly) == 0, "The Claude region must route to Claude in every layout.");
                    Assert(IndicatorView.ProviderAt(new Point(size.Width - 4, 25), size, style, weekly) == 1, "The Codex region must route to Codex in every layout.");
                    Assert(IndicatorView.ProviderAt(new Point(size.Width + 1, 25), size, style, weekly) == -1, "Points outside the indicator must not route to a provider.");
                    var left = IndicatorView.ProviderBounds(0, size, style, weekly);
                    var right = IndicatorView.ProviderBounds(1, size, style, weekly);
                    Assert(left.Right == right.Left && left.Width + right.Width == size.Width, "Provider regions must partition the displayed layout without overlap.");
                }
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
                AccountChecks.Run();
                ActivityChecks.Run(AppDomain.CurrentDomain.BaseDirectory);
                RenderChecks.Run(AppDomain.CurrentDomain.BaseDirectory);
                File.WriteAllText(output, "PASS: 7 placement assertions.\nPASS: provider routing in all 3 styles, with weekly off/on and scaled coordinates.\nPASS: 10 time-progress and budget-pace assertions.\nPASS: 26 recent-speed, projection, and sequential-fade assertions.\nPASS: account parsing, missing/expired windows, fractional readings, privacy, provider isolation, stale data, and no synthetic live observations.\nPASS: activity lifecycle, multiple sessions, incremental records, hook preservation, and staggered transparent Ripple rendering.\nPASS: all 3 styles in light/dark palettes, transparent backgrounds, visible vector/text content, and palette contrast.\n");
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(output, "FAIL: " + ex.Message); return 1; }
        }
        private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
