using System;
using System.Drawing;
using System.IO;
using LegacyCrosshair;

internal static class CoreTests
{
    private static int count;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }
    private static bool Near(double a, double b, double epsilon) { return Math.Abs(a - b) < epsilon; }
    private static void Advance(MovementModel model, MovementKeys keys, Settings s, int hz, double seconds)
    { for (int i = 0; i < hz * seconds; i++) model.Step(keys, 1.0 / hz, s); }
    private static int Main()
    {
        try
        {
            var s = new Settings { GameWidth = 1280, GameHeight = 960, DrawOutline = false, Length = 2, Thickness = 0.5, Gap = -2 };
            var bars = CrosshairRenderer.GameBars(s, 20);
            Check(bars.Count == 4 && bars[0].Rect.Width == 4 && bars[0].Rect.Height == 1, "Legacy dimensions at 960p");
            Check(bars[0].Rect == CrosshairRenderer.GameBars(s, 0)[0].Rect, "Static style ignores motion");
            s.GameHeight = 1080; s.Length = 2.5;
            Check(CrosshairRenderer.GameBars(s, 0)[0].Rect.Width == 6, "Legacy half-to-even quantization");
            s.GameHeight = 960; s.Length = 2; s.Gap = -6;
            Check(CrosshairRenderer.GameBars(s, 0)[0].Rect.Right > 0, "Negative gap preserves crossed arms");
            s.TStyle = true; Check(CrosshairRenderer.GameBars(s, 0).Count == 3, "T style omits top arm");
            s.Length = 0; s.Dot = true; Check(CrosshairRenderer.GameBars(s, 0).Count == 1, "Zero-length dot-only crosshair");
            s.TStyle = false; s.Dot = false; s.Length = 2; s.Gap = -2;
            CheckOutlineRegression();
            var stretch = ViewMapping.Create(s, new Rectangle(0, 0, 1920, 1080));
            Check(Near(stretch.ScaleX, 1.5, 0.001) && Near(stretch.ScaleY, 1.125, 0.001), "4:3 stretch scales axes independently");
            s.Mapping = "fit"; var fit = ViewMapping.Create(s, new Rectangle(0, 0, 1920, 1080));
            Check(Near(fit.ScaleX, fit.ScaleY, 0.001) && Near(fit.Viewport.X, 240, 0.01) && Near(fit.Viewport.Width, 1440, 0.01), "4:3 letterbox has 240px side bars");
            var negative = ViewMapping.Create(s, new Rectangle(-1920, 0, 1920, 1080));
            Check(Near(negative.Viewport.X, -1680, 0.01), "Secondary monitor supports negative coordinates");
            s.Mapping = "stretch"; s.Alpha = 128; s.Gap = -5;
            using (var frame = CrosshairRenderer.Render(s, 0, new Rectangle(0, 0, 1280, 960)))
            {
                bool any = false;
                for (int y = 0; y < frame.Bitmap.Height; y++) for (int x = 0; x < frame.Bitmap.Width; x++)
                { int alpha = frame.Bitmap.GetPixel(x, y).A; CheckPixel(alpha <= 128); any |= alpha == 128; }
                Check(any, "Overlapping arms retain configured alpha");
            }
            s.Alpha = 0;
            using (var frame = CrosshairRenderer.Render(s, 0, new Rectangle(0, 0, 1920, 1080)))
            {
                bool transparent = true;
                for (int y = 0; y < frame.Bitmap.Height; y++) for (int x = 0; x < frame.Bitmap.Width; x++) transparent &= frame.Bitmap.GetPixel(x, y).A == 0;
                Check(transparent, "Zero alpha is fully transparent");
            }
            s.Dynamic = true; s.Alpha = 255; s.Gap = -2; s.Spread = 5;
            var a = new MovementModel(); var b = new MovementModel();
            Advance(a, new MovementKeys(true, false, false, false), s, 60, 1);
            Advance(b, new MovementKeys(true, false, false, false), s, 144, 1);
            Check(a.Amount > 4.9 && Near(a.Amount, b.Amount, 0.01), "Movement response stable at 60Hz and 144Hz");
            double before = a.Amount; a.Step(new MovementKeys(), 0.016, s);
            Check(a.Amount > 0 && a.Amount < before, "Release begins smooth recovery, not instant collapse");
            Advance(a, new MovementKeys(), s, 60, 2); Check(a.Amount == 0, "Released keys recover to rest");
            a.Reset(); Advance(a, new MovementKeys(true, true, false, true), s, 60, 1);
            Check(a.Amount > 4.9 && Near(a.X, 0, 0.001), "W+A+D retains forward movement");
            a.Reset(); Advance(a, new MovementKeys(true, false, false, true), s, 60, 2);
            Check(a.Amount <= s.Spread + 0.001, "Diagonal motion does not exceed maximum spread");
            a.Reset(); Advance(a, new MovementKeys(false, true, false, false), s, 60, 1);
            a.Step(new MovementKeys(false, false, false, true), 0.04, s);
            Check(Math.Abs(a.X) < 0.4, "Opposite key input brakes velocity before reversing");
            s.Split = true; s.SplitDistance = 2;
            Check(CrosshairRenderer.GameBars(s, 5).Count == 8, "Dynamic style emits inner and outer segments");
            s.Dynamic = false; a.Step(new MovementKeys(true, false, false, false), 0.1, s);
            Check(a.Amount == 0 && a.X == 0, "Switching to static resets movement");
            s.Dynamic = true; a.Reset(); a.Step(new MovementKeys(true, false, false, false), 0, s);
            Check(a.NeedsUpdate && a.Amount == 0, "First key event wakes animation without jumping after idle");
            Advance(a, new MovementKeys(true, false, false, false), s, 60, 3);
            Check(!a.NeedsUpdate && Near(a.Amount, s.Spread, 0.001), "Steady held input can leave the animation timer idle");
            a.Step(new MovementKeys(), 0.016, s);
            Check(a.NeedsUpdate, "Releasing a held key resumes smooth recovery");
            Advance(a, new MovementKeys(), s, 60, 3);
            Check(!a.NeedsUpdate && a.Amount == 0, "Recovered movement becomes fully idle");
            CheckFrameCache();
            string folder = Path.Combine(Path.GetTempPath(), "LegacyCrosshair.Tests." + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(folder, "settings.json");
            try
            {
                ConfigStore.Write(path, s); var read = ConfigStore.Read(path);
                Check(read.GameHeight == s.GameHeight && read.Gap == s.Gap, "Configuration JSON round trip");
                s.Gap = -3; ConfigStore.Write(path, s);
                Check(File.Exists(path + ".bak") && ConfigStore.Read(path).Gap == -3, "Atomic configuration update retains backup");
                s.UiTheme = "dark"; ConfigStore.Write(path, s);
                Check(ConfigStore.Read(path).UiTheme == "dark", "Theme preference survives configuration round trip");
                File.WriteAllText(path, "{\"Version\":1,\"Length\":1.6}");
                Check(ConfigStore.Read(path).UiTheme == "light" && ConfigStore.Read(path).Length == 1.6, "Old configuration without a theme keeps crosshair parameters and defaults to light");
                File.WriteAllText(path, "{broken");
                bool rejected = false; try { ConfigStore.Read(path); } catch { rejected = true; }
                Check(rejected, "Malformed import is rejected");
            }
            finally
            {
                string resolved = Path.GetFullPath(folder);
                if (resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(resolved).StartsWith("LegacyCrosshair.Tests.", StringComparison.Ordinal) && Directory.Exists(resolved)) Directory.Delete(resolved, true);
            }
            string artifacts = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "artifacts"); Directory.CreateDirectory(artifacts);
            s = new Settings { Dynamic = true };
            using (var frame = CrosshairRenderer.Render(s, 5, new Rectangle(0, 0, 1920, 1080))) frame.Bitmap.Save(Path.Combine(artifacts, "dynamic-stretch.png"));
            Console.WriteLine("All " + count + " checks passed."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void CheckPixel(bool ok) { if (!ok) throw new Exception("Overlap increased opacity"); }
    private static void CheckFrameCache()
    {
        var s = new Settings { Dynamic = true, SplitDistance = 2, Alpha = 128 };
        var output = new Rectangle(-1920, 0, 1920, 1080);
        using (var cache = new CrosshairFrameCache())
        {
            CrosshairFrame rest = cache.Get(s, 0, output, 1);
            cache.Get(s, 2, output, 1);
            Check(Object.ReferenceEquals(rest, cache.Get(s, 0, output, 1)) && cache.RenderCount == 2, "Repeated dynamic states reuse the same cached bitmap");
            Check(Object.ReferenceEquals(rest, cache.Get(s, 0.01, output, 1)), "Subpixel movement reuses the existing raster");
            bool identical = true;
            for (int movement = 0; movement < 50; movement++)
            {
                CrosshairFrame cached = cache.Get(s, movement / 2.0, output, 1);
                using (var direct = CrosshairRenderer.Render(s, movement / 2.0, output))
                {
                    identical &= cached.Origin == direct.Origin && cached.Bitmap.Size == direct.Bitmap.Size;
                    for (int y = 0; y < direct.Bitmap.Height; y++) for (int x = 0; x < direct.Bitmap.Width; x++)
                        identical &= cached.Bitmap.GetPixel(x, y) == direct.Bitmap.GetPixel(x, y);
                }
            }
            Check(identical, "Cached dynamic rasters retain every pixel and alpha value");
            Check(cache.Count <= 12 && cache.PixelCount <= 65536, "Raster cache stays within its entry and pixel budgets");
            int count = cache.RenderCount; s.Red = 255;
            cache.Get(s, 0, output, 2);
            Check(cache.Count == 1 && cache.RenderCount == count + 1, "Parameter revision invalidates old cached frames");
            cache.Get(s, 0, new Rectangle(0, 0, 2560, 1440), 2);
            Check(cache.Count == 1, "Display mapping changes replace the old cache");
            cache.Clear(); Check(cache.Count == 0 && cache.PixelCount == 0, "Disabling rendering releases cached frames");
        }
    }

    private static bool IsBlack(CrosshairFrame frame, int x, int y)
    {
        x -= frame.Origin.X; y -= frame.Origin.Y;
        if (x < 0 || y < 0 || x >= frame.Bitmap.Width || y >= frame.Bitmap.Height) return false;
        Color pixel = frame.Bitmap.GetPixel(x, y);
        return pixel.A > 0 && pixel.R == 0 && pixel.G == 0 && pixel.B == 0;
    }
    private static int BlackRun(CrosshairFrame frame, int x, int y, int dx, int dy)
    {
        int count = 0;
        while (IsBlack(frame, x, y)) { count++; x += dx; y += dy; }
        return count;
    }
    private static void CheckBalancedOutline(Settings s, Rectangle output, double spread, string message)
    {
        using (var frame = CrosshairRenderer.Render(s, spread, output))
        {
            foreach (var core in frame.Cores)
            {
                Rectangle r = core.Rect;
                int cx = r.Left + r.Width / 2, cy = r.Top + r.Height / 2;
                int left = BlackRun(frame, r.Left - 1, cy, -1, 0);
                int right = BlackRun(frame, r.Right, cy, 1, 0);
                int top = BlackRun(frame, cx, r.Top - 1, 0, -1);
                int bottom = BlackRun(frame, cx, r.Bottom, 0, 1);
                if (left != right || top != bottom)
                    throw new Exception(message + ": border L/R=" + left + "/" + right + ", T/B=" + top + "/" + bottom);
            }
            Check(true, message);
        }
    }
    private static void CheckOutlineRegression()
    {
        var sample = new Settings { Length = 1.6, Thickness = 0.4, Gap = -1.1, Outline = 0.8,
            Red = 255, Green = 255, Blue = 255, GameWidth = 1440, GameHeight = 1080 };
        string artifacts = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "artifacts");
        Directory.CreateDirectory(artifacts);
        using (var frame = CrosshairRenderer.Render(sample, 0, new Rectangle(0, 0, 1920, 1080)))
        {
            // Save the actual shared raster, magnified at an integer scale for visual inspection.
            using (var bitmap = new Bitmap(frame.Bitmap.Width * 12, frame.Bitmap.Height * 12))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.FromArgb(65, 74, 84));
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                graphics.DrawImage(frame.Bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    0, 0, frame.Bitmap.Width, frame.Bitmap.Height, GraphicsUnit.Pixel);
                bitmap.Save(Path.Combine(artifacts, "outline-regression-current.png"));
            }
        }
        CheckBalancedOutline(sample, new Rectangle(0, 0, 1920, 1080), 0, "Screenshot regression: 1440x1080 stretched to 1920x1080 has equal opposing borders");
        sample.Gap = 8;
        sample.GameWidth = 1280; sample.GameHeight = 960;
        CheckBalancedOutline(sample, new Rectangle(0, 0, 1920, 1080), 0, "Fractional X/Y stretch keeps outline balanced on each arm");
        sample.GameWidth = 1024; sample.GameHeight = 768;
        CheckBalancedOutline(sample, new Rectangle(-1920, -120, 1920, 1080), 0, "Negative monitor origin preserves balanced borders");
        sample.Mapping = "fit";
        CheckBalancedOutline(sample, new Rectangle(101, 37, 1919, 1079), 0, "Letterbox with fractional viewport keeps opposing borders equal");
        sample.Mapping = "stretch"; sample.Dynamic = true; sample.SplitDistance = 2; sample.Length = 4;
        CheckBalancedOutline(sample, new Rectangle(0, 0, 1920, 1080), 10, "Dynamic inner and outer segments share balanced borders");
        sample.TStyle = true;
        CheckBalancedOutline(sample, new Rectangle(0, 0, 1920, 1080), 10, "T-shaped dynamic borders stay balanced");
        sample.Dynamic = false; sample.Length = 0; sample.Dot = true;
        CheckBalancedOutline(sample, new Rectangle(0, 0, 1920, 1080), 0, "Center dot border stays balanced");
        sample.Alpha = 128;
        CheckBalancedOutline(sample, new Rectangle(0, 0, 1920, 1080), 0, "Semi-transparent borders retain equal extents");
        sample.DrawOutline = false;
        using (var frame = CrosshairRenderer.Render(sample, 0, new Rectangle(0, 0, 1920, 1080)))
        {
            bool noBlack = true;
            for (int y = 0; y < frame.Bitmap.Height; y++) for (int x = 0; x < frame.Bitmap.Width; x++)
                noBlack &= !IsBlack(frame, x + frame.Origin.X, y + frame.Origin.Y);
            Check(noBlack, "Disabling outline leaves no black stroke pixels");
        }
    }
}
