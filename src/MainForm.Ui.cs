using System;
using System.Drawing;
using System.Windows.Forms;

namespace LegacyCrosshair
{
    internal sealed partial class MainForm
    {
        private const int ContentWidth = 310;
        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(28, 22, 28, 16), Tag = "canvas" };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76)); Controls.Add(root);
            var header = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Tag = "canvas" }; root.Controls.Add(header, 0, 0);
            var mark = new LogoMark { Location = new Point(0, 6), Size = new Size(46, 46) }; header.Controls.Add(mark);
            var title = Theme.Label("Legacy Crosshair", 21, Theme.Text); title.Font = Theme.Font(21, FontStyle.Bold); title.Location = new Point(60, 0); header.Controls.Add(title);
            var subtitle = Theme.Label("旧版 CS 准星  /  让熟悉的手感回来", 10, Theme.Muted); subtitle.Location = new Point(63, 41); header.Controls.Add(subtitle);
            themePicker = new SegmentedPicker { Items = new[] { "明亮", "深色" }, Width = 206, Height = 40, AccessibleName = "界面主题", Anchor = AnchorStyles.Right | AnchorStyles.Top };
            themePicker.Location = new Point(header.Width - 206, 14); header.Controls.Add(themePicker);
            header.Resize += delegate { themePicker.Left = header.Width - themePicker.Width; };
            themePicker.SelectedIndexChanged += delegate { if (!updating) SetTheme(themePicker.SelectedIndex == 1 ? "dark" : "light"); };
            refreshers.Add(delegate { themePicker.SelectedIndex = settings.UiTheme == "dark" ? 1 : 0; });
            tooltip.SetToolTip(themePicker, "简约 · 明亮 / 简约 · 深色，自动记住你的选择");

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty, Tag = "canvas" };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.Controls.Add(body, 0, 1);
            var card = new SurfaceCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 20, 0), Padding = new Padding(20, 18, 16, 14) }; body.Controls.Add(card, 0, 0);
            var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 72)); left.RowStyles.Add(new RowStyle(SizeType.Absolute, 56)); left.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); card.Controls.Add(left);
            var presetRow = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty }; left.Controls.Add(presetRow, 0, 0);
            presetRow.Controls.Add(Theme.Label("快速预设", 9, Theme.Muted));
            var preset = NewCombo(); preset.SetBounds(0, 23, ContentWidth, 38); preset.AccessibleName = "快速预设";
            preset.Items.AddRange(new object[] { "自定义准星", "经典静态 · 绿色", "经典动态 · 移动反馈", "极简静态 · 青色", "中心点 · 白色" }); preset.SelectedIndex = 0;
            preset.SelectedIndexChanged += delegate { if (preset.SelectedIndex > 0) { ApplyPreset(preset.SelectedIndex); preset.SelectedIndex = 0; } }; presetRow.Controls.Add(preset);
            navigation = new SegmentedPicker { Items = new[] { "准星", "动态", "显示" }, Width = ContentWidth, Height = 40, AccessibleName = "设置分类", Margin = Padding.Empty };
            left.Controls.Add(navigation, 0, 1);
            var pageHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty }; left.Controls.Add(pageHost, 0, 2);
            FlowLayoutPanel appearance = AddPage(pageHost), dynamics = AddPage(pageHost), display = AddPage(pageHost);
            navigation.SelectedIndexChanged += delegate { SelectPage(navigation.SelectedIndex); }; SelectPage(0);

            AddChoice(appearance, "准星样式", new[] { "经典静态  ·  style 4", "经典动态  ·  style 2 模拟" },
                delegate { return settings.Dynamic ? 1 : 0; }, delegate(int n) { settings.Dynamic = n == 1; liveModel.Reset(); demoModel.Reset(); });
            AddNumber(appearance, "长度", delegate { return settings.Length; }, delegate(double v) { settings.Length = v; }, 0, 30, 1, 0.1);
            AddNumber(appearance, "粗细", delegate { return settings.Thickness; }, delegate(double v) { settings.Thickness = v; }, 0, 10, 1, 0.1);
            AddNumber(appearance, "间距", delegate { return settings.Gap; }, delegate(double v) { settings.Gap = v; }, -20, 30, 1, 0.1);
            AddColors(appearance);
            AddCheck(appearance, "黑色描边", delegate { return settings.DrawOutline; }, delegate(bool v) { settings.DrawOutline = v; });
            var advancedAppearance = AddDisclosure(appearance, "更多外观设置");
            AddNumber(advancedAppearance, "描边宽度", delegate { return settings.Outline; }, delegate(double v) { settings.Outline = v; }, 0, 3, 1, 0.5);
            AddCheck(advancedAppearance, "中心点", delegate { return settings.Dot; }, delegate(bool v) { settings.Dot = v; });
            AddCheck(advancedAppearance, "T 形准星", delegate { return settings.TStyle; }, delegate(bool v) { settings.TStyle = v; });
            AddNumber(advancedAppearance, "不透明度", delegate { return settings.Alpha; }, delegate(double v) { settings.Alpha = (int)v; }, 0, 255, 0, 1);
            AddNote(appearance, "参数修改即时生效。间距支持负值。", 38);

            AddCheck(dynamics, "全局 WASD 监听", delegate { return settings.Listen; }, delegate(bool v) { settings.Listen = v; if (keyboard != null) keyboard.Clear(); liveModel.Reset(); });
            AddNote(dynamics, "选择经典动态后，按下 WASD 即可预览移动反馈。", 50);
            AddNumber(dynamics, "移动扩散", delegate { return settings.Spread; }, delegate(double v) { settings.Spread = v; }, 0, 30, 1, 0.1);
            AddNumber(dynamics, "加速 · ms", delegate { return settings.AccelerationMs; }, delegate(double v) { settings.AccelerationMs = v; }, 10, 600, 0, 5);
            AddNumber(dynamics, "刹停 · ms", delegate { return settings.BrakingMs; }, delegate(double v) { settings.BrakingMs = v; }, 10, 600, 0, 5);
            AddNumber(dynamics, "收缩 · ms", delegate { return settings.RecoveryMs; }, delegate(double v) { settings.RecoveryMs = v; }, 10, 600, 0, 5);
            AddCheck(dynamics, "动态内外分段", delegate { return settings.Split; }, delegate(bool v) { settings.Split = v; });
            var advancedDynamics = AddDisclosure(dynamics, "分段详细设置");
            AddNumber(advancedDynamics, "阈值 · px", delegate { return settings.SplitDistance; }, delegate(double v) { settings.SplitDistance = v; }, 0, 50, 0, 1);
            AddNumber(advancedDynamics, "外段比例", delegate { return settings.SplitRatio; }, delegate(double v) { settings.SplitRatio = v; }, 0, 1, 2, 0.05);
            AddNumber(advancedDynamics, "内段透明度", delegate { return settings.InnerAlpha; }, delegate(double v) { settings.InnerAlpha = v; }, 0, 1, 2, 0.05);
            AddNumber(advancedDynamics, "外段透明度", delegate { return settings.OuterAlpha; }, delegate(double v) { settings.OuterAlpha = v; }, 0, 1, 2, 0.05);
            AddNote(dynamics, "时间为响应常数；动态效果基于按键模拟。", 42);

            var monitorRow = Row(76); monitorRow.Controls.Add(Theme.Label("目标显示器", 9, Theme.Muted));
            var refresh = Theme.Button("刷新", false); refresh.SetBounds(254, -3, 56, 24); refresh.Font = Theme.Font(8, FontStyle.Regular);
            refresh.Click += delegate { RefreshMonitors(); Changed(); }; monitorRow.Controls.Add(refresh);
            monitorBox = NewCombo(); monitorBox.SetBounds(0, 25, ContentWidth, 40); monitorBox.AccessibleName = "目标显示器"; monitorRow.Controls.Add(monitorBox); display.Controls.Add(monitorRow);
            monitorBox.SelectedIndexChanged += delegate { if (!updating && monitorBox.SelectedItem is MonitorItem) { settings.Monitor = ((MonitorItem)monitorBox.SelectedItem).Screen.DeviceName; Changed(); } };
            var resRow = Row(76); resRow.Controls.Add(Theme.Label("游戏渲染分辨率", 9, Theme.Muted)); var resolution = NewCombo(); resolution.AccessibleName = "游戏渲染分辨率";
            resolution.SetBounds(0, 25, ContentWidth, 40); resolution.Items.AddRange(new object[] { "自定义分辨率", "1024 × 768  ·  4:3", "1280 × 960  ·  4:3", "1440 × 1080  ·  4:3", "1280 × 1024  ·  5:4", "1280 × 720  ·  16:9", "1600 × 900  ·  16:9", "1920 × 1080  ·  16:9", "2560 × 1440  ·  16:9", "3840 × 2160  ·  16:9" });
            int[,] resolutions = { {0,0}, {1024,768}, {1280,960}, {1440,1080}, {1280,1024}, {1280,720}, {1600,900}, {1920,1080}, {2560,1440}, {3840,2160} };
            resolution.SelectedIndexChanged += delegate { if (updating || resolution.SelectedIndex <= 0) return; settings.GameWidth = resolutions[resolution.SelectedIndex, 0]; settings.GameHeight = resolutions[resolution.SelectedIndex, 1]; RefreshUi(); Changed(); };
            refreshers.Add(delegate { int found = 0; for (int i = 1; i < resolutions.GetLength(0); i++) if (settings.GameWidth == resolutions[i, 0] && settings.GameHeight == resolutions[i, 1]) found = i; resolution.SelectedIndex = found; });
            resRow.Controls.Add(resolution); display.Controls.Add(resRow);
            AddNumber(display, "自定义宽度", delegate { return settings.GameWidth; }, delegate(double v) { settings.GameWidth = (int)v; RefreshUi(); }, 320, 16384, 0, 1, false);
            AddNumber(display, "自定义高度", delegate { return settings.GameHeight; }, delegate(double v) { settings.GameHeight = (int)v; RefreshUi(); }, 240, 16384, 0, 1, false);
            AddChoice(display, "画面缩放", new[] { "铺满屏幕 / 4:3 拉伸", "保持比例 / 黑边" }, delegate { return settings.Mapping == "fit" ? 1 : 0; }, delegate(int n) { settings.Mapping = n == 1 ? "fit" : "stretch"; });
            AddCheck(display, "对齐 CS2 窗口", delegate { return settings.FollowCs2; }, delegate(bool v) { settings.FollowCs2 = v; });
            AddCheck(display, "仅在 CS2 前台显示", delegate { return settings.OnlyCs2; }, delegate(bool v) { settings.OnlyCs2 = v; });
            AddNote(display, "游戏分辨率与画面缩放，请保持和游戏设置一致。", 48);
            var fileRow = Row(44); var import = Theme.Button("导入配置", false); var export = Theme.Button("导出配置", false);
            import.SetBounds(0, 0, 150, 38); export.SetBounds(160, 0, 150, 38); import.Click += delegate { Import(); }; export.Click += delegate { Export(); }; fileRow.Controls.Add(import); fileRow.Controls.Add(export); display.Controls.Add(fileRow);

            var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Margin = Padding.Empty, Tag = "canvas" };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); right.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); right.RowStyles.Add(new RowStyle(SizeType.Absolute, 56)); body.Controls.Add(right, 1, 0);
            var tools = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Tag = "canvas" }; right.Controls.Add(tools, 0, 0);
            var background = NewCombo(); background.SetBounds(0, 0, 152, 38); background.AccessibleName = "预览背景"; background.Items.AddRange(new object[] { "训练场", "深色背景", "浅色背景", "棋盘背景" });
            string[] backgrounds = { "range", "dark", "light", "grid" };
            background.SelectedIndexChanged += delegate { if (!updating && background.SelectedIndex >= 0) { settings.Background = backgrounds[background.SelectedIndex]; Changed(); } };
            refreshers.Add(delegate { background.SelectedIndex = Array.IndexOf(backgrounds, settings.Background); }); tools.Controls.Add(background);
            var full = new ToggleSwitch { Text = "全屏构图", Width = 172, Height = 38, Location = new Point(176, 0) };
            full.CheckedChanged += delegate { if (!updating) { settings.FullPreview = full.Checked; Changed(); } };
            refreshers.Add(delegate { full.Checked = settings.FullPreview; }); tools.Controls.Add(full);
            preview.Dock = DockStyle.Fill; preview.Margin = Padding.Empty; right.Controls.Add(preview, 0, 1);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 14, 0, 0), Tag = "canvas" };
            var hold = Theme.Button("按住模拟移动", false); hold.Width = 150; holdButton = hold;
            hold.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { heldPreview = true; hold.Capture = true; RequestUpdate(); } };
            hold.MouseUp += delegate { heldPreview = false; RequestUpdate(); }; hold.MouseCaptureChanged += delegate { if (!hold.Capture) { heldPreview = false; RequestUpdate(); } };
            demoButton = Theme.Button("自动演示", false); demoButton.Width = 136;
            demoButton.Click += delegate { demo = !demo; demoButton.Text = demo ? "停止演示" : "自动演示"; demoModel.Reset(); RequestUpdate(); };
            actions.Controls.Add(hold); actions.Controls.Add(demoButton); right.Controls.Add(actions, 0, 2);
            tooltip.SetToolTip(hold, "仅驱动预览；覆盖层始终使用真实 WASD 输入。");

            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = new Padding(0, 17, 0, 0), Tag = "canvas" };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.Controls.Add(footer, 0, 2);
            status = Theme.Label("正在初始化…", 10, Theme.Text); status.AutoSize = false; status.Dock = DockStyle.Fill; status.AutoEllipsis = true; footer.Controls.Add(status, 0, 0);
            var hint = Theme.Label("Ctrl+Alt+X  切换准星     Ctrl+Alt+S  显示设置     ·     v1.0.0", 8.5f, Theme.Muted); hint.Dock = DockStyle.Fill; footer.Controls.Add(hint, 0, 1);
            overlayButton = Theme.Button("开启覆盖层", true); overlayButton.Dock = DockStyle.Fill; overlayButton.Margin = new Padding(0, 0, 0, 8);
            overlayButton.Click += delegate { ToggleOverlay(); }; footer.Controls.Add(overlayButton, 1, 0); footer.SetRowSpan(overlayButton, 2);
        }

        private void SetTheme(string mode)
        { settings.UiTheme = mode; Theme.SetMode(mode); Theme.Apply(this); UpdateChrome(); Changed(); }
        private void SelectPage(int index)
        { for (int i = 0; i < pages.Count; i++) pages[i].Visible = i == index; if (index >= 0 && index < pages.Count) pages[index].BringToFront(); }
        private ChoiceField NewCombo() { return new ChoiceField(); }
        private FlowLayoutPanel AddPage(Panel host)
        { var page = new SettingsScroll { Dock = DockStyle.Fill, Margin = Padding.Empty }; host.Controls.Add(page); pages.Add(page); return page.Content; }
        private Panel Row(int height) { return new Panel { Width = ContentWidth, Height = height, Margin = new Padding(0, 0, 0, 4) }; }
        private void AddNote(FlowLayoutPanel parent, string text, int height)
        { parent.Controls.Add(new Label { Text = text, Width = ContentWidth, Height = height, ForeColor = Theme.Muted, Font = Theme.Font(9, FontStyle.Regular), Tag = "muted", BackColor = Color.Transparent, Margin = new Padding(0, 10, 0, 0) }); }
        private void AddCheck(FlowLayoutPanel parent, string text, Func<bool> getter, Action<bool> setter)
        { var box = new ToggleSwitch { Text = text, Width = ContentWidth, AccessibleName = text }; box.CheckedChanged += delegate { if (!updating) { setter(box.Checked); Changed(); } }; refreshers.Add(delegate { box.Checked = getter(); }); parent.Controls.Add(box); }
        private void AddChoice(FlowLayoutPanel parent, string text, string[] items, Func<int> getter, Action<int> setter)
        {
            var row = Row(76); row.Controls.Add(Theme.Label(text, 9, Theme.Muted)); var combo = NewCombo(); combo.AccessibleName = text; combo.SetBounds(0, 25, ContentWidth, 40); combo.Items.AddRange(items);
            combo.SelectedIndexChanged += delegate { if (!updating) { setter(combo.SelectedIndex); Changed(); } }; refreshers.Add(delegate { combo.SelectedIndex = getter(); }); row.Controls.Add(combo); parent.Controls.Add(row);
        }
        private void AddNumber(FlowLayoutPanel parent, string text, Func<double> getter, Action<double> setter, double min, double max, int decimals, double increment, bool slider = true)
        {
            var row = Row(48); var label = Theme.Label(text, 10, Theme.Text); label.Location = new Point(0, 9); row.Controls.Add(label);
            var number = new NumberField { Minimum = (decimal)min, Maximum = (decimal)max, DecimalPlaces = decimals, Increment = (decimal)increment, AccessibleName = text };
            number.SetBounds(slider ? 214 : 190, 0, slider ? 96 : 120, 36); number.Value = (decimal)getter();
            ValueSlider track = null; bool syncing = false;
            if (slider)
            {
                track = new ValueSlider { Minimum = 0, Maximum = (int)Math.Round((max - min) / increment), Left = 98, Top = 2, Width = 102, Height = 32, AccessibleName = text + "滑块" };
                track.ValueChanged += delegate { if (updating || syncing) return; syncing = true; number.Value = (decimal)(min + track.Value * increment); syncing = false; setter((double)number.Value); Changed(); }; row.Controls.Add(track);
            }
            number.ValueChanged += delegate { if (updating || syncing) return; syncing = true; if (track != null) track.Value = (int)Math.Round(((double)number.Value - min) / increment); syncing = false; setter((double)number.Value); Changed(); };
            refreshers.Add(delegate { number.Value = (decimal)getter(); if (track != null) track.Value = (int)Math.Round((getter() - min) / increment); }); row.Controls.Add(number); parent.Controls.Add(row);
        }
        private FlowLayoutPanel AddDisclosure(FlowLayoutPanel parent, string title)
        {
            var panel = Row(44); var button = Theme.Button(title + "  +", false); button.Width = ContentWidth; button.Height = 36; panel.Controls.Add(button);
            var content = new FlowLayoutPanel { Width = ContentWidth, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Location = new Point(0, 48), Margin = Padding.Empty, Visible = false }; panel.Controls.Add(content);
            content.SizeChanged += delegate { if (content.Visible) panel.Height = content.Bottom + 4; };
            button.Click += delegate { content.Visible = !content.Visible; panel.Height = content.Visible ? content.Bottom + 4 : 44; button.Text = title + (content.Visible ? "  −" : "  +"); };
            parent.Controls.Add(panel); return content;
        }
        private void AddColors(FlowLayoutPanel parent)
        {
            var row = Row(72); row.Controls.Add(Theme.Label("准星颜色", 9, Theme.Muted));
            Color[] colors = { Theme.CrosshairGreen, Color.Cyan, Color.Yellow, Color.Magenta, Color.White };
            for (int i = 0; i < colors.Length; i++)
            {
                Color color = colors[i]; var button = Theme.Button("", false); button.Swatch = true; button.SwatchColor = color; button.SetBounds(i * 41, 26, 33, 33);
                button.AccessibleName = "颜色 " + color.Name; button.Click += delegate { SetColor(color); }; row.Controls.Add(button);
            }
            var custom = Theme.Button("自定义", false); custom.SetBounds(215, 25, 95, 36); custom.Click += delegate { using (var dialog = new ColorDialog { Color = Color.FromArgb(settings.Red, settings.Green, settings.Blue), FullOpen = true }) if (dialog.ShowDialog(this) == DialogResult.OK) SetColor(dialog.Color); }; row.Controls.Add(custom); parent.Controls.Add(row);
        }
    }

    internal sealed class LogoMark : Control, IThemeAware
    {
        public void ApplyTheme() { Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Theme.Background); g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Theme.Fill(g, Theme.AccentSoft, ClientRectangle, Theme.U(this, 13)); float cx = Width / 2f, cy = Height / 2f, near = Theme.U(this, 5), far = Theme.U(this, 12);
            using (var pen = new Pen(Theme.Accent, Theme.U(this, 2))) { g.DrawLine(pen, cx - far, cy, cx - near, cy); g.DrawLine(pen, cx + near, cy, cx + far, cy); g.DrawLine(pen, cx, cy - far, cx, cy - near); g.DrawLine(pen, cx, cy + near, cx, cy + far); }
        }
    }
}
