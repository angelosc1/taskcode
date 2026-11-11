using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Taskcode.Probe
{
    internal static class AtomChecks
    {
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static Bitmap Render(IndicatorStyle style, bool weekly, int scale)
        {
            var size = new Size(IndicatorView.LogicalWidth(style, weekly) * scale, IndicatorView.LogicalHeight * scale);
            var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(bitmap)) { graphics.Clear(Color.FromArgb(28, 29, 32)); IndicatorView.Draw(graphics, size, style, TaskbarTheme.Create(true), weekly); }
            return bitmap;
        }
        private static int Differences(Bitmap a, Bitmap b)
        {
            int count = 0;
            for (int y = 0; y < a.Height; y++) for (int x = 0; x < a.Width; x++) if (a.GetPixel(x, y) != b.GetPixel(x, y)) count++;
            return count;
        }
        internal static void Run(string directory)
        {
            var marker = IndicatorView.Marker; bool atom = IndicatorView.ShowAtom;
            try
            {
                IndicatorView.Marker = TimeMarker.None; IndicatorView.ShowAtom = true;
                Check(new Settings().UsageAtom, "The usage atom is on by default.");
                Check(IndicatorView.ElectronCount(1) == 2 && IndicatorView.ElectronCount(2) == 4 && IndicatorView.ElectronCount(3) == 6 && IndicatorView.ElectronCount(0) == 0,
                    "Usage levels 1, 2 and 3 must show 2, 4 and 6 electrons; idle shows none.");
                LogoMotion.SetLevelForChecks(0, 0);
                string idleKey = LogoMotion.Key;
                LogoMotion.SetLevelForChecks(0, 2);
                Check(LogoMotion.Key != idleKey, "A level change must redraw the cached surface even with animations off.");
                LogoMotion.SetLevelForChecks(0, 0);
                var styles = (IndicatorStyle[])Enum.GetValues(typeof(IndicatorStyle));
                const int scale = 3;
                int rowHeight = IndicatorView.LogicalHeight * scale + 6, columnWidth = 0;
                foreach (var style in styles) columnWidth = Math.Max(columnWidth, IndicatorView.LogicalWidth(style, true) * scale + 12);
                using (var board = new Bitmap(columnWidth * 4, rowHeight * styles.Length * 2))
                using (var graphics = Graphics.FromImage(board))
                {
                    graphics.Clear(Color.FromArgb(18, 19, 21));
                    for (int row = 0; row < styles.Length * 2; row++)
                    {
                        var style = styles[row / 2]; bool weekly = row % 2 == 1;
                        Bitmap idle = null;
                        for (int level = 0; level <= 3; level++)
                        {
                            LogoMotion.SetLevelForChecks(0, level); LogoMotion.SetLevelForChecks(1, level);
                            var image = Render(style, weekly, scale);
                            graphics.DrawImageUnscaled(image, level * columnWidth, row * rowHeight);
                            if (level == 0) idle = image;
                            else { Check(Differences(idle, image) > 40, "Electrons must be visible in " + style + " at level " + level); image.Dispose(); }
                        }
                        idle.Dispose();
                    }
                    board.Save(Path.Combine(directory, "atom-levels.png"), ImageFormat.Png);
                }
                // Off keeps only the spinning logo: no electrons, full-size logo, while the provider works.
                LogoMotion.SetLevelForChecks(0, 3); LogoMotion.SetLevelForChecks(1, 3);
                IndicatorView.ShowAtom = false;
                using (var plain = Render(IndicatorStyle.Essential, true, scale))
                {
                    Check(LogoMotion.Key.Length > 0, "Levels stay tracked for the logo spin when the atom is off.");
                    IndicatorView.ShowAtom = true;
                    using (var orbiting = Render(IndicatorStyle.Essential, true, scale)) Check(Differences(plain, orbiting) > 40, "Turning the usage atom off must remove the electrons.");
                }
            }
            finally
            {
                IndicatorView.Marker = marker; IndicatorView.ShowAtom = atom;
                for (int provider = 0; provider < 2; provider++) LogoMotion.SetLevelForChecks(provider, 0);
            }
        }
    }
}
