using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace LegacyCrosshair
{
    internal interface IThemeAware { void ApplyTheme(); }

    internal static class Theme
    {
        internal static bool Dark;
        internal static Color Background, Panel, Input, Border, Text, Muted, Accent, AccentSoft, Hover, Track;
        internal static readonly Color CrosshairGreen = Color.FromArgb(95, 255, 139);
        static Theme() { SetMode("light"); }
        internal static void SetMode(string mode)
        {
            Dark = mode == "dark";
            Background = Hex(Dark ? 0x121418 : 0xF4F5F7); Panel = Hex(Dark ? 0x1B1E24 : 0xFFFFFF);
            Input = Hex(Dark ? 0x252930 : 0xF2F4F7); Border = Hex(Dark ? 0x333943 : 0xE5E8EE);
            Text = Hex(Dark ? 0xF0F2F6 : 0x242A34); Muted = Hex(Dark ? 0x9CA5B3 : 0x737E8E);
            Accent = Hex(Dark ? 0x8EAEFF : 0x386DE8); AccentSoft = Hex(Dark ? 0x2A3651 : 0xEBF0FF);
            Hover = Hex(Dark ? 0x303640 : 0xE9EDF3); Track = Hex(Dark ? 0x414854 : 0xDCE1E9);
        }
        internal static Color Hex(int value) { return Color.FromArgb((value >> 16) & 255, (value >> 8) & 255, value & 255); }
        internal static Font Font(float size, FontStyle style) { return new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point); }
        internal static int U(Control control, float value) { return (int)Math.Round(value * control.DeviceDpi / 96f); }
        internal static GraphicsPath Round(RectangleF r, float radius)
        {
            var path = new GraphicsPath(); float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { path.AddRectangle(r); return path; }
            path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
        internal static void Fill(Graphics g, Color color, RectangleF rect, float radius)
        { using (var path = Round(rect, radius)) using (var brush = new SolidBrush(color)) g.FillPath(brush, path); }
        internal static void Stroke(Graphics g, Color color, RectangleF rect, float radius)
        { using (var path = Round(rect, radius)) using (var pen = new Pen(color)) g.DrawPath(pen, path); }
        internal static Label Label(string text, float size, Color color)
        {
            return new Label { Text = text, Font = Font(size, FontStyle.Regular), ForeColor = color, BackColor = Color.Transparent,
                AutoSize = true, Margin = Padding.Empty, Tag = color == Muted ? "muted" : color == Accent ? "accent" : "text" };
        }
        internal static ActionButton Button(string text, bool primary) { return new ActionButton { Text = text, Primary = primary }; }
        internal static void Apply(Control root)
        {
            var aware = root as IThemeAware;
            if (aware != null) aware.ApplyTheme();
            else if (root is Label)
                root.ForeColor = (string)root.Tag == "muted" ? Muted : (string)root.Tag == "accent" ? Accent : Text;
            else if (root is TextBox) { root.BackColor = Input; root.ForeColor = Text; }
            else
            {
                root.BackColor = root is Form || (root.Tag as string) == "canvas" ? Background : root.Parent == null ? Panel : root.Parent.BackColor;
                root.ForeColor = Text;
            }
            foreach (Control child in root.Controls) Apply(child);
            root.Invalidate();
        }
    }

    internal sealed class SurfaceCard : Panel, IThemeAware
    {
        internal SurfaceCard() { DoubleBuffered = true; Padding = new Padding(20); }
        public void ApplyTheme() { BackColor = Theme.Panel; Invalidate(); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Theme.Background : Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Theme.Fill(e.Graphics, Theme.Panel, r, Theme.U(this, 18));
            Theme.Stroke(e.Graphics, Theme.Border, r, Theme.U(this, 18));
        }
    }

    internal sealed class ActionButton : Button, IThemeAware
    {
        internal bool Primary;
        internal bool Swatch;
        internal Color SwatchColor;
        private bool hovered, pressed;
        internal ActionButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Size = new Size(128, 40);
            Font = Theme.Font(10, FontStyle.Regular); Cursor = Cursors.Hand; Margin = new Padding(0, 0, 10, 0);
        }
        public void ApplyTheme() { BackColor = Theme.Panel; ForeColor = Primary ? Color.White : Theme.Text; Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(Parent == null ? Theme.Panel : Parent.BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = new RectangleF(1, 1, Width - 3, Height - 3);
            if (Swatch)
            {
                Theme.Fill(g, SwatchColor, r, Theme.U(this, 10));
                Theme.Stroke(g, Focused || hovered ? Theme.Accent : Theme.Border, r, Theme.U(this, 10)); return;
            }
            Color fill = Primary ? Theme.Hex(pressed ? 0x2454BF : hovered ? 0x477AF0 : 0x386DE8) : pressed || hovered ? Theme.Hover : Theme.Input;
            Theme.Fill(g, fill, r, Theme.U(this, 10));
            if (!Primary) Theme.Stroke(g, Theme.Border, r, Theme.U(this, 10));
            if (Focused && ShowFocusCues) Theme.Stroke(g, Theme.Accent, r, Theme.U(this, 10));
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, Primary ? Color.White : Enabled ? Theme.Text : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class ToggleSwitch : CheckBox, IThemeAware
    {
        private float position;
        private readonly Timer animation = new Timer { Interval = 15 };
        internal ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = false; Height = 44; Width = 310; Font = Theme.Font(10, FontStyle.Regular); Cursor = Cursors.Hand;
            Margin = new Padding(0, 0, 0, 4);
            animation.Tick += delegate { float target = Checked ? 1 : 0; position += (target - position) * 0.34f; if (Math.Abs(target - position) < 0.01f) { position = target; animation.Stop(); } Invalidate(); };
        }
        public void ApplyTheme() { BackColor = Parent == null ? Theme.Panel : Parent.BackColor; ForeColor = Theme.Text; Invalidate(); }
        protected override void OnCheckedChanged(EventArgs e)
        { if (IsHandleCreated && Visible) animation.Start(); else position = Checked ? 1 : 0; base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(Parent == null ? Theme.Panel : Parent.BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = Theme.U(this, 44), h = Theme.U(this, 26), pad = Theme.U(this, 3);
            var track = new RectangleF(Width - w - 2, (Height - h) / 2f, w, h);
            Theme.Fill(g, Checked ? Theme.Hex(0x34C77B) : Theme.Track, track, h / 2f);
            float knob = h - pad * 2, x = track.Left + pad + (w - h) * position;
            using (var shadow = new SolidBrush(Color.FromArgb(25, Color.Black))) g.FillEllipse(shadow, x, track.Top + pad + 1, knob, knob);
            using (var brush = new SolidBrush(Color.White)) g.FillEllipse(brush, x, track.Top + pad, knob, knob);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width - w - Theme.U(this, 16), Height), Enabled ? Theme.Text : Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues) Theme.Stroke(g, Theme.Accent, new RectangleF(track.X - 2, track.Y - 2, track.Width + 4, track.Height + 4), h / 2f + 2);
        }
        protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }
    }

    internal sealed class SegmentedPicker : Control, IThemeAware
    {
        internal string[] Items = new string[0];
        private int selectedIndex;
        internal event EventHandler SelectedIndexChanged;
        internal int SelectedIndex
        { get { return selectedIndex; } set { if (value == selectedIndex) return; selectedIndex = value; Invalidate(); if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty); } }
        internal SegmentedPicker()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Size = new Size(240, 42); Font = Theme.Font(10, FontStyle.Regular); Cursor = Cursors.Hand; TabStop = true;
            AccessibleRole = AccessibleRole.PageTabList;
        }
        public void ApplyTheme() { BackColor = Parent == null ? Theme.Panel : Parent.BackColor; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e)
        { base.OnMouseDown(e); Focus(); if (e.Button == MouseButtons.Left && Items.Length > 0) SelectedIndex = Math.Min(Items.Length - 1, Math.Max(0, e.X * Items.Length / Math.Max(1, Width))); }
        protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e)
        { if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right) { SelectedIndex = Math.Max(0, Math.Min(Items.Length - 1, selectedIndex + (e.KeyCode == Keys.Right ? 1 : -1))); e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(Parent == null ? Theme.Panel : Parent.BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
            float pad = Theme.U(this, 3); Theme.Fill(g, Theme.Input, ClientRectangle, Theme.U(this, 11));
            if (Items.Length == 0) return;
            float cell = (Width - pad * 2) / Items.Length;
            var active = new RectangleF(pad + cell * selectedIndex, pad, cell, Height - pad * 2);
            Theme.Fill(g, Theme.Dark ? Theme.Hover : Theme.Panel, active, Theme.U(this, 8));
            Theme.Stroke(g, Focused && ShowFocusCues ? Theme.Accent : Theme.Border, active, Theme.U(this, 8));
            for (int i = 0; i < Items.Length; i++) TextRenderer.DrawText(g, Items[i], Font,
                new Rectangle((int)(pad + i * cell), 0, (int)cell, Height), i == selectedIndex ? Theme.Text : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    internal sealed class ChoiceField : Control, IThemeAware
    {
        internal readonly List<object> Items = new List<object>();
        private int selectedIndex = -1;
        private ToolStripDropDown popup;
        private bool hovered;
        internal event EventHandler SelectedIndexChanged;
        internal object SelectedItem { get { return selectedIndex >= 0 && selectedIndex < Items.Count ? Items[selectedIndex] : null; } }
        internal int SelectedIndex
        {
            get { return selectedIndex; }
            set { value = Math.Max(-1, Math.Min(Items.Count - 1, value)); if (value == selectedIndex) return; selectedIndex = value; Invalidate(); if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty); }
        }
        internal bool PopupVisible { get { return popup != null && popup.Visible; } }
#if UI_TEST
        internal ToolStripDropDown TestPopup { get { return popup; } }
#endif
        internal ChoiceField()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Size = new Size(300, 40); Font = Theme.Font(10, FontStyle.Regular); Cursor = Cursors.Hand; TabStop = true; AccessibleRole = AccessibleRole.ComboBox;
        }
        public void ApplyTheme() { if (popup != null) popup.Close(); BackColor = Parent == null ? Theme.Panel : Parent.BackColor; ForeColor = Theme.Text; Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { Focus(); OpenDropDown(); } }
        protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.F4 || (e.Alt && e.KeyCode == Keys.Down)) { OpenDropDown(); e.Handled = true; }
            else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) { SelectedIndex = Math.Max(0, Math.Min(Items.Count - 1, selectedIndex + (e.KeyCode == Keys.Down ? 1 : -1))); e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        internal void OpenDropDown()
        {
            if (Items.Count == 0) return;
            if (popup != null) popup.Dispose();
            var list = new ChoiceList(Items, selectedIndex, Font, Width, delegate(int index) { SelectedIndex = index; if (popup != null) popup.Close(); Focus(); });
            var host = new ToolStripControlHost(list) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = list.Size };
            popup = new ToolStripDropDown { AutoSize = true, Padding = new Padding(1), Margin = Padding.Empty, BackColor = Theme.Border, DropShadowEnabled = true };
            popup.Items.Add(host); popup.Show(this, new Point(0, Height + Theme.U(this, 6))); list.Focus();
        }
        internal void CloseDropDown() { if (popup != null) popup.Close(); }
        protected override void Dispose(bool disposing) { if (disposing && popup != null) popup.Dispose(); base.Dispose(disposing); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(Parent == null ? Theme.Panel : Parent.BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Theme.Fill(g, Theme.Input, r, Theme.U(this, 10));
            Theme.Stroke(g, Focused || hovered ? Theme.Accent : Theme.Border, r, Theme.U(this, 10));
            TextRenderer.DrawText(g, SelectedItem == null ? "请选择" : SelectedItem.ToString(), Font,
                new Rectangle(Theme.U(this, 12), 0, Width - Theme.U(this, 42), Height), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            float x = Width - Theme.U(this, 21), y = Height / 2f;
            using (var pen = new Pen(Theme.Muted, Theme.U(this, 1.5f))) { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawLines(pen, new[] { new PointF(x - 4, y - 2), new PointF(x, y + 2), new PointF(x + 4, y - 2) }); }
        }
    }

    internal sealed class ChoiceList : Control
    {
        private readonly List<object> items;
        private readonly Action<int> select;
        private int active, first;
        private readonly int row;
        internal ChoiceList(List<object> source, int selected, Font font, int width, Action<int> choose)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            items = source; active = Math.Max(0, selected); select = choose; Font = font; row = Theme.U(this, 38);
            Size = new Size(Math.Max(width - 2, Theme.U(this, 230)), Math.Min(8, source.Count) * row + Theme.U(this, 12));
            first = Math.Max(0, active - 7); TabStop = true; BackColor = Theme.Panel; AccessibleRole = AccessibleRole.List;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(Theme.Panel); g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = first; i < Math.Min(items.Count, first + 8); i++)
            {
                var rect = new Rectangle(Theme.U(this, 6), Theme.U(this, 6) + (i - first) * row, Width - Theme.U(this, 12), row);
                if (i == active) Theme.Fill(g, Theme.AccentSoft, rect, Theme.U(this, 7));
                TextRenderer.DrawText(g, items[i].ToString(), Font, new Rectangle(rect.Left + Theme.U(this, 10), rect.Top, rect.Width - Theme.U(this, 20), rect.Height),
                    i == active ? Theme.Accent : Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
        protected override void OnMouseMove(MouseEventArgs e) { int n = first + (e.Y - Theme.U(this, 6)) / row; if (n >= first && n < Math.Min(items.Count, first + 8) && n != active) { active = n; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { if (e.Button == MouseButtons.Left && e.Y >= Theme.U(this, 6) && e.Y < Height - Theme.U(this, 6)) select(active); base.OnMouseUp(e); }
        protected override void OnMouseWheel(MouseEventArgs e) { first = Math.Max(0, Math.Min(Math.Max(0, items.Count - 8), first - Math.Sign(e.Delta))); Invalidate(); base.OnMouseWheel(e); }
        protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return || e.KeyCode == Keys.Space) { select(active); e.Handled = true; }
            else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.Home || e.KeyCode == Keys.End)
            {
                active = e.KeyCode == Keys.Home ? 0 : e.KeyCode == Keys.End ? items.Count - 1 : Math.Max(0, Math.Min(items.Count - 1, active + (e.KeyCode == Keys.Down ? 1 : -1)));
                if (active < first) first = active; if (active >= first + 8) first = active - 7; Invalidate(); e.Handled = true;
            }
            base.OnKeyDown(e);
        }
    }

    internal sealed class NumberField : UserControl, IThemeAware
    {
        internal readonly TextBox Editor = new TextBox { BorderStyle = BorderStyle.None, TextAlign = HorizontalAlignment.Right };
        internal decimal Minimum, Maximum = 100, Increment = 1;
        internal int DecimalPlaces;
        private decimal value;
        internal event EventHandler ValueChanged;
        internal decimal Value
        {
            get { return value; }
            set { value = Math.Max(Minimum, Math.Min(Maximum, value)); if (this.value == value) { FormatValue(); return; } this.value = value; FormatValue(); if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); Invalidate(); }
        }
        internal NumberField()
        {
            DoubleBuffered = true; Size = new Size(96, 36); Font = Theme.Font(10, FontStyle.Regular); Controls.Add(Editor);
            Editor.Font = Font; Editor.LostFocus += delegate { CommitText(); Invalidate(); }; Editor.GotFocus += delegate { Invalidate(); };
            Editor.KeyDown += delegate(object sender, KeyEventArgs e) {
                if (e.KeyCode == Keys.Enter) { CommitText(); Editor.SelectAll(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) { Value += e.KeyCode == Keys.Up ? Increment : -Increment; e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Escape) { FormatValue(); e.SuppressKeyPress = true; }
            };
            FormatValue();
        }
        private void FormatValue() { Editor.Text = value.ToString("F" + DecimalPlaces, CultureInfo.CurrentCulture); }
        internal void CommitText()
        { decimal parsed; if (Decimal.TryParse(Editor.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed)) Value = Decimal.Round(parsed, DecimalPlaces); else FormatValue(); }
        public void ApplyTheme() { BackColor = Parent == null ? Theme.Panel : Parent.BackColor; Editor.BackColor = Theme.Input; Editor.ForeColor = Theme.Text; Invalidate(); }
        protected override void OnResize(EventArgs e)
        { base.OnResize(e); if (Editor != null) Editor.SetBounds(Theme.U(this, 8), Math.Max(0, (Height - Editor.PreferredHeight) / 2), Math.Max(10, Width - Theme.U(this, 36)), Editor.PreferredHeight); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Theme.Panel : Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Theme.Fill(e.Graphics, Theme.Input, r, Theme.U(this, 9)); Theme.Stroke(e.Graphics, ContainsFocus ? Theme.Accent : Theme.Border, r, Theme.U(this, 9));
            int x = Width - Theme.U(this, 16), mid = Height / 2;
            using (var pen = new Pen(Theme.Muted, 1.4f))
            {
                e.Graphics.DrawLines(pen, new[] { new Point(x - 3, mid - 5), new Point(x, mid - 8), new Point(x + 3, mid - 5) });
                e.Graphics.DrawLines(pen, new[] { new Point(x - 3, mid + 5), new Point(x, mid + 8), new Point(x + 3, mid + 5) });
            }
        }
        protected override void OnMouseDown(MouseEventArgs e)
        { base.OnMouseDown(e); if (e.Button == MouseButtons.Left && e.X > Width - Theme.U(this, 29)) { CommitText(); Value += e.Y < Height / 2 ? Increment : -Increment; } else Editor.Focus(); }
    }

    internal sealed class ValueSlider : Control, IThemeAware
    {
        internal int Minimum, Maximum = 100;
        private int value;
        internal event EventHandler ValueChanged;
        internal int Value { get { return value; } set { value = Math.Max(Minimum, Math.Min(Maximum, value)); if (this.value == value) return; this.value = value; Invalidate(); if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); } }
        internal ValueSlider() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true); Height = 32; TabStop = true; Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.Slider; }
        public void ApplyTheme() { BackColor = Parent == null ? Theme.Panel : Parent.BackColor; Invalidate(); }
        private void SetFromX(int x) { int pad = Theme.U(this, 7); Value = Minimum + (int)Math.Round((Maximum - Minimum) * Math.Max(0, Math.Min(1, (x - pad) / (double)Math.Max(1, Width - pad * 2)))); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { Focus(); Capture = true; SetFromX(e.X); } }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (Capture) SetFromX(e.X); }
        protected override void OnMouseUp(MouseEventArgs e) { Capture = false; base.OnMouseUp(e); }
        protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right) { Value += e.KeyCode == Keys.Right ? 1 : -1; e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(Parent == null ? Theme.Panel : Parent.BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
            float pad = Theme.U(this, 7), y = Height / 2f, x = pad + (Width - pad * 2) * (value - Minimum) / Math.Max(1f, Maximum - Minimum);
            Theme.Fill(g, Theme.Track, new RectangleF(pad, y - 2, Width - pad * 2, 4), 2);
            if (x > pad) Theme.Fill(g, Theme.Accent, new RectangleF(pad, y - 2, x - pad, 4), 2);
            float radius = Theme.U(this, 6);
            using (var brush = new SolidBrush(Theme.Accent)) g.FillEllipse(brush, x - radius, y - radius, radius * 2, radius * 2);
            if (Focused && ShowFocusCues) using (var pen = new Pen(Theme.Accent)) g.DrawEllipse(pen, x - radius - 3, y - radius - 3, radius * 2 + 6, radius * 2 + 6);
        }
    }

    internal sealed class SettingsScroll : Panel, IThemeAware
    {
        internal readonly FlowLayoutPanel Content = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
        private int offset, dragY, dragOffset;
        internal SettingsScroll() { DoubleBuffered = true; Controls.Add(Content); Content.SizeChanged += delegate { Arrange(); }; HookInput(Content); }
        private void HookInput(Control control)
        {
            control.ControlAdded += delegate(object sender, ControlEventArgs e) { HookInput(e.Control); };
            control.MouseWheel += delegate(object sender, MouseEventArgs e) { ScrollWheel(e); };
            control.Enter += delegate {
                if (!control.TabStop || !control.Visible || !control.IsHandleCreated) return;
                Point point = PointToClient(control.PointToScreen(Point.Empty));
                if (point.Y < 0) offset += point.Y;
                else if (point.Y + control.Height > Height) offset += point.Y + control.Height - Height;
                Arrange();
            };
            foreach (Control child in control.Controls) HookInput(child);
        }
        public void ApplyTheme() { BackColor = Theme.Panel; Content.BackColor = Theme.Panel; Invalidate(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); Arrange(); }
        private void Arrange()
        {
            if (Content == null) return;
            offset = Math.Max(0, Math.Min(offset, Math.Max(0, Content.Height - Height))); Content.Location = new Point(0, -offset); Invalidate();
        }
        private void ScrollWheel(MouseEventArgs e)
        {
            var handled = e as HandledMouseEventArgs; if (handled != null && handled.Handled) return;
            offset -= e.Delta / 3; Arrange(); if (handled != null) handled.Handled = true;
        }
        protected override void OnMouseWheel(MouseEventArgs e) { ScrollWheel(e); base.OnMouseWheel(e); }
        private Rectangle Thumb()
        {
            int extent = Math.Max(1, Content.Height), h = Math.Max(Theme.U(this, 28), Height * Height / extent);
            return new Rectangle(Width - Theme.U(this, 5), (Height - h) * offset / Math.Max(1, extent - Height), Theme.U(this, 4), h);
        }
        protected override void OnPaint(PaintEventArgs e)
        { base.OnPaint(e); if (Content.Height > Height) { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; Theme.Fill(e.Graphics, Theme.Track, Thumb(), Theme.U(this, 2)); } }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.X >= Width - Theme.U(this, 14) && Content.Height > Height) { dragY = e.Y; dragOffset = offset; Capture = true; } }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (Capture) { offset = dragOffset + (e.Y - dragY) * Math.Max(1, Content.Height - Height) / Math.Max(1, Height - Thumb().Height); Arrange(); } }
        protected override void OnMouseUp(MouseEventArgs e) { Capture = false; base.OnMouseUp(e); }
    }
}
