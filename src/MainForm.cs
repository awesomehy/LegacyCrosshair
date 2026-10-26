using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LegacyCrosshair
{
    internal sealed partial class MainForm : Form
    {
        private Settings settings;
        private readonly List<Action> refreshers = new List<Action>();
        private bool updating, dirty, enabled, demo, heldPreview, ending, updateQueued, runtimeReady, exitRequested;
        private bool? appliedDynamicMode;
        private int revision;
        private double lastEdit, lastTick, lastTarget = -1;
        private string saveError = "", startupWarning = "", overlayError = "";
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 16 };
        private readonly RawKeyboardInput keyboard;
        private readonly OverlayWindow overlay = new OverlayWindow();
        private readonly MovementModel liveModel = new MovementModel(), demoModel = new MovementModel();
        private readonly CrosshairFrameCache liveCache = new CrosshairFrameCache(), demoCache = new CrosshairFrameCache();
        private readonly Cs2WindowTracker targetTracker = new Cs2WindowTracker();
        private readonly ForegroundWatcher foreground = new ForegroundWatcher();
        private readonly PreviewPanel preview = new PreviewPanel();
        private CrosshairFrame liveFrame, demoFrame;
        private MovementKeys lastLiveKeys, lastDemoKeys;
        private Rectangle output;
        private bool cs2Foreground, cs2Found, missingMonitor;
        private Button overlayButton, demoButton, holdButton;
        private Label status;
        private ChoiceField monitorBox;
        private SegmentedPicker themePicker, navigation;
        private readonly List<SettingsScroll> pages = new List<SettingsScroll>();
        private readonly NotifyIcon tray;
        private readonly Icon applicationIcon;
        private readonly ToolTip tooltip = new ToolTip();

        public MainForm()
        {
            settings = new Settings();
            if (File.Exists(ConfigStore.FilePath))
            {
                try { settings = ConfigStore.Read(ConfigStore.FilePath); }
                catch (Exception ex) { startupWarning = "配置读取失败，使用默认值：" + ex.Message; }
            }
            Text = "Legacy Crosshair · 旧版 CS 准星"; Name = "LegacyCrosshairSettings";
            Theme.SetMode(settings.UiTheme);
            Font = Theme.Font(10, FontStyle.Regular); BackColor = Theme.Background; ForeColor = Theme.Text;
            AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(1180, 840); MinimumSize = new Size(1000, 720);
            StartPosition = FormStartPosition.CenterScreen;
            using (Stream iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("LegacyCrosshair.App.ico"))
            {
                if (iconStream == null) throw new InvalidOperationException("缺少应用图标资源，请重新构建完整版本。");
                applicationIcon = new Icon(iconStream);
            }
            Icon = applicationIcon;
            BuildUi(); RefreshUi(); Theme.Apply(this);
            HandleCreated += delegate { UpdateChrome(); };
            keyboard = new RawKeyboardInput(settings.Dynamic && settings.Listen);
            keyboard.StateChanged += InputChanged;
            foreground.Changed += delegate { lastTarget = -1; RequestUpdate(); };
            bool toggleRegistered = Native.RegisterHotKey(keyboard.Handle, 1, 0x4003, 0x58);
            bool settingsRegistered = Native.RegisterHotKey(keyboard.Handle, 2, 0x4003, 0x53);
            if (!toggleRegistered || !settingsRegistered) startupWarning = "部分全局快捷键被占用，可使用界面或托盘操作。";
            keyboard.Hotkey += delegate(int id) { if (id == 1) ToggleOverlay(); else ShowSettings(); };
            tray = new NotifyIcon { Icon = Icon, Text = "Legacy Crosshair · 旧版准星", Visible = true };
            var menu = new ContextMenuStrip();
            menu.Items.Add("显示设置", null, delegate { ShowSettings(); });
            menu.Items.Add("开启 / 关闭准星  Ctrl+Alt+X", null, delegate { ToggleOverlay(); });
            menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("退出", null, delegate { RequestExit(); });
            tray.ContextMenuStrip = menu; tray.DoubleClick += delegate { ShowSettings(); };
            timer.Tick += Tick;
            Shown += delegate {
                Rectangle work = Screen.FromControl(this).WorkingArea;
                Size = new Size(Math.Min(Width, work.Width), Math.Min(Height, work.Height));
                Location = new Point(work.Left + (work.Width - Width) / 2, work.Top + (work.Height - Height) / 2);
                RefreshMonitors(); ResolveOutput(); lastTick = clock.Elapsed.TotalSeconds; runtimeReady = true; RequestUpdate();
            };
            Resize += delegate { if (WindowState == FormWindowState.Minimized && tray != null) { Hide(); } };
            VisibleChanged += delegate { if (!Visible) { heldPreview = false; preview.SuspendDrawing(); } RequestUpdate(); };
        }

        private void SetColor(Color color) { settings.Red = color.R; settings.Green = color.G; settings.Blue = color.B; Changed(); }
        private void RefreshUi() { updating = true; try { foreach (var refresh in refreshers) refresh(); } finally { updating = false; } UpdateMode(); Theme.SetMode(settings.UiTheme); Theme.Apply(this); UpdateChrome(); }
        private void UpdateChrome() { if (IsHandleCreated) Native.ThemeWindow(Handle, Theme.Dark, Theme.Background, Theme.Text); }
        private void Changed()
        {
            settings.Validate(); revision++; dirty = true; lastEdit = clock.Elapsed.TotalSeconds; lastTarget = -1; startupWarning = "";
            UpdateMode();
            RequestUpdate();
        }
        private void ApplyPreset(int index)
        {
            settings.Length = index == 3 ? 1.5 : 2.5; settings.Thickness = 0.5; settings.Gap = index == 3 ? -3 : -2;
            settings.DrawOutline = true; settings.Outline = 1; settings.Dot = index == 4; settings.TStyle = false; settings.Alpha = 255;
            settings.Dynamic = index == 2; if (index == 4) settings.Length = 0;
            Color color = index == 3 ? Color.Cyan : index == 4 ? Color.White : Theme.CrosshairGreen;
            settings.Red = color.R; settings.Green = color.G; settings.Blue = color.B;
            liveModel.Reset(); demoModel.Reset(); RefreshUi(); Changed();
        }
        private void ToggleOverlay()
        { enabled = !enabled; overlayError = ""; overlayButton.Text = enabled ? "关闭覆盖层" : "开启覆盖层"; if (!enabled) { overlay.Hide(); overlay.ReleaseSurface(); } lastTarget = -1; RequestUpdate(); }
        private void ShowSettings() { Show(); WindowState = FormWindowState.Normal; Activate(); }
        private void RequestExit() { exitRequested = true; Close(); }
        private void RefreshMonitors()
        {
            updating = true;
            try
            {
                monitorBox.Items.Clear(); int selected = -1;
                foreach (Screen screen in Screen.AllScreens)
                {
                    int index = monitorBox.Items.Count; monitorBox.Items.Add(new MonitorItem(screen)); if (screen.DeviceName == settings.Monitor) selected = index;
                }
                if (selected < 0) for (int i = 0; i < monitorBox.Items.Count; i++) if (((MonitorItem)monitorBox.Items[i]).Screen.Primary) selected = i;
                if (monitorBox.Items.Count > 0) monitorBox.SelectedIndex = Math.Max(0, selected);
                monitorBox.Invalidate();
            }
            finally { updating = false; }
        }
        private void ResolveOutput()
        {
            Screen target = Screen.PrimaryScreen; missingMonitor = !String.IsNullOrEmpty(settings.Monitor);
            foreach (Screen screen in Screen.AllScreens) if (screen.DeviceName == settings.Monitor) { target = screen; missingMonitor = false; }
            Rectangle game = Rectangle.Empty; cs2Found = settings.FollowCs2 && (enabled || Visible) && targetTracker.Read(clock.Elapsed.TotalSeconds, out game);
            output = target.Bounds;
            if (settings.FollowCs2 && cs2Found) output = game;
            cs2Foreground = settings.OnlyCs2 && Native.IsCs2Foreground();
        }
        private void RequestUpdate()
        {
            if (ending || !runtimeReady || updateQueued || !IsHandleCreated) return;
            if (!timer.Enabled) lastTick = clock.Elapsed.TotalSeconds;
            updateQueued = true;
            BeginInvoke(new Action(delegate { updateQueued = false; if (!ending) Tick(this, EventArgs.Empty); }));
        }
        private void InputChanged()
        {
            if (!runtimeReady || ending || !settings.Dynamic) return;
            // The slow steady-state timer must not apply a new direction to time
            // that elapsed before the key event. Advance the previous input first.
            double now = clock.Elapsed.TotalSeconds, elapsed = Math.Max(0, now - lastTick);
            if (Visible || overlay.Visible) liveModel.Step(lastLiveKeys, elapsed, settings);
            if (Visible && (demo || heldPreview)) demoModel.Step(lastDemoKeys, elapsed, settings);
            lastTick = now; RequestUpdate();
        }
        private void UpdateMode()
        {
            if (appliedDynamicMode == settings.Dynamic) return;
            appliedDynamicMode = settings.Dynamic;
            liveModel.Reset(); demoModel.Reset(); lastLiveKeys = lastDemoKeys = new MovementKeys();
            if (pages.Count > 1) pages[1].Content.Enabled = settings.Dynamic;
            if (holdButton != null) holdButton.Enabled = settings.Dynamic;
            if (demoButton != null) demoButton.Enabled = settings.Dynamic;
            if (!settings.Dynamic)
            {
                demo = heldPreview = false;
                if (preview.Frame == demoFrame) preview.Frame = null;
                demoCache.Clear(); demoFrame = null;
                if (demoButton != null) demoButton.Text = "自动演示";
                if (keyboard != null) keyboard.SetListening(false);
            }
        }
        private void Schedule(bool visible, bool show, bool listening, MovementKeys keys)
        {
            int interval = Int32.MaxValue;
            if (settings.Dynamic && ((show || visible) && liveModel.NeedsUpdate || visible && (demo || heldPreview))) interval = 16;
            if (visible && preview.PendingPaint) interval = Math.Min(interval, 34);
            if (listening && (!keyboard.Registered || keys.Any)) interval = Math.Min(interval, keyboard.Registered ? 150 : 16);
            if ((enabled || visible) && settings.FollowCs2) interval = Math.Min(interval, cs2Found ? 250 : 1000);
            if (enabled && settings.OnlyCs2 && !foreground.Registered) interval = Math.Min(interval, 500);
            if (dirty) interval = Math.Min(interval, 200);
            if (interval == Int32.MaxValue) timer.Stop();
            else { if (timer.Interval != interval) timer.Interval = interval; if (!timer.Enabled) timer.Start(); }
        }
        private void Tick(object sender, EventArgs e)
        {
#if UI_TEST
            RuntimeTicks++;
#endif
            if (ending) return;
            try
            {
                double now = clock.Elapsed.TotalSeconds, elapsed = Math.Max(0, now - lastTick); lastTick = now;
                bool visible = Visible && WindowState != FormWindowState.Minimized;
                foreground.SetEnabled(enabled && settings.OnlyCs2 || (enabled || visible) && settings.FollowCs2);
                double targetInterval = settings.FollowCs2 ? (cs2Found ? 0.25 : 1) : 0.5;
                if (lastTarget < 0 || (enabled || visible) && (settings.FollowCs2 || enabled && settings.OnlyCs2 && !foreground.Registered) && now - lastTarget >= targetInterval)
                { ResolveOutput(); lastTarget = now; }
                bool show = enabled && (!settings.OnlyCs2 || cs2Foreground) && (!settings.FollowCs2 || cs2Found);
                bool listening = settings.Dynamic && settings.Listen && (visible || show);
                if (settings.Dynamic) keyboard.SetListening(listening);
                MovementKeys liveKeys = new MovementKeys(), demoKeys = new MovementKeys();
                bool demonstrating = settings.Dynamic && visible && (heldPreview || demo);
                if (settings.Dynamic)
                {
                    liveKeys = keyboard.Read(listening);
                    if (visible || show) liveModel.Step(liveKeys, elapsed, settings); else liveModel.Reset();
                    lastLiveKeys = liveKeys;
                    if (demonstrating)
                    {
                        double phase = now % 6;
                        demoKeys = heldPreview ? new MovementKeys(true, false, false, false) :
                            new MovementKeys(phase < 1.7, phase >= 2.4 && phase < 4, false, phase >= 4 && phase < 4.3);
                        demoModel.Step(demoKeys, elapsed, settings); lastDemoKeys = demoKeys;
                    }
                    else { demoModel.Reset(); lastDemoKeys = new MovementKeys(); }
                }
                CrosshairFrame previous = liveFrame;
                if (visible || show) liveFrame = liveCache.Get(settings, liveModel.Amount, output, revision);
                else { liveCache.Clear(); liveFrame = null; }
                bool changed = liveFrame != previous;
                if (show && (changed || !overlay.Visible)) overlay.Present(liveFrame);
                if (!show && overlay.Visible) overlay.Hide();
                if (settings.Dynamic)
                {
                    if (demonstrating) demoFrame = demoCache.Get(settings, demoModel.Amount, output, revision);
                    else { demoCache.Clear(); demoFrame = null; }
                }
                if (visible)
                {
                    preview.UpdateState(settings, demonstrating ? demoFrame : liveFrame, output, demonstrating ? demoKeys : liveKeys,
                        demonstrating ? demoModel.Amount : liveModel.Amount, demonstrating,
                        !settings.Dynamic ? "静态模式 · WASD 已停用" : !settings.Listen ? "WASD 监听已关闭" : keyboard.Registered ? "全局 WASD 已就绪" : "WASD 兼容监听", revision);
                    preview.FlushPaint(now);
                }
                else preview.Frame = null;
                if (dirty && now - lastEdit > 0.65) Save();
                string message = !enabled ? "准星已关闭，预览可用" : show ? "准星已开启 · 点击穿透" : "准星已开启，等待 CS2 窗口";
                if (missingMonitor) message += " · 显示器离线，使用主屏";
                if (!String.IsNullOrEmpty(startupWarning)) message = startupWarning;
                if (!String.IsNullOrEmpty(saveError)) message = saveError;
                if (!String.IsNullOrEmpty(overlayError)) message = overlayError;
                if (status.Text != message) status.Text = message;
                Schedule(visible, show, listening, liveKeys);
            }
            catch (Exception ex)
            {
                preview.Frame = liveFrame;
                enabled = false; overlay.Hide(); overlayButton.Text = "开启覆盖层";
                overlayError = "覆盖层已停止：" + ex.Message; status.Text = overlayError;
                timer.Stop();
            }
        }
#if UI_TEST
        internal int RuntimeTicks;
#endif
        private void Save()
        {
            try { ConfigStore.Write(ConfigStore.FilePath, settings); dirty = false; saveError = ""; }
            catch (Exception ex) { saveError = "设置未保存：" + ex.Message; dirty = false; }
        }
        private void Import()
        {
            using (var dialog = new OpenFileDialog { Filter = "准星配置 (*.json)|*.json", Title = "导入准星配置" })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try { settings = ConfigStore.Read(dialog.FileName); liveModel.Reset(); demoModel.Reset(); RefreshUi(); RefreshMonitors(); Changed(); }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                }
        }
        private void Export()
        {
            using (var dialog = new SaveFileDialog { Filter = "准星配置 (*.json)|*.json", FileName = "legacy-crosshair.json", Title = "导出准星配置" })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try { ConfigStore.Write(dialog.FileName, settings); }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                }
        }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x007E && monitorBox != null) { lastTarget = -1; RefreshMonitors(); RequestUpdate(); }
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel) return;
            if (e.CloseReason == CloseReason.UserClosing && !exitRequested)
            {
                e.Cancel = true;
                if (dirty) Save();
                Hide(); return;
            }
            ending = true; timer.Stop(); if (dirty) Save();
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            overlay.Dispose(); foreground.Dispose(); keyboard.Dispose(); preview.Frame = null;
            liveCache.Dispose(); demoCache.Dispose();
            tray.Visible = false; tray.ContextMenuStrip.Dispose(); tray.Dispose(); timer.Dispose(); tooltip.Dispose();
            base.OnFormClosed(e); applicationIcon.Dispose();
        }
        private sealed class MonitorItem
        {
            public Screen Screen;
            public MonitorItem(Screen screen) { Screen = screen; }
            public override string ToString() { return Screen.DeviceName.Replace("\\\\.\\", "") + "  ·  " + Screen.Bounds.Width + "×" + Screen.Bounds.Height + (Screen.Primary ? "  主屏" : ""); }
        }
    }
}
