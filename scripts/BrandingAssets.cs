using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class BrandingAssets
{
    private static void Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Expected source PNG and output directory.");
        Directory.CreateDirectory(args[1]);
        using (var source = new Bitmap(args[0]))
        using (var normalized = new Bitmap(512, 512, PixelFormat.Format32bppArgb))
        {
            int left = source.Width, top = source.Height, right = -1, bottom = -1;
            for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
                if (source.GetPixel(x, y).A > 0)
                { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
            if (right < left) throw new InvalidDataException("The icon is fully transparent.");
            float scale = 460f / Math.Max(right - left + 1, bottom - top + 1);
            float width = (right - left + 1) * scale, height = (bottom - top + 1) * scale;
            using (var graphics = Graphics.FromImage(normalized))
            {
                Configure(graphics);
                graphics.DrawImage(source, new RectangleF((512 - width) / 2, (512 - height) / 2, width, height), new RectangleF(left, top, right - left + 1, bottom - top + 1), GraphicsUnit.Pixel);
            }
            normalized.Save(Path.Combine(args[1], "taskcode.png"), ImageFormat.Png);
            var sizes = new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
            var frames = new List<byte[]>();
            foreach (int size in sizes)
            using (var frame = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            using (var stream = new MemoryStream())
            {
                using (var graphics = Graphics.FromImage(frame)) { Configure(graphics); graphics.DrawImage(normalized, new Rectangle(0, 0, size, size)); }
                if (size == 256) frame.Save(stream, ImageFormat.Png);
                else WriteDib(frame, stream);
                frames.Add(stream.ToArray());
            }
            using (var writer = new BinaryWriter(File.Create(Path.Combine(args[1], "taskcode.ico"))))
            {
                writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
                int offset = 6 + sizes.Length * 16;
                for (int index = 0; index < sizes.Length; index++)
                {
                    writer.Write((byte)(sizes[index] == 256 ? 0 : sizes[index])); writer.Write((byte)(sizes[index] == 256 ? 0 : sizes[index]));
                    writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
                    writer.Write((uint)frames[index].Length); writer.Write((uint)offset); offset += frames[index].Length;
                }
                foreach (var frame in frames) writer.Write(frame);
            }
            using (var icon = new Icon(Path.Combine(args[1], "taskcode.ico"), new Size(32, 32)))
            using (var actual = icon.ToBitmap())
            using (var expected = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(expected)) { Configure(graphics); graphics.DrawImage(normalized, new Rectangle(0, 0, 32, 32)); }
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    var target = expected.GetPixel(x, y); var pixel = actual.GetPixel(x, y);
                    if (target.A == 255 && (pixel.A < 250 || Math.Abs(target.R - pixel.R) + Math.Abs(target.G - pixel.G) + Math.Abs(target.B - pixel.B) > 6))
                        throw new InvalidDataException("The Windows tray icon did not preserve the supplied artwork.");
                }
            }
        }
        Console.WriteLine("Prepared Taskcode branding: transparent PNG and 9-size Windows ICO.");
    }
    private static void Configure(Graphics graphics)
    {
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }
    private static void WriteDib(Bitmap bitmap, Stream stream)
    {
        int size = bitmap.Width, stride = ((size + 31) / 32) * 4;
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write(40); writer.Write(size); writer.Write(size * 2);
            writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(0);
            writer.Write(size * size * 4 + stride * size);
            writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
            for (int y = size - 1; y >= 0; y--)
            for (int x = 0; x < size; x++)
            { var pixel = bitmap.GetPixel(x, y); writer.Write(pixel.B); writer.Write(pixel.G); writer.Write(pixel.R); writer.Write(pixel.A); }
            for (int y = size - 1; y >= 0; y--)
            {
                var mask = new byte[stride];
                for (int x = 0; x < size; x++) if (bitmap.GetPixel(x, y).A == 0) mask[x / 8] |= (byte)(128 >> (x % 8));
                writer.Write(mask);
            }
        }
    }
}
