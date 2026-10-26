using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LegacyCrosshair
{
    internal sealed class PreviewPanel : Control, IThemeAware
    {
        internal Settings Settings;
        internal CrosshairFrame Frame;
        internal Rectangle Output;
        internal MovementKeys Keys;
        internal double Spread;
        internal bool Demo;
        internal string InputStatus = "";
        private int paintedRevision = -1;
        private bool pendingPaint;
        private double lastPaintRequest = -1;
        internal bool PendingPaint { get { return pendingPaint; } }
        internal void UpdateState(Settings settings, CrosshairFrame frame, Rectangle output, MovementKeys keys, double spread, bool demo, string inputStatus, int revision)
        {
            pendingPaint |= paintedRevision != revision || Frame != frame || Output != output ||
                Keys.W != keys.W || Keys.A != keys.A || Keys.S != keys.S || Keys.D != keys.D ||
                Math.Round(Spread, 2) != Math.Round(spread, 2) || Demo != demo || InputStatus != inputStatus;
            Settings = settings; Frame = frame; Output = output; Keys = keys; Spread = spread; Demo = demo; InputStatus = inputStatus;
            paintedRevision = revision;
        }
        internal void FlushPaint(double now)
        {
            if (!pendingPaint || (lastPaintRequest >= 0 && now - lastPaintRequest < 1.0 / 30)) return;
            pendingPaint = false; lastPaintRequest = now; Invalidate();
        }
        internal void SuspendDrawing()
        {
            pendingPaint = false;
            // Release the thread's retained back buffer when the settings window goes to the tray.
            BufferedGraphicsManager.Current.Invalidate();
        }
        private readonly Font small = Theme.Font(9.5f, FontStyle.Regular), caption = Theme.Font(11, FontStyle.Regular), tiny = Theme.Font(9, FontStyle.Regular), heading = Theme.Font(12, FontStyle.Bold);
        private int U(float value) { return Theme.U(this, value); }
        public PreviewPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Panel; AccessibleName = "实时准星预览";
        }
        public void ApplyTheme() { BackColor = Theme.Panel; textBrush.Color = Theme.Text; accentBrush.Color = Theme.Accent; mutedBrush.Color = Theme.Muted; Invalidate(); }
        protected override void Dispose(bool disposing)
        { if (disposing) { small.Dispose(); caption.Dispose(); tiny.Dispose(); heading.Dispose(); textBrush.Dispose(); accentBrush.Dispose(); mutedBrush.Dispose(); } base.Dispose(disposing); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics; g.Clear(Theme.Background); g.SmoothingMode = SmoothingMode.AntiAlias;
            Theme.Fill(g, Theme.Panel, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Theme.U(this, 18));
            Theme.Stroke(g, Theme.Border, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Theme.U(this, 18));
            if (Settings == null || Frame == null || Width < 100 || Height < 100) return;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.DrawString("实时预览", heading, ThemeBrush(Theme.Text), U(24), U(22));
            string tag = Settings.Dynamic ? "经典动态" : "经典静态";
            Theme.Fill(g, Theme.AccentSoft, new Rectangle(Width - U(110), U(22), U(86), U(28)), U(8));
            TextRenderer.DrawText(g, tag, tiny, new Rectangle(Width - U(110), U(22), U(86), U(28)), Theme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            Rectangle area = new Rectangle(U(24), U(74), Math.Max(1, Width - U(48)), Math.Max(U(30), Height - U(204)));
            double fit = Math.Min(area.Width / (double)Output.Width, area.Height / (double)Output.Height);
            Rectangle scene = new Rectangle(area.Left + (area.Width - (int)(Output.Width * fit)) / 2,
                area.Top + (area.Height - (int)(Output.Height * fit)) / 2, Math.Max(1, (int)(Output.Width * fit)), Math.Max(1, (int)(Output.Height * fit)));
            var sceneClip = g.Save(); using (var path = Theme.Round(scene, U(12))) g.SetClip(path); DrawScene(g, scene, Settings.Background);
            if (Settings.Mapping == "fit")
            {
                RectangleF viewport = Frame.Mapping.Viewport;
                Rectangle game = Rectangle.FromLTRB(scene.Left + (int)((viewport.Left - Output.Left) * fit),
                    scene.Top + (int)((viewport.Top - Output.Top) * fit), scene.Left + (int)((viewport.Right - Output.Left) * fit),
                    scene.Top + (int)((viewport.Bottom - Output.Top) * fit));
                using (var black = new SolidBrush(Color.FromArgb(8, 10, 13)))
                {
                    g.FillRectangle(black, scene.Left, scene.Top, Math.Max(0, game.Left - scene.Left), scene.Height);
                    g.FillRectangle(black, game.Right, scene.Top, Math.Max(0, scene.Right - game.Right), scene.Height);
                    g.FillRectangle(black, game.Left, scene.Top, game.Width, Math.Max(0, game.Top - scene.Top));
                    g.FillRectangle(black, game.Left, game.Bottom, game.Width, Math.Max(0, scene.Bottom - game.Bottom));
                }
            }
            var saved = g.Save();
            double drawScale = Settings.FullPreview ? fit : 1;
            double centerX = Output.Left + Output.Width / 2.0, centerY = Output.Top + Output.Height / 2.0;
            float x = (float)(scene.Left + scene.Width / 2.0 + (Frame.Origin.X - centerX) * drawScale);
            float y = (float)(scene.Top + scene.Height / 2.0 + (Frame.Origin.Y - centerY) * drawScale);
            g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(Frame.Bitmap, new RectangleF(x, y, (float)(Frame.Bitmap.Width * drawScale), (float)(Frame.Bitmap.Height * drawScale)),
                new RectangleF(0, 0, Frame.Bitmap.Width, Frame.Bitmap.Height), GraphicsUnit.Pixel);
            g.Restore(saved);
            g.Restore(sceneClip);
            Theme.Stroke(g, Theme.Border, scene, U(12));
            Badge(g, scene.Left + U(12), scene.Top + U(12), Settings.FullPreview ? "全屏构图" : "1:1 实际像素");
            if (Settings.Dynamic && Demo) Badge(g, scene.Left + U(12), scene.Top + U(44), "移动效果演示");
            DrawLens(g, scene);
            int foot = Height - U(110);
            g.DrawString(Settings.GameWidth + " × " + Settings.GameHeight + "  →  " + Output.Width + " × " + Output.Height,
                caption, ThemeBrush(Theme.Text), U(24), foot);
            g.DrawString((Settings.Mapping == "stretch" ? "铺满 / 拉伸" : "保持比例 / 黑边") + "   ·   X " + Frame.Mapping.ScaleX.ToString("0.00") + "  /  Y " + Frame.Mapping.ScaleY.ToString("0.00"),
                small, ThemeBrush(Theme.Muted), U(24), foot + U(29));
            if (Settings.Dynamic)
            {
                int keyX = Math.Max(U(24), Width - U(146)), keyY = foot - U(1);
                DrawKey(g, "W", keyX + U(38), keyY, Keys.W);
                DrawKey(g, "A", keyX, keyY + U(36), Keys.A); DrawKey(g, "S", keyX + U(38), keyY + U(36), Keys.S); DrawKey(g, "D", keyX + U(76), keyY + U(36), Keys.D);
            }
            g.DrawString(InputStatus, tiny, ThemeBrush(Theme.Muted), U(24), Height - U(31));
            string amount = Settings.Dynamic ? "扩散 " + Spread.ToString("0.00") : "低开销静态模式";
            g.DrawString(amount, tiny, ThemeBrush(Theme.Accent), Width - U(112), Height - U(32));
        }
        private SolidBrush ThemeBrush(Color color)
        {
            // Return cached system-independent brushes owned by this control's palette.
            if (color == Theme.Text) return textBrush;
            if (color == Theme.Accent) return accentBrush;
            return mutedBrush;
        }
        private readonly SolidBrush textBrush = new SolidBrush(Theme.Text), accentBrush = new SolidBrush(Theme.Accent), mutedBrush = new SolidBrush(Theme.Muted);
        private void DrawKey(Graphics g, string label, int x, int y, bool on)
        {
            Theme.Fill(g, on ? Theme.AccentSoft : Theme.Input, new Rectangle(x, y, U(31), U(30)), U(7));
            using (var b = new SolidBrush(on ? Theme.Accent : Theme.Muted)) g.DrawString(label, small, b, x + U(7), y + U(5));
        }
        private void Badge(Graphics g, int x, int y, string label)
        {
            SizeF size = g.MeasureString(label, tiny);
            Theme.Fill(g, Color.FromArgb(220, 22, 28, 36), new RectangleF(x, y, size.Width + U(16), U(27)), U(7));
            using (var b = new SolidBrush(Color.White)) g.DrawString(label, tiny, b, x + U(8), y + U(4));
        }
        private void DrawLens(Graphics g, Rectangle scene)
        {
            if (scene.Width < U(300) || scene.Height < U(190)) return;
            int extent = Math.Min(U(150), scene.Height - U(55)), x = scene.Right - extent - U(12), y = scene.Bottom - extent - U(12);
            int zoom = Math.Max(1, Math.Min(8, (extent - U(16)) / Math.Max(Frame.Bitmap.Width, Frame.Bitmap.Height)));
            var saved = g.Save(); using (var path = Theme.Round(new Rectangle(x, y, extent, extent), U(8))) g.SetClip(path);
            using (var a = new SolidBrush(Color.FromArgb(28, 34, 40)))
            using (var b = new SolidBrush(Color.FromArgb(36, 43, 50)))
                for (int yy = 0; yy < extent; yy += 8) for (int xx = 0; xx < extent; xx += 8)
                    g.FillRectangle((xx / 8 + yy / 8) % 2 == 0 ? a : b, x + xx, y + yy, 8, 8);
            double cx = Output.Left + Output.Width / 2.0, cy = Output.Top + Output.Height / 2.0;
            g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(Frame.Bitmap, new RectangleF((float)(x + extent / 2.0 + (Frame.Origin.X - cx) * zoom),
                (float)(y + extent / 2.0 + (Frame.Origin.Y - cy) * zoom), Frame.Bitmap.Width * zoom, Frame.Bitmap.Height * zoom),
                new RectangleF(0, 0, Frame.Bitmap.Width, Frame.Bitmap.Height), GraphicsUnit.Pixel);
            g.Restore(saved);
            Theme.Stroke(g, Theme.Border, new Rectangle(x, y, extent, extent), U(8));
            Badge(g, x, y - U(27), zoom + "× 像素放大");
        }
        private void DrawScene(Graphics g, Rectangle r, string background)
        {
            if (background == "grid")
            {
                using (var a = new SolidBrush(Color.FromArgb(36, 43, 50)))
                using (var b = new SolidBrush(Color.FromArgb(45, 53, 61)))
                    for (int y = 0; y < r.Height; y += 20) for (int x = 0; x < r.Width; x += 20)
                        g.FillRectangle((x / 20 + y / 20) % 2 == 0 ? a : b, r.X + x, r.Y + y, Math.Min(20, r.Width - x), Math.Min(20, r.Height - y));
                return;
            }
            if (background == "dark" || background == "light")
            { using (var b = new SolidBrush(background == "dark" ? Color.FromArgb(14, 18, 24) : Color.FromArgb(220, 221, 214))) g.FillRectangle(b, r); return; }
            using (var gradient = new LinearGradientBrush(r, Color.FromArgb(133, 149, 148), Color.FromArgb(83, 91, 87), 90)) g.FillRectangle(gradient, r);
            int horizon = r.Top + r.Height * 56 / 100;
            using (var wall = new SolidBrush(Color.FromArgb(143, 133, 111))) g.FillRectangle(wall, r.Left, r.Top + r.Height / 6, r.Width, horizon - r.Top);
            using (var pen = new Pen(Color.FromArgb(100, 91, 76), 2))
            {
                for (int i = 0; i < 7; i++) g.DrawLine(pen, r.Left + r.Width * i / 6, r.Top + r.Height / 6, r.Left + r.Width * i / 6, horizon);
                g.DrawLine(pen, r.Left, r.Top + r.Height * 2 / 5, r.Right, r.Top + r.Height * 2 / 5);
            }
            using (var floor = new SolidBrush(Color.FromArgb(88, 93, 83))) g.FillRectangle(floor, r.Left, horizon, r.Width, r.Bottom - horizon);
            using (var p = new Pen(Color.FromArgb(120, 128, 112)))
            {
                for (int i = -4; i <= 4; i++) g.DrawLine(p, r.Left + r.Width / 2, horizon, r.Left + r.Width / 2 + i * r.Width / 3, r.Bottom);
                for (int i = 1; i < 5; i++) { int yy = horizon + (r.Bottom - horizon) * i * i / 25; g.DrawLine(p, r.Left, yy, r.Right, yy); }
            }
            int tw = Math.Max(24, r.Width / 11), th = Math.Max(36, r.Height / 4);
            Rectangle target = new Rectangle(r.Left + r.Width / 2 - tw / 2, r.Top + r.Height / 2 - th / 2, tw, th);
            using (var b = new SolidBrush(Color.FromArgb(187, 177, 145))) g.FillRectangle(b, target);
            using (var p = new Pen(Color.FromArgb(83, 77, 63), 2))
            {
                g.DrawRectangle(p, target);
                g.DrawEllipse(p, target.Left + tw / 5, target.Top + th / 5, tw * 3 / 5, th * 3 / 5);
                g.DrawEllipse(p, target.Left + tw / 3, target.Top + th / 3, tw / 3, th / 3);
            }
        }
    }
}
