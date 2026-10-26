using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace LegacyCrosshair
{
    public sealed class Settings
    {
        public int Version = 1;
        public bool Dynamic = false;
        public double Length = 2.5, Thickness = 0.5, Gap = -2, Outline = 1;
        public bool DrawOutline = true, Dot = false, TStyle = false;
        public int Red = 95, Green = 255, Blue = 139, Alpha = 255;
        public double Spread = 5, AccelerationMs = 110, BrakingMs = 65, RecoveryMs = 100;
        public bool Split = true;
        public double SplitDistance = 7, SplitRatio = 0.35, InnerAlpha = 1, OuterAlpha = 0.5;
        public int GameWidth = 1280, GameHeight = 960;
        public string Mapping = "stretch", Monitor = "";
        public bool Listen = true, OnlyCs2 = false, FollowCs2 = false;
        public string Background = "range";
        public bool FullPreview = false;
        public string UiTheme = "light";

        public void Validate()
        {
            if (Version != 1) throw new InvalidDataException("不支持的配置版本。");
            Length = Clamp(Length, 0, 30); Thickness = Clamp(Thickness, 0, 10);
            Gap = Clamp(Gap, -20, 30); Outline = Clamp(Outline, 0, 3);
            Red = Math.Max(0, Math.Min(255, Red)); Green = Math.Max(0, Math.Min(255, Green));
            Blue = Math.Max(0, Math.Min(255, Blue)); Alpha = Math.Max(0, Math.Min(255, Alpha));
            Spread = Clamp(Spread, 0, 30); AccelerationMs = Clamp(AccelerationMs, 10, 600);
            BrakingMs = Clamp(BrakingMs, 10, 600); RecoveryMs = Clamp(RecoveryMs, 10, 600);
            SplitDistance = Clamp(SplitDistance, 0, 50); SplitRatio = Clamp(SplitRatio, 0, 1);
            InnerAlpha = Clamp(InnerAlpha, 0, 1); OuterAlpha = Clamp(OuterAlpha, 0, 1);
            if (GameWidth < 320 || GameWidth > 16384 || GameHeight < 240 || GameHeight > 16384)
                throw new InvalidDataException("游戏分辨率须在 320×240 至 16384×16384 范围内。");
            if (Mapping != "stretch" && Mapping != "fit") Mapping = "stretch";
            if (Background != "range" && Background != "dark" && Background != "light" && Background != "grid") Background = "range";
            if (Monitor == null) Monitor = "";
            if (UiTheme != "light" && UiTheme != "dark") UiTheme = "light";
        }
        public static double Clamp(double value, double min, double max)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value)) throw new InvalidDataException("配置包含无效数字。");
            return Math.Max(min, Math.Min(max, value));
        }
        public Settings Copy() { return (Settings)MemberwiseClone(); }
    }

    public static class ConfigStore
    {
#if UI_TEST
        public static readonly string Folder = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "artifacts", "ui-test-settings"));
#else
        public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LegacyCrosshair");
