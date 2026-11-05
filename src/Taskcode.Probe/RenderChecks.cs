using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Taskcode.Probe
{
    internal static class RenderChecks
    {
        internal static void Run(string directory)
        {
            using (var board = new Bitmap(960, 350))
            using (var canvas = Graphics.FromImage(board))
            using (var label = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                for (int column = 0; column < 2; column++)
                {
                    var theme = TaskbarTheme.Create(column == 0);
                    using (var background = new SolidBrush(theme.PreviewBackground)) canvas.FillRectangle(background, column * 480, 0, 480, 350);
                    using (var text = new SolidBrush(theme.Text)) canvas.DrawString(theme.Dark ? "Dark taskbar / 125%" : "Light taskbar / 125%", label, text, column * 480 + 20, 14);
                    if (Contrast(theme.Text, theme.PreviewBackground) < 4.5 || Contrast(theme.Claude, theme.PreviewBackground) < 3 || Contrast(theme.Codex, theme.PreviewBackground) < 3)
                        throw new InvalidOperationException("Taskbar palette contrast is insufficient.");
                    foreach (IndicatorStyle style in Enum.GetValues(typeof(IndicatorStyle)))
                    {
                        var size = new Size((int)Math.Round(IndicatorView.LogicalWidth(style) * 1.25), (int)Math.Round(IndicatorView.LogicalHeight * 1.25));
                        using (var surface = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb))
                        {
                            using (var graphics = Graphics.FromImage(surface)) IndicatorView.Draw(graphics, size, style, theme);
                            if (surface.GetPixel(0, 0).A != 0 || surface.GetPixel(surface.Width - 1, surface.Height - 1).A != 0)
                                throw new InvalidOperationException("Indicator background must remain fully transparent.");
                            int painted = 0;
                            for (int y = 0; y < surface.Height; y++) for (int x = 0; x < surface.Width; x++) if (surface.GetPixel(x, y).A > 0) painted++;
                            if (painted < 100) throw new InvalidOperationException("Indicator content failed to render.");
                            int top = 52 + (int)style * 94;
                            using (var text = new SolidBrush(theme.Muted)) canvas.DrawString(style.ToString(), label, text, column * 480 + 20, top);
                            canvas.DrawImageUnscaled(surface, column * 480 + 20, top + 17);
                        }
                    }
                }
                board.Save(Path.Combine(directory, "appearance-check.png"), ImageFormat.Png);
            }
        }
        private static double Contrast(Color a, Color b)
        {
            double first = Luminance(a), second = Luminance(b);
            return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
        }
        private static double Luminance(Color value) { return 0.2126 * Linear(value.R) + 0.7152 * Linear(value.G) + 0.0722 * Linear(value.B); }
        private static double Linear(byte value) { double s = value / 255.0; return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
    }
}
