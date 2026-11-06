using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace Taskcode.Probe
{
    internal static class ProviderLogoChecks
    {
        internal static void Run(string directory)
        {
            var usage = new UsageDemo { OptionalProviders = ProviderCatalog.Create() };
            foreach (var provider in usage.OptionalProviders) provider.Enabled = true;
            foreach (float scale in new[] { 1.25f, 1.5f, 2f })
            {
                int rowHeight = (int)Math.Round(70 * scale);
                int width = 0;
                foreach (IndicatorStyle style in Enum.GetValues(typeof(IndicatorStyle)))
                    width = Math.Max(width, (int)Math.Round(IndicatorView.LogicalWidth(style, false, usage) * scale));
                using (var board = new Bitmap(width, rowHeight * 7))
                using (var canvas = Graphics.FromImage(board))
                {
                    canvas.Clear(TaskbarTheme.Create(true).PreviewBackground);
                    foreach (IndicatorStyle style in Enum.GetValues(typeof(IndicatorStyle)))
                    {
                        var size = new Size((int)Math.Round(IndicatorView.LogicalWidth(style, false, usage) * scale), (int)Math.Round(IndicatorView.LogicalHeight * scale));
                        using (var bitmap = new Bitmap(size.Width, size.Height))
                        using (var graphics = Graphics.FromImage(bitmap))
                        {
                            IndicatorView.Draw(graphics, size, style, TaskbarTheme.Create(true), false, 0, usage);
                            canvas.DrawImageUnscaled(bitmap, 0, (int)style * rowHeight);
                        }
                    }
                    board.Save(Path.Combine(directory, "provider-logos-" + (int)(scale * 100) + ".png"));
                }
                for (int index = 0; index < ProviderCatalog.Count; index++)
                {
                    int side = (int)Math.Round(16 * scale);
                    using (var bitmap = new Bitmap(side + 4, side + 4))
                    using (var graphics = Graphics.FromImage(bitmap))
                    using (var brush = new SolidBrush(usage.OptionalProviders[index].Color))
                    {
                        graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        BrandMarks.DrawProvider(graphics, brush, index, new RectangleF(2, 2, side, side));
                        int painted = 0;
                        for (int y = 0; y < bitmap.Height; y++)
                        for (int x = 0; x < bitmap.Width; x++) if (bitmap.GetPixel(x, y).A > 0) painted++;
                        if (painted < side) throw new InvalidOperationException("Provider logo is empty at display scale: " + ProviderCatalog.Names[index]);
                    }
                }
            }
        }
    }
}
