using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Taskcode.Probe
{
    internal static class InputChecks
    {
        private sealed class Host : Form
        {
            internal int ContextMessages;
            protected override void WndProc(ref Message message)
            {
                if (message.Msg == 0x007B) ContextMessages++;
                base.WndProc(ref message);
            }
        }
        internal static void Run()
        {
            foreach (IndicatorStyle style in Enum.GetValues(typeof(IndicatorStyle)))
            foreach (bool weekly in new[] { false, true })
            foreach (bool dark in new[] { false, true })
            using (var bitmap = new Bitmap(IndicatorView.LogicalWidth(style, weekly), IndicatorView.LogicalHeight, PixelFormat.Format32bppPArgb))
            {
                using (var graphics = Graphics.FromImage(bitmap)) IndicatorWindow.DrawInputSurface(graphics, bitmap.Size, style, TaskbarTheme.Create(dark), weekly, 0, null, 0);
                for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++)
                    Check(bitmap.GetPixel(x, y).A > 0, "Empty pixels must retain the indicator's mouse hit area.");
                Check(bitmap.GetPixel(0, 0).A == 1 && bitmap.GetPixel(bitmap.Width - 1, bitmap.Height - 1).A == 1, "Input coverage must not introduce a visible background.");
            }
            using (var host = new Host { Location = new Point(-30000, -30000), StartPosition = FormStartPosition.Manual })
            using (var indicator = new IndicatorWindow { Size = new Size(250, 40), Theme = TaskbarTheme.Create(true) })
            {
                host.Show();
                int requests = 0;
                indicator.ContextRequested += delegate { requests++; };
                foreach (bool embedded in new[] { false, true })
                {
                    long style = Native.GetStyle(indicator.Handle, Native.GwlStyle).ToInt64();
                    Native.SetStyle(indicator.Handle, Native.GwlStyle, new IntPtr(embedded ? (style & ~Native.Popup) | Native.Child : (style & ~Native.Child) | Native.Popup));
                    Native.SetParent(indicator.Handle, embedded ? host.Handle : IntPtr.Zero);
                    indicator.Location = new Point(-30000, -30000);
                    indicator.Show(); indicator.RenderAlpha();
                    int before = requests;
                    Native.SendMessage(indicator.Handle, 0x007B, indicator.Handle, new IntPtr(-1));
                    Check(requests == before + 1 && host.ContextMessages == 0, "Keyboard context-menu requests must be handled without reaching the parent.");
                    before = requests;
                    Native.SendMessage(indicator.Handle, 0x0204, new IntPtr(2), new IntPtr(0x00050005));
                    Native.SendMessage(indicator.Handle, 0x0205, IntPtr.Zero, new IntPtr(0x00050005));
                    Check(requests == before + 1 && host.ContextMessages == 0, "Right-click must open Taskcode's menu exactly once without forwarding to the host.");
                    Check(Native.IsWindowVisible(indicator.Handle), "Right-click must not hide the indicator.");
                    Check(Native.SendMessage(indicator.Handle, 0x0021, host.Handle, IntPtr.Zero).ToInt32() == 3, "Mouse input must not activate the native host.");
                    indicator.InvalidateSurface(); Check(indicator.RenderAlpha(), "The surface must redraw after menu dismissal.");
                }
                host.Hide();
            }
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
