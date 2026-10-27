using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

internal static class AnnotateScreenshots
{
    private static readonly Color Ink = Color.FromArgb(26, 38, 58);
    private static readonly Color Muted = Color.FromArgb(99, 113, 135);
    private static readonly Color Blue = Color.FromArgb(40, 98, 218);
    private static readonly Color Amber = Color.FromArgb(177, 116, 0);

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1) throw new ArgumentException("Specify the screenshots directory.");
            string folder = Path.GetFullPath(args[0]);
            Render(folder, false);
            Render(folder, true);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Render(string folder, bool scope)
    {
        string input = scope ? "scope-center-reference.png" : "cs2-crosshair-closeup.png";
        string output = scope ? "scope-alignment-annotated.png" : "cs2-crosshair-annotated.png";
        using (var source = new Bitmap(Path.Combine(folder, input)))
        using (var canvas = new Bitmap(1200, 720, PixelFormat.Format32bppArgb))
        {
            canvas.SetResolution(96, 96);
            int overviewScale = scope ? 2 : 3;
            int zoomScale = scope ? 12 : 16;
            Rectangle crop = scope ? new Rectangle(111, 69, 22, 26) : new Rectangle(60, 45, 20, 20);
            Rectangle overview = new Rectangle(40, 180, source.Width * overviewScale, source.Height * overviewScale);
            Rectangle zoom = new Rectangle(scope ? 852 : 824, 196, crop.Width * zoomScale, crop.Height * zoomScale);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                Text(g, scope ? "02  工具准星与狙击镜中心对齐" : "01  原生准星与工具准星存在偏移", 40, 30, 36, Ink, true);
                Text(g, scope ? "白色：Legacy Crosshair 工具准星    黑色：狙击镜中心线" : "黄色：游戏原生准星    白色：Legacy Crosshair 工具准星", 40, 90, 21, Muted, false);
                Text(g, "原始画面 · " + overviewScale + "×", 40, 143, 20, Muted, false);
                DrawSource(g, source, new Rectangle(Point.Empty, source.Size), overview);
                DrawSource(g, source, crop, zoom);

                if (scope)
                {
                    float axisX = zoom.Left + (121.5f - crop.Left) * zoomScale;
                    Text(g, "狙击镜中心线（黑色）", 824, 131, 21, Ink, true);
                    Arrow(g, Ink, new[] { new PointF(axisX, 162), new PointF(axisX, zoom.Top - 8) });
                    Callout(g, "工具准星", "白色 · 中心与黑线对齐", 592, 278, Blue);
                    float armY = zoom.Top + (82.5f - crop.Top) * zoomScale;
                    Arrow(g, Blue, new[] { new PointF(592, 340), new PointF(802, 340), new PointF(zoom.Left - 8, armY) });
                }
                else
                {
                    Callout(g, "游戏原生准星", "黄色 · 与白色中心错开", 568, 249, Amber);
                    float nativeArmY = zoom.Top + (52.5f - crop.Top) * zoomScale;
                    Arrow(g, Amber, new[] { new PointF(568, 309), new PointF(788, 309), new PointF(zoom.Left - 8, nativeArmY) });
                    Callout(g, "工具准星", "白色 · 独立覆盖层", 568, 390, Blue);
                    float toolArmY = zoom.Top + (54.5f - crop.Top) * zoomScale;
                    Arrow(g, Blue, new[] { new PointF(568, 379), new PointF(788, 379), new PointF(zoom.Left - 8, toolArmY) });
                }

                Text(g, "中心局部 · " + zoomScale + "×", zoom.Left, zoom.Bottom + 18, 20, Muted, false);
                using (var fill = new SolidBrush(scope ? Color.FromArgb(237, 244, 255) : Color.FromArgb(255, 248, 229)))
                using (GraphicsPath path = Rounded(new RectangleF(40, 580, 1104, 70), 12))
                    g.FillPath(fill, path);
                Text(g, scope ? "对齐：白色工具准星的中心与黑色狙击镜中心线重合。" : "偏移：黄色原生准星与白色工具准星的中心没有重合。", 62, 598, 24, scope ? Blue : Amber, true);
                Text(g, "整数倍最近邻放大 · 图外标注 · 原始画面与局部像素保持不变", 40, 674, 18, Muted, false);
            }

            // Verify the finished image, including all annotations. No arrow, text,
            // smoothing or border may overwrite a screenshot or enlarged pixel.
            VerifyPixels(source, canvas, new Rectangle(Point.Empty, source.Size), overview, overviewScale);
            VerifyPixels(source, canvas, crop, zoom, zoomScale);
            canvas.Save(Path.Combine(folder, output), ImageFormat.Png);
            Console.WriteLine("PASS original and enlarged pixels unchanged: " + output);
        }
    }

    private static void DrawSource(Graphics g, Bitmap source, Rectangle crop, Rectangle destination)
    {
        GraphicsState saved = g.Save();
        g.CompositingMode = CompositingMode.SourceCopy;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.SmoothingMode = SmoothingMode.None;
        g.DrawImage(source, destination, crop, GraphicsUnit.Pixel);
        g.Restore(saved);
        using (var border = new Pen(Color.FromArgb(217, 225, 236)))
            g.DrawRectangle(border, destination.Left - 2, destination.Top - 2, destination.Width + 3, destination.Height + 3);
    }

    private static void VerifyPixels(Bitmap source, Bitmap result, Rectangle crop, Rectangle destination, int scale)
    {
        for (int y = 0; y < destination.Height; y++)
            for (int x = 0; x < destination.Width; x++)
                if (source.GetPixel(crop.Left + x / scale, crop.Top + y / scale).ToArgb() != result.GetPixel(destination.Left + x, destination.Top + y).ToArgb())
                    throw new InvalidDataException("An annotation or scaling changed a source pixel at " + x + ", " + y + ".");
    }

    private static void Callout(Graphics g, string title, string note, float x, float y, Color color)
    {
        using (var dot = new SolidBrush(color)) g.FillEllipse(dot, x, y + 8, 10, 10);
        Text(g, title, x + 20, y, 23, color, true);
        Text(g, note, x, y + 33, 17, Muted, false);
    }

    private static void Arrow(Graphics g, Color color, PointF[] points)
    {
        using (var arrow = new AdjustableArrowCap(4, 5, true))
        using (var pen = new Pen(color, 2.4f))
        { pen.CustomEndCap = arrow; pen.LineJoin = LineJoin.Round; g.DrawLines(pen, points); }
    }

    private static void Text(Graphics g, string value, float x, float y, float size, Color color, bool bold)
    {
        using (var font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(color))
        using (var format = new StringFormat(StringFormat.GenericTypographic))
            g.DrawString(value, font, brush, new PointF(x, y), format);
    }

    private static GraphicsPath Rounded(RectangleF rect, float radius)
    {
        var path = new GraphicsPath(); float diameter = radius * 2;
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90); path.CloseFigure(); return path;
    }
}
