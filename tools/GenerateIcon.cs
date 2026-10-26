using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class GenerateIcon
{
    private static int Main(string[] args)
    {
        if (args.Length != 1) { Console.Error.WriteLine("Specify the assets directory."); return 1; }
        string folder = Path.GetFullPath(args[0]); Directory.CreateDirectory(folder);
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        var images = new List<byte[]>();
        foreach (int size in sizes)
            using (Bitmap bitmap = Draw(size))
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png); images.Add(stream.ToArray());
                if (size == 256) bitmap.Save(Path.Combine(folder, "LegacyCrosshair.png"), ImageFormat.Png);
            }
        using (var writer = new BinaryWriter(File.Create(Path.Combine(folder, "LegacyCrosshair.ico"))))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset = 6 + sizes.Length * 16;
            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(images[i].Length); writer.Write(offset); offset += images[i].Length;
            }
            foreach (byte[] image in images) writer.Write(image);
        }
        Console.WriteLine("Generated 9 icon sizes and a 256px preview in " + folder); return 0;
    }
    private static int Pixel(int size, double value) { return (int)Math.Round(size * value / 256, MidpointRounding.AwayFromZero); }
    private static Bitmap Draw(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
            float inset = size / 16f, radius = size * 52f / 256, diameter = radius * 2;
            float edge = size - inset, extent = edge - inset;
            using (var path = new GraphicsPath())
            using (var blue = new LinearGradientBrush(new PointF(0, inset), new PointF(0, edge), Color.FromArgb(77, 125, 255), Color.FromArgb(47, 85, 217)))
            {
                path.AddArc(inset, inset, diameter, diameter, 180, 90);
                path.AddArc(edge - diameter, inset, diameter, diameter, 270, 90);
                path.AddArc(edge - diameter, edge - diameter, diameter, diameter, 0, 90);
                path.AddArc(inset, edge - diameter, diameter, diameter, 90, 90); path.CloseFigure();
                g.FillPath(blue, path);
            }
            // Snap small icons to a shared integer grid to keep both sides crisp.
            int thickness = Math.Max(2, Pixel(size, 24));
            if ((thickness & 1) != 0) thickness++;
            int band = (size - thickness) / 2, gap = Math.Max(1, Pixel(size, 12)), length = Math.Max(2, Pixel(size, 48));
            g.SmoothingMode = SmoothingMode.None;
            g.FillRectangle(Brushes.White, band, band - gap - length, thickness, length);
            g.FillRectangle(Brushes.White, band, band + thickness + gap, thickness, length);
            g.FillRectangle(Brushes.White, band - gap - length, band, length, thickness);
            g.FillRectangle(Brushes.White, band + thickness + gap, band, length, thickness);
        }
        return bitmap;
    }
}
