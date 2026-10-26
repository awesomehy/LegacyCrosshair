using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LegacyCrosshair
{
    internal sealed partial class MainForm
    {
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr TestWindowStyle(IntPtr handle, int index);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out Native.Rect rect);
        [DllImport("user32.dll")] private static extern IntPtr GetThreadDpiAwarenessContext();
        [DllImport("user32.dll")] private static extern bool AreDpiAwarenessContextsEqual(IntPtr first, IntPtr second);
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        private int smokeStep, smokeChecks;
        private IntPtr beforeOverlay;
        private int themeRasterHash;
        private int idleTicks, idlePresents, idleRenders, idleSearches;
        private double savedDynamicSpread;
        private ChoiceField testChoice;
        private System.Windows.Forms.Timer smokeTimer;
        private static readonly string Artifacts = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "artifacts"));
        internal int SmokeExitCode;

        private void SmokeCheck(bool condition, string message)
        { if (!condition) throw new Exception(message); smokeChecks++; Console.WriteLine("PASS " + message); }
        internal void StartSmoke()
        {
            Directory.CreateDirectory(Artifacts);
            settings = new Settings(); RefreshUi(); Changed();
            smokeTimer = new System.Windows.Forms.Timer { Interval = 250 };
            smokeTimer.Tick += delegate {
                try { SmokeNext(); }
                catch (Exception ex) { Console.Error.WriteLine(ex); SmokeExitCode = 1; smokeTimer.Stop(); RequestExit(); }
            };
            smokeTimer.Start();
        }
        private IEnumerable<Control> Walk(Control parent)
        {
            foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Walk(child)) yield return nested; }
        }
        private void RenderForm(string name)
        {
            using (var bitmap = new Bitmap(Width, Height))
            { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(Path.Combine(Artifacts, name)); }
        }
        private void CheckTabWidth()
        {
            foreach (var page in pages) if (page.Visible)
                SmokeCheck(page.Content.Width <= page.Width - 6 && !page.HorizontalScroll.Visible, "Settings content fits without horizontal scrolling: " + navigation.SelectedIndex);
        }
        private int RasterHash()
        { unchecked { int hash = 17; for (int y = 0; y < liveFrame.Bitmap.Height; y++) for (int x = 0; x < liveFrame.Bitmap.Width; x++) hash = hash * 31 + liveFrame.Bitmap.GetPixel(x, y).ToArgb(); return hash; } }
        private void RenderPopup(string name)
        { using (var bitmap = new Bitmap(testChoice.TestPopup.Width, testChoice.TestPopup.Height)) { testChoice.TestPopup.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(Artifacts, name)); } }
        private void ControlKey(Control control, Keys key)
        { control.GetType().GetMethod("OnKeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(control, new object[] { new KeyEventArgs(key) }); }
        private void SmokeNext()
        {
            switch (smokeStep++)
            {
                case 0:
                    SmokeCheck(AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), new IntPtr(-4)), "Application uses per-monitor V2 physical-pixel awareness");
                    using (var embeddedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
                    using (var iconBitmap = embeddedIcon.ToBitmap())
                    {
                        Color center = iconBitmap.GetPixel(iconBitmap.Width / 2, iconBitmap.Height / 2);
                        SmokeCheck(center.B > 150 && center.B > center.R * 1.5, "Executable file embeds the blue crosshair icon");
                    }
                    SmokeCheck(Icon == applicationIcon && tray.Icon == applicationIcon && Icon.Handle != SystemIcons.Application.Handle,
                        "Settings window and notification area share the application icon");
                    SmokeCheck(SendMessage(Handle, 0x007F, new IntPtr(1), IntPtr.Zero) != IntPtr.Zero && SendMessage(Handle, 0x007F, IntPtr.Zero, IntPtr.Zero) != IntPtr.Zero,
                        "Native window exposes large and small taskbar/titlebar icons");
                    SmokeCheck(!keyboard.Registered && !holdButton.Enabled && !demoButton.Enabled && !pages[1].Content.Enabled, "Static mode disables Raw Input, dynamic settings and demonstration controls");
                    SmokeCheck(liveFrame != null && preview.Frame != null, "Live preview receives rendered frame");
                    int staticReads = keyboard.ReadCount, staticSteps = liveModel.StepCount + demoModel.StepCount;
                    for (int i = 0; i < 10; i++) Tick(this, EventArgs.Empty);
                    SmokeCheck(keyboard.ReadCount == staticReads && liveModel.StepCount + demoModel.StepCount == staticSteps && demoCache.Count == 0,
                        "Static update path never reads WASD, steps movement models or creates demo frames");
                    RenderForm("ui-default.png");
                    beforeOverlay = Native.GetForegroundWindow(); ToggleOverlay(); break;
                case 1:
                    SmokeCheck(overlay.Visible && overlayError == "", "Layered overlay presents successfully");
                    long style = TestWindowStyle(overlay.Handle, -20).ToInt64();
                    SmokeCheck((style & 0x00080020) == 0x00080020, "Overlay uses layered and click-through styles");
                    SmokeCheck((style & 0x08000008) == 0x08000008, "Overlay uses no-activate and topmost styles");
                    SmokeCheck(Native.GetForegroundWindow() != overlay.Handle && Native.GetForegroundWindow() == beforeOverlay, "Showing overlay preserves foreground focus");
                    Native.Rect rect; GetWindowRect(overlay.Handle, out rect);
                    SmokeCheck(rect.Left == liveFrame.Origin.X && rect.Top == liveFrame.Origin.Y && rect.Right - rect.Left == liveFrame.Bitmap.Width, "Native overlay bounds match shared pixel raster");
                    ToggleOverlay();
                    foreach (var control in Walk(this))
                    {
                        var combo = control as ChoiceField;
                        if (combo != null && combo.Items.Count == 2 && combo.Items[0].ToString().Contains("style 4")) { combo.SelectedIndex = 1; break; }
                    }
                    SmokeCheck(settings.Dynamic, "Style selector is connected to runtime settings");
                    foreach (var control in Walk(this))
                    {
                        var number = control as NumberField;
                        if (number != null && number.AccessibleName == "长度") { number.Value = 3.2M; break; }
                    }
                    SmokeCheck(Math.Abs(settings.Length - 3.2) < 0.001, "Numeric edits update crosshair settings");
                    demo = true; demoButton.Text = "自动演示：开"; heldPreview = true; break;
                case 2:
                    SmokeCheck(keyboard.Registered && holdButton.Enabled && demoButton.Enabled && pages[1].Content.Enabled, "Dynamic mode restores Raw Input and dynamic controls");
                    SmokeCheck(preview.Demo && demoModel.Amount > 0, "Movement demonstration animates preview");
                    SmokeCheck(preview.Frame == demoFrame && liveFrame != demoFrame, "Demo and live overlay use separate frames");
                    SmokeCheck(preview.Width > 450 && preview.Height > 400, "Preview layout has usable dimensions");
                    CheckTabWidth(); RenderForm("ui-appearance.png");
                    navigation.SelectedIndex = 1;
                    break;
                case 3:
                    CheckTabWidth(); RenderForm("ui-dynamics.png");
                    navigation.SelectedIndex = 2;
                    settings.Mapping = "fit"; RefreshUi(); Changed(); break;
                case 4:
                    SmokeCheck(preview.Frame.Mapping.ScaleX == preview.Frame.Mapping.ScaleY, "Letterbox edit reaches preview renderer");
                    CheckTabWidth(); RenderForm("ui-display.png");
                    foreach (var control in Walk(this))
                    {
                        var combo = control as ChoiceField;
                        if (combo != null && combo.AccessibleName == "游戏渲染分辨率")
                        {
                            int fourThree = 0; bool selectedPresetsWork = true;
                            for (int i = 1; i < combo.Items.Count; i++)
                            {
                                string label = combo.Items[i].ToString();
                                if (!label.Contains("4:3")) continue;
                                fourThree++; combo.SelectedIndex = i;
                                string[] dimensions = label.Split('·')[0].Split('×');
                                selectedPresetsWork &= settings.GameWidth == Int32.Parse(dimensions[0].Trim()) && settings.GameHeight == Int32.Parse(dimensions[1].Trim()) && settings.GameWidth * 3 == settings.GameHeight * 4;
                            }
                            SmokeCheck(fourThree == 13 && selectedPresetsWork, "All 13 labeled 4:3 presets set the matching render dimensions");
                            for (int i = 0; i < combo.Items.Count; i++) if (combo.Items[i].ToString().Contains("1920 × 1440")) combo.SelectedIndex = i;
                        }
                    }
                    SmokeCheck(settings.GameWidth == 1920 && settings.GameHeight == 1440, "1920x1440 4:3 resolution preset updates both game dimensions");
                    demo = heldPreview = false; demoButton.Text = "自动演示"; settings.Mapping = "stretch"; RefreshUi(); Changed(); Tick(this, EventArgs.Empty);
                    Point beforeCalibration = liveFrame.Origin; int beforeCalibrationHash = RasterHash();
                    foreach (var control in Walk(this)) if (control is Button && control.Text.StartsWith("中心位置校准")) ((Button)control).PerformClick();
                    foreach (var control in Walk(this))
                    {
                        var number = control as NumberField;
                        if (number != null && number.AccessibleName == "水平偏移 · px") number.Value = -1;
                        if (number != null && number.AccessibleName == "垂直偏移 · px") number.Value = 1;
                    }
                    Tick(this, EventArgs.Empty);
                    SmokeCheck(settings.OffsetX == -1 && settings.OffsetY == 1 && liveFrame.Origin == new Point(beforeCalibration.X - 1, beforeCalibration.Y + 1) && beforeCalibrationHash == RasterHash(),
                        "Center calibration controls translate the live raster by physical pixels");
                    SmokeCheck(preview.Frame == liveFrame, "Preview and overlay share the calibrated raster");
                    RenderForm("ui-center-calibration.png");
                    foreach (var control in Walk(this)) if (control is Button && control.Text == "重置中心位置") ((Button)control).PerformClick();
                    Tick(this, EventArgs.Empty);
                    SmokeCheck(settings.OffsetX == 0 && settings.OffsetY == 0 && liveFrame.Origin == beforeCalibration, "Reset center restores the original location exactly");
                    foreach (var control in Walk(this))
                    {
                        var number = control as NumberField;
                        if (number != null && number.AccessibleName == "自定义宽度") number.Value = 1300;
                    }
                    bool customSelected = false;
                    foreach (var control in Walk(this))
                    {
                        var combo = control as ChoiceField;
                        if (combo != null && combo.AccessibleName == "游戏渲染分辨率") customSelected = combo.SelectedIndex == 0;
                    }
                    SmokeCheck(customSelected && settings.GameWidth == 1300, "Custom resolution synchronizes the preset selector");
                    demo = heldPreview = false; settings.Dynamic = false; settings.OnlyCs2 = true; Changed(); ToggleOverlay(); break;
                case 5:
                    if (!Native.IsCs2Foreground()) SmokeCheck(!overlay.Visible, "CS2 foreground gate hides overlay on other applications");
                    settings.OnlyCs2 = false; settings.FollowCs2 = true; Changed(); break;
                case 6:
                    if (!cs2Found) SmokeCheck(!overlay.Visible, "Window alignment waits for a visible CS2 client");
                    settings.FollowCs2 = false; settings.Mapping = "stretch"; Changed(); break;
                case 7:
                    SmokeCheck(overlay.Visible, "Overlay recovers when display gate is removed");
                    ToggleOverlay(); demoButton.Text = "自动演示"; RefreshUi(); themeRasterHash = RasterHash(); navigation.SelectedIndex = 0; themePicker.SelectedIndex = 1; break;
                case 8:
                    SmokeCheck(settings.UiTheme == "dark" && Theme.Dark && BackColor == Theme.Background, "Theme selector updates the full application to dark mode");
                    SmokeCheck(themeRasterHash == RasterHash(), "Switching theme preserves the crosshair pixel raster");
                    RenderForm("ui-dark.png");
                    foreach (var control in Walk(this)) if (control is ChoiceField && control.AccessibleName == "准星样式") testChoice = (ChoiceField)control;
                    testChoice.OpenDropDown(); break;
                case 9:
                    SmokeCheck(testChoice.PopupVisible, "Custom dropdown opens as a real dismissible popup");
                    RenderPopup("ui-dropdown-dark.png"); testChoice.CloseDropDown(); themePicker.SelectedIndex = 0; break;
                case 10:
                    SmokeCheck(settings.UiTheme == "light" && !Theme.Dark && BackColor == Theme.Background, "Theme selector restores light mode");
                    SmokeCheck(themeRasterHash == RasterHash(), "Light-dark round trip retains crosshair geometry and color");
                    RenderForm("ui-light.png"); testChoice.OpenDropDown(); break;
                case 11:
                    RenderPopup("ui-dropdown-light.png");
                    Control popupList = ((ToolStripControlHost)testChoice.TestPopup.Items[0]).Control;
                    ControlKey(popupList, Keys.Down); ControlKey(popupList, Keys.Return);
                    SmokeCheck(settings.Dynamic && !testChoice.PopupVisible, "Dropdown keyboard selection updates style and dismisses the popup");
                    NumberField length = null;
                    foreach (var control in Walk(this)) if (control is NumberField && control.AccessibleName == "长度") length = (NumberField)control;
                    decimal oldValue = length.Value; length.Editor.Text = "invalid"; length.CommitText();
                    SmokeCheck(length.Value == oldValue, "Numeric field rejects invalid text without changing settings");
                    length.Editor.Text = 2.8M.ToString(System.Globalization.CultureInfo.CurrentCulture); length.CommitText();
                    SmokeCheck(Math.Abs(settings.Length - 2.8) < 0.001, "Custom numeric text input commits to runtime settings");
                    ControlKey(length.Editor, Keys.Up);
                    SmokeCheck(Math.Abs(settings.Length - 2.9) < 0.001, "Numeric field arrow keys apply the configured step");
                    foreach (var control in Walk(this)) if (control is Button && control.Text.StartsWith("更多外观设置")) ((Button)control).PerformClick();
                    break;
                case 12:
                    ToggleSwitch dot = null;
                    foreach (var control in Walk(this)) if (control is ToggleSwitch && control.Text == "中心点") dot = (ToggleSwitch)control;
                    SmokeCheck(dot != null && dot.Visible, "Advanced settings expand to expose secondary controls");
                    dot.Checked = true; SmokeCheck(settings.Dot, "Sliding switch updates crosshair settings");
                    dot.Focus();
                    Point dotLocation = pages[0].PointToClient(dot.PointToScreen(Point.Empty));
                    SmokeCheck(dotLocation.Y >= 0 && dotLocation.Y + dot.Height <= pages[0].Height, "Keyboard focus scrolls advanced settings into view");
                    int oldTop = pages[0].Content.Top;
                    dot.GetType().GetMethod("OnMouseWheel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(dot, new object[] { new HandledMouseEventArgs(MouseButtons.None, 0, 5, 5, -120) });
                    SmokeCheck(pages[0].Content.Top < oldTop, "Mouse wheel over a child control scrolls the settings pane");
                    CheckTabWidth(); RenderForm("ui-advanced-light.png");
                    Size = new Size(1000, 720); break;
                case 13:
                    SmokeCheck(preview.Width > 400 && preview.Height > 280, "Compact window keeps the preview usable");
                    CheckTabWidth(); RenderForm("ui-compact-light.png");
                    Save();
                    SmokeCheck(saveError == "" && ConfigStore.Read(ConfigStore.FilePath).UiTheme == "light", "Theme preference and crosshair parameters save together");
                    settings = new Settings(); settings.Listen = true; RefreshUi(); Changed(); Save();
                    demo = heldPreview = false; enabled = true; Tick(this, EventArgs.Empty);
                    Hide(); break;
                case 14:
                    SmokeCheck(!timer.Enabled && !keyboard.Registered && overlay.Visible, "Hidden static overlay sleeps and unregisters unused keyboard input");
                    idleTicks = RuntimeTicks; idlePresents = overlay.PresentCount; idleRenders = liveCache.RenderCount; idleSearches = targetTracker.SearchCount;
                    break;
                case 15:
                    SmokeCheck(RuntimeTicks == idleTicks && overlay.PresentCount == idlePresents && liveCache.RenderCount == idleRenders, "Idle static overlay does not tick, rerender or reupload its bitmap");
                    SmokeCheck(targetTracker.SearchCount == idleSearches, "Unused CS2 window alignment does not scan the process list");
                    Show(); settings.Dynamic = true; keyboard.TestKeys = new MovementKeys(true, false, false, false); Changed(); Save();
                    break;
                case 16:
                    SmokeCheck(timer.Enabled && liveModel.Amount > 0 && overlay.Visible, "Dynamic movement wakes the scheduler and updates the overlay");
                    keyboard.TestKeys = new MovementKeys(); RequestUpdate(); break;
                case 17:
                    SmokeCheck(liveModel.Amount > 0 && liveModel.Amount < settings.Spread, "Release keeps smooth contraction after waking from idle");
                    Hide(); enabled = false; RequestUpdate(); break;
                case 18:
                    SmokeCheck(!timer.Enabled && liveCache.Count == 0 && demoCache.Count == 0 && preview.Frame == null, "Hidden disabled overlay releases frame caches and sleeps");
                    SmokeCheck(!keyboard.Registered, "Inactive background application releases Raw Input registration");
                    keyboard.TestKeys = null; Show(); RequestUpdate(); break;
                case 19:
                    SmokeCheck(keyboard.Registered && preview.Frame != null && overlayError == "", "Returning from the tray restores input and live preview");
                    CheckReusableSurface();
                    settings.Dynamic = true; keyboard.TestKeys = new MovementKeys(); Changed(); Save(); Tick(this, EventArgs.Empty);
                    break;
                case 20:
                    SmokeCheck(!timer.Enabled, "Visible dynamic preview sleeps after movement settles");
                    int transitions = 0; Action transition = delegate { transitions++; }; keyboard.StateChanged += transition;
                    keyboard.TestTransition(0x57, true); keyboard.TestTransition(0x57, true); keyboard.StateChanged -= transition;
                    SmokeCheck(transitions == 1 && updateQueued, "Raw keyboard packet wakes idle runtime once and ignores key repeat");
                    break;
                case 21:
                    SmokeCheck(liveModel.Amount > 0 && timer.Enabled, "Event-driven WASD input starts responsive dynamic animation");
                    keyboard.TestTransition(0x57, false); break;
                case 22:
                    SmokeCheck(liveModel.Amount > 0 && liveModel.Amount < settings.Spread, "Raw key release drives smooth recovery without polling for the transition");
                    var forward = new MovementKeys(true, false, false, false);
                    liveModel.Reset(); for (int i = 0; i < 180; i++) liveModel.Step(forward, 0.016, settings);
                    lastLiveKeys = forward; lastTick = clock.Elapsed.TotalSeconds - 0.14; keyboard.TestKeys = forward;
                    keyboard.TestTransition(0x44, true);
                    SmokeCheck(Math.Abs(liveModel.X) < 0.001 && liveModel.Y > 0.99, "Direction change does not apply new input retroactively during the slow idle interval");
                    keyboard.TestKeys = null;
                    savedDynamicSpread = settings.Spread; demo = heldPreview = true;
                    settings.Dynamic = false; Changed(); Save(); break;
                case 23:
                    SmokeCheck(!keyboard.Registered && !timer.Enabled && !demo && !heldPreview && demoCache.Count == 0 && liveModel.Amount == 0,
                        "Switching from active dynamic animation to static fully stops dynamic work");
                    SmokeCheck(settings.Spread == savedDynamicSpread && settings.Listen, "Static mode preserves saved dynamic parameters and input preference");
                    settings.Dynamic = true; Changed(); Save(); break;
                case 24:
                    SmokeCheck(keyboard.Registered && holdButton.Enabled && pages[1].Content.Enabled && settings.Spread == savedDynamicSpread,
                        "Switching back to dynamic restores its input and saved parameters");
                    if (!enabled) ToggleOverlay();
                    Tick(this, EventArgs.Empty); Close(); break;
                case 25:
                    SmokeCheck(!Visible && !IsDisposed && !ending && tray.Visible && overlay.Visible && keyboard.Registered,
                        "Close button hides settings while keeping the tray, overlay and dynamic input running");
                    ShowSettings();
                    SmokeCheck(Visible && WindowState == FormWindowState.Normal, "Hidden settings can be restored without restarting the application");
                    demo = heldPreview = false; settings.Dynamic = false; Changed(); Save(); Tick(this, EventArgs.Empty); Close(); break;
                case 26:
                    SmokeCheck(!Visible && overlay.Visible && !keyboard.Registered && !timer.Enabled,
                        "Close to tray preserves the static overlay and idle sleep");
                    ToolStripItem exitItem = null;
                    foreach (ToolStripItem item in tray.ContextMenuStrip.Items) if (item.Text == "退出") exitItem = item;
                    SmokeCheck(exitItem != null, "Tray context menu provides an explicit exit command");
                    var shutdown = new FormClosingEventArgs(CloseReason.WindowsShutDown, false);
                    OnFormClosing(shutdown);
                    SmokeCheck(!shutdown.Cancel && !overlay.IsDisposed && tray.Visible,
                        "System shutdown is allowed and native resources survive the closing notification until closed");
                    smokeTimer.Stop(); smokeTimer.Dispose(); exitItem.PerformClick();
                    SmokeCheck(IsDisposed && overlay.IsDisposed && ending && !tray.Visible && keyboard.Handle == IntPtr.Zero,
                        "Tray exit closes the application and releases overlay, input and notification icon");
                    Console.WriteLine("All " + smokeChecks + " UI/native smoke checks passed.");
                    Console.WriteLine("Offscreen UI renders: " + Artifacts);
                    break;
            }
        }
        private void CheckReusableSurface()
        {
            using (var window = new OverlayWindow())
            {
                var s = new Settings { Dynamic = true, Alpha = 128 };
                var output = new Rectangle(0, 0, 1920, 1080);
                using (var rest = CrosshairRenderer.Render(s, 0, output)) window.Present(rest);
                int allocations = window.SurfaceAllocations;
                bool pixelsMatch = true;
                for (int i = 0; i < 40; i++) using (var frame = CrosshairRenderer.Render(s, i % 6 / 2.0, output))
                { window.Present(frame); pixelsMatch &= window.TestSurfaceMatches(frame); }
                SmokeCheck(window.SurfaceAllocations == allocations && window.PresentCount == 41, "Repeated dynamic presents reuse one native DIB surface");
                SmokeCheck(pixelsMatch, "Reusable DIB retains premultiplied alpha and clears old dynamic pixels");
                window.Hide(); window.ReleaseSurface();
                using (var rest = CrosshairRenderer.Render(s, 0, output)) window.Present(rest);
                SmokeCheck(window.SurfaceAllocations == allocations + 1, "Released overlay surface is recreated correctly on demand");
            }
        }
    }
    internal static class UiSmokeTests
    {
        [STAThread] private static int Main()
        {
            try
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                var form = new MainForm(); form.Shown += delegate { form.StartSmoke(); };
                Application.Run(form); return form.SmokeExitCode;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
