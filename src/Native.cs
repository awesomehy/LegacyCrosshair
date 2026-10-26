using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LegacyCrosshair
{
    internal static class Native
    {
        internal const int WM_INPUT = 0x00FF, WM_HOTKEY = 0x0312, WM_MOUSEACTIVATE = 0x0021;
        [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; public Point(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] internal struct Size { public int Width, Height; public Size(int w, int h) { Width = w; Height = h; } }
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] internal struct Blend { public byte Operation, Flags, ConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)] internal struct RawDevice { public ushort UsagePage, Usage; public uint Flags; public IntPtr Target; }
        [StructLayout(LayoutKind.Sequential)] internal struct RawHeader { public uint Type, Size; public IntPtr Device, WParam; }
        [StructLayout(LayoutKind.Sequential)] internal struct RawKeyboard { public ushort MakeCode, Flags, Reserved, VKey; public uint Message, ExtraInformation; }
        [StructLayout(LayoutKind.Sequential)] internal struct BitmapInfo
        { public uint HeaderSize; public int Width, Height; public ushort Planes, BitCount; public uint Compression, ImageSize; public int XPels, YPels; public uint ColorsUsed, ColorsImportant; }
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterRawInputDevices(RawDevice[] devices, uint count, uint size);
        [DllImport("user32.dll", SetLastError = true)] internal static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] internal static extern bool ClientToScreen(IntPtr window, ref Point point);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll", SetLastError = true)] internal static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")] internal static extern void CopyMemory(IntPtr destination, IntPtr source, UIntPtr bytes);
        internal delegate void WinEventCallback(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint thread, uint time);
        [DllImport("user32.dll")] internal static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module, WinEventCallback callback, uint process, uint thread, uint flags);
        [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool UpdateLayeredWindow(IntPtr window, IntPtr destinationDc, ref Point destination,
            ref Size size, IntPtr sourceDc, ref Point source, uint colorKey, ref Blend blend, uint flags);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        internal static void ThemeWindow(IntPtr window, bool dark, Color caption, Color text)
        {
            // These attributes are documented for Windows 11; older systems keep their native frame.
            if (Environment.OSVersion.Version.Build < 22000) return;
            int mode = dark ? 1 : 0, background = caption.R | caption.G << 8 | caption.B << 16, foreground = text.R | text.G << 8 | text.B << 16;
            DwmSetWindowAttribute(window, 20, ref mode, 4); DwmSetWindowAttribute(window, 35, ref background, 4); DwmSetWindowAttribute(window, 36, ref foreground, 4);
        }

        private static IntPtr foregroundWindow;
        private static uint foregroundPid;
        private static bool foregroundIsCs2;
        internal static bool IsCs2Foreground()
        {
            IntPtr window = GetForegroundWindow(); uint pid; GetWindowThreadProcessId(window, out pid);
            if (window == foregroundWindow && pid == foregroundPid) return foregroundIsCs2;
            foregroundWindow = window; foregroundPid = pid; foregroundIsCs2 = false;
            try { using (var process = Process.GetProcessById((int)pid)) foregroundIsCs2 = String.Equals(process.ProcessName, "cs2", StringComparison.OrdinalIgnoreCase); }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (Win32Exception) { return false; }
            return foregroundIsCs2;
        }
        internal static IntPtr FindCs2Window()
        {
            Process[] processes = Process.GetProcessesByName("cs2");
            try
            {
                foreach (var process in processes)
                {
                    try
                    {
                        IntPtr handle = process.MainWindowHandle;
                        if (handle != IntPtr.Zero) return handle;
                    }
                    catch (InvalidOperationException) { }
                    catch (Win32Exception) { }
                }
            }
            finally { foreach (var process in processes) process.Dispose(); }
            return IntPtr.Zero;
        }
    }

    internal sealed class ForegroundWatcher : IDisposable
    {
        private readonly Native.WinEventCallback callback;
        private IntPtr hook;
        private bool requested;
        internal bool Registered { get { return hook != IntPtr.Zero; } }
        internal event Action Changed;
        internal ForegroundWatcher()
        { callback = delegate { if (Changed != null) Changed(); }; }
        internal void SetEnabled(bool enabled)
        {
            if (requested == enabled) return; requested = enabled;
            if (hook != IntPtr.Zero) { Native.UnhookWinEvent(hook); hook = IntPtr.Zero; }
            // Out-of-context notifications run on this UI thread; no game injection or polling loop.
            if (enabled) hook = Native.SetWinEventHook(3, 3, IntPtr.Zero, callback, 0, 0, 0);
        }
        public void Dispose() { SetEnabled(false); }
    }

    internal sealed class Cs2WindowTracker
    {
        private IntPtr window;
        private uint pid;
        private double nextSearch;
        internal int SearchCount { get; private set; }
        internal bool Read(double now, out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            uint currentPid;
            if (window != IntPtr.Zero && (!Native.IsWindow(window) || Native.GetWindowThreadProcessId(window, out currentPid) == 0 || currentPid != pid)) window = IntPtr.Zero;
            if (window == IntPtr.Zero && now >= nextSearch)
            {
                SearchCount++; nextSearch = now + 2;
                window = Native.FindCs2Window(); Native.GetWindowThreadProcessId(window, out pid);
            }
            if (window == IntPtr.Zero || Native.IsIconic(window) || !Native.IsWindowVisible(window)) return false;
            Native.Rect rect; var origin = new Native.Point();
            if (!Native.GetClientRect(window, out rect) || !Native.ClientToScreen(window, ref origin) || rect.Right < 32 || rect.Bottom < 32) return false;
            bounds = new Rectangle(origin.X, origin.Y, rect.Right, rect.Bottom); return true;
        }
    }

    internal sealed class RawKeyboardInput : NativeWindow, IDisposable
    {
        private readonly bool[] down = new bool[256];
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private double lastSync = -1;
        private IntPtr buffer;
        private readonly uint headerSize = (uint)Marshal.SizeOf(typeof(Native.RawHeader));
        private bool listening;
        public bool Registered { get; private set; }
        public string Error { get; private set; }
        public long EventCount { get; private set; }
        public event Action<int> Hotkey;
        public event Action StateChanged;
#if UI_TEST
        internal MovementKeys? TestKeys = null;
        internal int ReadCount;
        internal void TestTransition(int key, bool pressed)
        {
            if (!listening) return;
            Marshal.WriteInt32(buffer, 1);
            Marshal.WriteInt16(buffer, (int)headerSize + 2, (short)(pressed ? 0 : 1));
            Marshal.WriteInt16(buffer, (int)headerSize + 6, (short)key);
            MovementKeys current = TestKeys ?? new MovementKeys();
            if (key == 0x57) current.W = pressed; if (key == 0x41) current.A = pressed;
            if (key == 0x53) current.S = pressed; if (key == 0x44) current.D = pressed;
            TestKeys = current; ProcessPacket(headerSize + 16);
        }
#endif
        public RawKeyboardInput(bool enabled)
        {
            CreateHandle(new CreateParams { Caption = "LegacyCrosshair.Input", Parent = new IntPtr(-3) });
            SetListening(enabled);
        }
        public void SetListening(bool enabled)
        {
            if (listening == enabled) return;
            listening = enabled; Clear(); lastSync = -1;
            if (!enabled)
            {
                if (Registered) Native.RegisterRawInputDevices(new[] { new Native.RawDevice { UsagePage = 1, Usage = 6, Flags = 1 } }, 1, (uint)Marshal.SizeOf(typeof(Native.RawDevice)));
                if (buffer != IntPtr.Zero) { Marshal.FreeHGlobal(buffer); buffer = IntPtr.Zero; }
                Registered = false; return;
            }
            buffer = Marshal.AllocHGlobal(4096);
            var device = new Native.RawDevice { UsagePage = 1, Usage = 6, Flags = 0x100, Target = Handle };
            Registered = Native.RegisterRawInputDevices(new[] { device }, 1, (uint)Marshal.SizeOf(typeof(Native.RawDevice)));
            if (!Registered) Error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
        }
        public void Clear() { Array.Clear(down, 0, down.Length); }
        public MovementKeys Read(bool enabled)
        {
#if UI_TEST
            ReadCount++;
            if (TestKeys.HasValue) return enabled ? TestKeys.Value : new MovementKeys();
#endif
            if (!enabled) { Clear(); return new MovementKeys(); }
            // Raw Input drives transitions. A periodic snapshot repairs missed releases.
            if (!Registered || lastSync < 0 || clock.Elapsed.TotalSeconds - lastSync > 0.15)
            {
                ReadKey(0x57); ReadKey(0x41); ReadKey(0x53); ReadKey(0x44); lastSync = clock.Elapsed.TotalSeconds;
            }
            return new MovementKeys(down[0x57], down[0x41], down[0x53], down[0x44]);
        }
        private bool ReadKey(int key)
        {
            // Reconcile missed releases after lock/unlock, desktop changes or device removal.
            down[key] = (Native.GetAsyncKeyState(key) & 0x8000) != 0;
            return down[key];
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_INPUT && listening)
            {
                uint size = 4096;
                uint read = Native.GetRawInputData(m.LParam, 0x10000003, buffer, ref size, headerSize);
                ProcessPacket(read);
            }
            else if (m.Msg == Native.WM_HOTKEY && Hotkey != null) Hotkey(m.WParam.ToInt32());
            base.WndProc(ref m);
        }
        private void ProcessPacket(uint read)
        {
            if (!listening || read == UInt32.MaxValue || read < headerSize + 16 || read > 4096 || Marshal.ReadInt32(buffer) != 1) return;
            int key = (ushort)Marshal.ReadInt16(buffer, (int)headerSize + 6);
            if (key == 0x57 || key == 0x41 || key == 0x53 || key == 0x44)
            {
                bool pressed = (Marshal.ReadInt16(buffer, (int)headerSize + 2) & 1) == 0; EventCount++;
                if (down[key] != pressed) { down[key] = pressed; if (StateChanged != null) StateChanged(); }
            }
        }
        public void Dispose()
        {
            Native.UnregisterHotKey(Handle, 1); Native.UnregisterHotKey(Handle, 2);
            SetListening(false);
            DestroyHandle();
        }
    }

    internal sealed class OverlayWindow : Form
    {
        private IntPtr memoryDc, bitmap, previous, bits;
        private int capacityWidth, capacityHeight;
        internal int SurfaceAllocations { get; private set; }
        internal int PresentCount { get; private set; }
#if UI_TEST
        internal bool TestSurfaceMatches(CrosshairFrame frame)
        {
            BitmapData data = frame.Bitmap.LockBits(new Rectangle(Point.Empty, frame.Bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                for (int y = 0; y < frame.Bitmap.Height; y++) for (int x = 0; x < frame.Bitmap.Width; x++)
                    if (Marshal.ReadInt32(bits, (y * capacityWidth + x) * 4) != Marshal.ReadInt32(data.Scan0, y * data.Stride + x * 4)) return false;
                return true;
            }
            finally { frame.Bitmap.UnlockBits(data); }
        }
#endif
        public OverlayWindow()
        {
            Text = "LegacyCrosshair.Overlay";
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.None;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x00080000 | 0x00000020 | 0x08000000 | 0x00000080 | 0x00000008;
                return cp;
            }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_MOUSEACTIVATE) { m.Result = new IntPtr(3); return; }
            if (m.Msg == 0x0084) { m.Result = new IntPtr(-1); return; }
            base.WndProc(ref m);
        }
        public void Present(CrosshairFrame frame)
        {
            EnsureSurface(frame.Bitmap.Width, frame.Bitmap.Height);
            BitmapData data = frame.Bitmap.LockBits(new Rectangle(Point.Empty, frame.Bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                for (int y = 0; y < frame.Bitmap.Height; y++)
                    Native.CopyMemory(IntPtr.Add(bits, y * capacityWidth * 4), IntPtr.Add(data.Scan0, y * data.Stride), new UIntPtr((uint)(frame.Bitmap.Width * 4)));
            }
            finally { frame.Bitmap.UnlockBits(data); }
            var location = new Native.Point(frame.Origin.X, frame.Origin.Y);
            var size = new Native.Size(frame.Bitmap.Width, frame.Bitmap.Height);
            var source = new Native.Point(0, 0);
            var blend = new Native.Blend { ConstantAlpha = 255, AlphaFormat = 1 };
            if (!Native.UpdateLayeredWindow(Handle, IntPtr.Zero, ref location, ref size, memoryDc, ref source, 0, ref blend, 2))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            PresentCount++;
            if (!Visible) Show();
            Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        }
        private static int Capacity(int size) { int result = 64; while (result < size) result *= 2; return result; }
        private void EnsureSurface(int width, int height)
        {
            if (memoryDc != IntPtr.Zero && width <= capacityWidth && height <= capacityHeight &&
                ((long)capacityWidth * capacityHeight <= 65536 || (long)width * height * 8 >= (long)capacityWidth * capacityHeight)) return;
            ReleaseSurface(); capacityWidth = Capacity(width); capacityHeight = Capacity(height);
            if ((long)capacityWidth * capacityHeight > 16000000) { capacityWidth = width; capacityHeight = height; }
            memoryDc = Native.CreateCompatibleDC(IntPtr.Zero);
            var info = new Native.BitmapInfo { HeaderSize = 40, Width = capacityWidth, Height = -capacityHeight, Planes = 1, BitCount = 32 };
            bitmap = Native.CreateDIBSection(memoryDc, ref info, 0, out bits, IntPtr.Zero, 0);
            if (memoryDc == IntPtr.Zero || bitmap == IntPtr.Zero) { ReleaseSurface(); throw new Win32Exception("无法创建覆盖层画布。"); }
            previous = Native.SelectObject(memoryDc, bitmap); SurfaceAllocations++;
        }
        internal void ReleaseSurface()
        {
            if (previous != IntPtr.Zero) Native.SelectObject(memoryDc, previous);
            if (bitmap != IntPtr.Zero) Native.DeleteObject(bitmap);
            if (memoryDc != IntPtr.Zero) Native.DeleteDC(memoryDc);
            bitmap = memoryDc = previous = bits = IntPtr.Zero; capacityWidth = capacityHeight = 0;
        }
        protected override void Dispose(bool disposing) { if (disposing) ReleaseSurface(); base.Dispose(disposing); }
    }
}