#endif
        public static readonly string FilePath = Path.Combine(Folder, "settings.json");
        public static Settings Read(string path)
        {
            var info = new FileInfo(path);
            if (info.Length > 128 * 1024) throw new InvalidDataException("配置文件过大。");
            Settings value = new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(path));
            if (value == null) throw new InvalidDataException("配置为空。");
            value.Validate();
            return value;
        }
        public static void Write(string path, Settings settings)
        {
            settings.Validate();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + ".tmp";
            File.WriteAllText(temp, new JavaScriptSerializer().Serialize(settings), new System.Text.UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
    }

    public struct MovementKeys
    {
        public bool W, A, S, D;
        public MovementKeys(bool w, bool a, bool s, bool d) { W = w; A = a; S = s; D = d; }
        public bool Any { get { return W || A || S || D; } }
    }

    public sealed class MovementModel
    {
#if UI_TEST
        internal int StepCount;
#endif
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Amount { get; private set; }
        public bool NeedsUpdate { get; private set; }
        public void Reset() { X = Y = Amount = 0; NeedsUpdate = false; }
        public void Step(MovementKeys keys, double elapsed, Settings settings)
        {
#if UI_TEST
            StepCount++;
#endif
            if (!settings.Dynamic) { Reset(); return; }
            elapsed = Math.Max(0, elapsed);
            // Small fixed substeps make a short frame and a long frame follow the same motion.
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Min(elapsed, 0.25) / 0.002));
            double dt = Math.Min(elapsed, 0.25) / steps;
            double tx = (keys.D ? 1 : 0) - (keys.A ? 1 : 0);
            double ty = (keys.W ? 1 : 0) - (keys.S ? 1 : 0);
            double magnitude = Math.Sqrt(tx * tx + ty * ty);
            if (magnitude > 1) { tx /= magnitude; ty /= magnitude; }
            for (int i = 0; i < steps; i++)
            {
                X = Approach(X, tx, dt, (tx == 0 || tx * X < 0) ? settings.BrakingMs : settings.AccelerationMs);
                Y = Approach(Y, ty, dt, (ty == 0 || ty * Y < 0) ? settings.BrakingMs : settings.AccelerationMs);
                double target = Math.Min(1, Math.Sqrt(X * X + Y * Y)) * settings.Spread;
                Amount = Approach(Amount, target, dt, target < Amount ? settings.RecoveryMs : 35);
            }
            if (!keys.Any && Amount < 0.001) Reset();
            NeedsUpdate = Math.Abs(X - tx) > 0.0001 || Math.Abs(Y - ty) > 0.0001 ||
                Math.Abs(Amount - Math.Min(1, magnitude) * settings.Spread) > 0.0001;
        }
        private static double Approach(double current, double target, double dt, double ms)
        {
            return target + (current - target) * Math.Exp(-dt / (ms / 1000));
        }
    }

    public struct ViewMapping
    {
        public RectangleF Viewport;
        public double ScaleX, ScaleY;
        public static ViewMapping Create(Settings settings, Rectangle output)
        {
            double sx = output.Width / (double)settings.GameWidth;
            double sy = output.Height / (double)settings.GameHeight;
            double width = output.Width, height = output.Height;
            if (settings.Mapping == "fit")
            {
                sx = sy = Math.Min(sx, sy);
                width = settings.GameWidth * sx; height = settings.GameHeight * sy;
            }
            return new ViewMapping {
                ScaleX = sx, ScaleY = sy,
                Viewport = new RectangleF((float)(output.Left + (output.Width - width) / 2),
                    (float)(output.Top + (output.Height - height) / 2), (float)width, (float)height)
            };
        }
    }

    public struct Bar
    {
        public Rectangle Rect;
        public double Alpha;
        public Bar(Rectangle rect, double alpha) { Rect = rect; Alpha = alpha; }
    }

    public sealed class CrosshairFrame : IDisposable
    {
        public Bitmap Bitmap;
        public Point Origin;
        public List<Bar> Cores;
        public ViewMapping Mapping;
        public void Dispose() { if (Bitmap != null) Bitmap.Dispose(); }
    }

    // Only a small number of raster states recur while accelerating and recovering.
    // Bound both entry count and pixel storage; an unusually large frame is retained alone.
    public sealed class CrosshairFrameCache : IDisposable
    {
        private sealed class Entry { public int Movement; public CrosshairFrame Frame; }
        private readonly List<Entry> entries = new List<Entry>();
        private int version = -1, pixels;
        private Rectangle output;
        public int RenderCount { get; private set; }
        public int Count { get { return entries.Count; } }
        public int PixelCount { get { return pixels; } }
        public CrosshairFrame Get(Settings settings, double spread, Rectangle target, int revision)
        {
            if (version != revision || output != target) { Clear(); version = revision; output = target; }
            int movement = settings.Dynamic ? Math.Max(0, CrosshairRenderer.Quantize(spread * settings.GameHeight / 480.0)) : 0;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Movement == movement)
                { Entry hit = entries[i]; entries.RemoveAt(i); entries.Add(hit); return hit.Frame; }
            CrosshairFrame frame = CrosshairRenderer.Render(settings, spread, target); RenderCount++;
            int size = frame.Bitmap.Width * frame.Bitmap.Height;
            while (entries.Count > 0 && (entries.Count >= 12 || pixels + size > 65536))
            { Entry oldest = entries[0]; entries.RemoveAt(0); pixels -= oldest.Frame.Bitmap.Width * oldest.Frame.Bitmap.Height; oldest.Frame.Dispose(); }
            entries.Add(new Entry { Movement = movement, Frame = frame }); pixels += size;
            return frame;
        }
        public void Clear()
        { foreach (Entry entry in entries) entry.Frame.Dispose(); entries.Clear(); pixels = 0; version = -1; }
        public void Dispose() { Clear(); }
    }

    public static class CrosshairRenderer
    {
        public static int Quantize(double value) { return (int)Math.Round(value, MidpointRounding.ToEven); }
        public static List<Bar> GameBars(Settings settings, double spread)
        {
            double scale = settings.GameHeight / 480.0;
            int length = Quantize((float)(scale * (float)settings.Length));
            int width = Math.Max(1, Quantize((float)(scale * (float)settings.Thickness)));
            int offset = (int)((float)((float)settings.Gap + 4f));
            int movement = settings.Dynamic ? Math.Max(0, Quantize(spread * scale)) : 0;
            int near = width / 2 + offset + movement;
            var bars = new List<Bar>();
            if (length > 0)
            {
                int innerLength = length, outerLength = 0, innerNear = near;
                if (settings.Dynamic && settings.Split && movement > settings.SplitDistance)
                {
                    outerLength = Math.Min(length, Quantize(length * settings.SplitRatio));
                    innerLength = length - outerLength;
                    innerNear = width / 2 + offset + Quantize(settings.SplitDistance);
                }
                AddArms(bars, innerLength, width, innerNear, settings.TStyle,
                    outerLength > 0 ? settings.InnerAlpha : 1);
                if (outerLength > 0)
                    AddArms(bars, outerLength, width, near + innerLength, settings.TStyle, settings.OuterAlpha);
            }
            if (settings.Dot) bars.Add(new Bar(new Rectangle(-width / 2, -width / 2, width, width), 1));
            return bars;
        }
        private static void AddArms(List<Bar> bars, int length, int width, int near, bool t, double alpha)
        {
            if (length <= 0) return;
            int band = -width / 2, far = near + (width % 2);
            bars.Add(new Bar(new Rectangle(-near - length, band, length, width), alpha));
            bars.Add(new Bar(new Rectangle(far, band, length, width), alpha));
            if (!t) bars.Add(new Bar(new Rectangle(band, -near - length, width, length), alpha));
            bars.Add(new Bar(new Rectangle(band, far, width, length), alpha));
        }
        public static CrosshairFrame Render(Settings settings, double spread, Rectangle output)
        {
            var mapping = ViewMapping.Create(settings, output);
            var cores = new List<Bar>(); var outlines = new List<Bar>();
            int outline = settings.DrawOutline ? Math.Max(0, Quantize(settings.Outline)) : 0;
            // Quantize the screen-space stroke once per axis. Mapping an expanded game
            // rectangle separately gives opposite edges different rounding errors.
            int outlineX = outline > 0 ? Math.Max(1, Quantize(outline * mapping.ScaleX)) : 0;
            int outlineY = outline > 0 ? Math.Max(1, Quantize(outline * mapping.ScaleY)) : 0;
            Rectangle clip = Rectangle.FromLTRB((int)Math.Ceiling(mapping.Viewport.Left), (int)Math.Ceiling(mapping.Viewport.Top),
                (int)Math.Floor(mapping.Viewport.Right), (int)Math.Floor(mapping.Viewport.Bottom));
            foreach (var bar in GameBars(settings, spread))
            {
                Rectangle mapped = MapRect(bar.Rect, settings, mapping);
                Rectangle core = Rectangle.Intersect(mapped, clip);
                if (core.Width > 0 && core.Height > 0) cores.Add(new Bar(core, bar.Alpha));
                if (outline > 0)
                {
                    Rectangle border = mapped; border.Inflate(outlineX, outlineY);
                    border.Intersect(clip);
                    if (border.Width > 0 && border.Height > 0) outlines.Add(new Bar(border, bar.Alpha));
                }
            }
            Rectangle bounds = Rectangle.Empty;
            foreach (var bar in cores) bounds = bounds.IsEmpty ? bar.Rect : Rectangle.Union(bounds, bar.Rect);
            foreach (var bar in outlines) bounds = bounds.IsEmpty ? bar.Rect : Rectangle.Union(bounds, bar.Rect);
            if (bounds.IsEmpty) bounds = new Rectangle(output.Left + output.Width / 2, output.Top + output.Height / 2, 1, 1);
            bounds.Inflate(2, 2);
            if ((long)bounds.Width * bounds.Height > 16000000) throw new InvalidOperationException("准星画布过大，请减小尺寸。");
            int[] pixels = new int[bounds.Width * bounds.Height];
            foreach (var bar in outlines) Fill(pixels, bounds, bar, settings, true);
            // Core rectangles form a union: intersections must not become more opaque.
            int[] corePixels = new int[pixels.Length];
            foreach (var bar in cores) Fill(corePixels, bounds, bar, settings, false);
            for (int i = 0; i < pixels.Length; i++) if ((uint)corePixels[i] >> 24 != 0) pixels[i] = corePixels[i];
            var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try { Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); } finally { bitmap.UnlockBits(data); }
            return new CrosshairFrame { Bitmap = bitmap, Origin = bounds.Location, Cores = cores, Mapping = mapping };
        }
        private static Rectangle MapRect(Rectangle r, Settings s, ViewMapping m)
        {
            double cx = s.GameWidth / 2, cy = s.GameHeight / 2;
            int l = Quantize(m.Viewport.Left + (cx + r.Left) * m.ScaleX);
            int t = Quantize(m.Viewport.Top + (cy + r.Top) * m.ScaleY);
            int rr = Quantize(m.Viewport.Left + (cx + r.Right) * m.ScaleX);
            int b = Quantize(m.Viewport.Top + (cy + r.Bottom) * m.ScaleY);
            return Rectangle.FromLTRB(l, t, Math.Max(l + 1, rr), Math.Max(t + 1, b));
        }
        private static void Fill(int[] pixels, Rectangle bounds, Bar bar, Settings s, bool outline)
        {
            int alpha = Math.Max(0, Math.Min(255, Quantize(s.Alpha * bar.Alpha)));
            int red = outline ? 0 : (s.Red * alpha + 127) / 255;
            int green = outline ? 0 : (s.Green * alpha + 127) / 255;
            int blue = outline ? 0 : (s.Blue * alpha + 127) / 255;
            int color = (alpha << 24) | (red << 16) | (green << 8) | blue;
            Rectangle rect = Rectangle.Intersect(bounds, bar.Rect);
            for (int y = rect.Top; y < rect.Bottom; y++)
                for (int x = rect.Left; x < rect.Right; x++)
                {
                    int i = (y - bounds.Top) * bounds.Width + x - bounds.Left;
                    if ((uint)pixels[i] >> 24 <= alpha) pixels[i] = color;
                }
        }
    }
}
