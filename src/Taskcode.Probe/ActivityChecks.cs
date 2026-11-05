using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;

namespace Taskcode.Probe
{
    internal static class ActivityChecks
    {
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        internal static void Run(string directory)
        {
            DateTime now = DateTime.UtcNow;
            var state = new ActivityState();
            state.Observe("task_started", now); Check(state.IsActive(now.AddSeconds(1)), "A turn start must activate Ripple.");
            state.Observe("token_count", now.AddSeconds(2)); Check(state.At == now, "Quota updates must not create or extend activity.");
            state.Observe("task_complete", now.AddSeconds(3)); Check(!state.IsActive(now.AddSeconds(4)), "Turn completion must stop Ripple.");
            state.Observe("task_started", now); Check(!state.IsActive(now.AddSeconds(4)), "An older start must not overwrite a newer stop.");
            state.Observe("UserPromptSubmit", now.AddSeconds(5)); Check(state.IsActive(now.AddSeconds(6)), "Claude prompt events must activate Ripple.");
            state.Observe("PermissionRequest", now.AddSeconds(7)); Check(!state.IsActive(now.AddSeconds(8)), "Permission waiting must stop the working animation.");
            state.Observe("PostToolUse", now.AddSeconds(9)); state.Observe("Stop", now.AddSeconds(10)); Check(!state.IsActive(now.AddSeconds(11)), "Claude stop must clear activity.");
            state.Observe("turn_started", now.AddSeconds(12)); Check(!state.IsActive(now.AddMinutes(31)), "Missing end events must expire.");
            var parsed = new ActivityState();
            LocalActivity.ObserveCodexLine(parsed, AccountJson.Encode(new { type = "event_msg", timestamp = now.ToString("o"), payload = new { type = "task_started" } }), now);
            Check(parsed.IsActive(now), "Codex lifecycle JSONL must activate activity.");
            LocalActivity.ObserveCodexLine(parsed, AccountJson.Encode(new { type = "response_item", timestamp = now.AddSeconds(1).ToString("o"), payload = new { type = "task_complete" } }), now.AddSeconds(1));
            Check(parsed.IsActive(now.AddSeconds(1)), "Message content must not spoof a lifecycle event.");
            LocalActivity.ObserveCodexLine(parsed, "partial task_complete", now); Check(parsed.IsActive(now), "Partial records must be harmless.");
            var settings = AccountJson.Parse("{\"hooks\":{\"Stop\":[{\"hooks\":[{\"type\":\"command\",\"command\":\"echo keep-me\"}]}]},\"unrelated\":true}");
            ClaudeBridge.ConfigureActivityHooks(settings, true); string once = AccountJson.Encode(settings);
            ClaudeBridge.ConfigureActivityHooks(settings, true); Check(once == AccountJson.Encode(settings), "Installing activity hooks twice must not duplicate them.");
            ClaudeBridge.ConfigureActivityHooks(settings, false); string restored = AccountJson.Encode(settings);
            Check(restored.Contains("keep-me") && restored.Contains("unrelated") && !restored.Contains("--activity-"), "Removing activity hooks must preserve user hooks and other settings.");
            Check(Math.Abs(ActivityRipple.PhaseAt(0.5, 0) - ActivityRipple.PhaseAt(0.5, 1)) > 0.49, "Ripple rings must have staggered phases.");
            string originalHome = Environment.GetEnvironmentVariable("CODEX_HOME");
            string fixtureHome = Path.Combine(directory, "activity-fixture");
            string fixtureDay = Path.Combine(fixtureHome, "sessions", now.ToString("yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture));
            Directory.CreateDirectory(fixtureDay);
            string a = Path.Combine(fixtureDay, "a.jsonl"), b = Path.Combine(fixtureDay, "b.jsonl");
            try
            {
                Environment.SetEnvironmentVariable("CODEX_HOME", fixtureHome);
                File.WriteAllText(a, Event("task_started", now.AddSeconds(-3)) + "\n");
                File.WriteAllText(b, Event("task_complete", now.AddSeconds(-2)) + "\n");
                var monitor = new LocalActivity();
                Check(monitor.Read(now, false, true) == 2, "An idle session must not stop another active session.");
                File.AppendAllText(a, Event("task_complete", now.AddSeconds(-1)));
                Check(monitor.Read(now, false, true) == 2, "Partial final records must wait for their newline.");
                File.AppendAllText(a, "\n"); Check(monitor.Read(now, false, true) == 0, "A completed incremental record must stop activity.");
                File.WriteAllText(a, Event("turn_started", now) + "\n");
                Check(monitor.Read(now, false, true) == 2, "Truncated files must recover their lifecycle state.");
                Check(monitor.Read(now, false, false) == 0, "Disconnect must clear source activity.");
            }
            finally { Environment.SetEnvironmentVariable("CODEX_HOME", originalHome); }
            using (var board = new Bitmap(960, 360))
            using (var graphics = Graphics.FromImage(board))
            using (var font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                for (int palette = 0; palette < 2; palette++)
                {
                    var theme = TaskbarTheme.Create(palette == 0);
                    using (var background = new SolidBrush(theme.PreviewBackground)) graphics.FillRectangle(background, palette * 480, 0, 480, 360);
                    foreach (IndicatorStyle style in Enum.GetValues(typeof(IndicatorStyle)))
                    for (int frame = 0; frame < 3; frame++)
                    {
                        int y = 12 + (int)style * 115 + frame * 33;
                        if (frame == 0) using (var brush = new SolidBrush(theme.Text)) graphics.DrawString(style.ToString(), font, brush, palette * 480 + 12, y);
                        using (var surface = new Bitmap(IndicatorView.LogicalWidth(style), IndicatorView.LogicalHeight))
                        {
                            using (var canvas = Graphics.FromImage(surface)) IndicatorView.Draw(canvas, surface.Size, style, theme, false, 0, null, 3, frame * 0.55);
                            graphics.DrawImageUnscaled(surface, palette * 480 + 110, y - 3);
                            Check(surface.GetPixel(0, 0).A == 0, "Ripple must preserve transparent background corners.");
                        }
                    }
                }
                board.Save(Path.Combine(directory, "ripple-check.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        private static string Event(string type, DateTime at) { return AccountJson.Encode(new { type = "event_msg", timestamp = at.ToString("o"), payload = new { type = type } }); }
    }
}
