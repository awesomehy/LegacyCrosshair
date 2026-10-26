using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace LegacyCrosshair
{
    internal sealed partial class MainForm
    {
        [DllImport("user32.dll")] private static extern int GetGuiResources(IntPtr process, int flags);
        private readonly Stopwatch benchmarkClock = new Stopwatch();
        private readonly List<object> measurements = new List<object>();
        private System.Windows.Forms.Timer benchmarkTimer;
        private Process benchmarkProcess;
        private int benchmarkPhase = -1, benchmarkPaints, startPaints, startGen0, lastInput = -1;
        private double measureCpu, measureTime;
        private long startManaged;
        private bool measuring;
        private string benchmarkLabel;
        private double sampleSeconds;
        private readonly Dictionary<string, int> controlPaints = new Dictionary<string, int>();
        private void HookBenchmarkPaints(Control parent)
        {
            parent.Paint += delegate { string type = parent.GetType().Name; int count; controlPaints.TryGetValue(type, out count); controlPaints[type] = count + 1; };
            foreach (Control child in parent.Controls) HookBenchmarkPaints(child);
        }
        private static readonly string[] PhaseNames = { "visible_static_idle", "visible_dynamic_demo", "hidden_static_overlay", "hidden_dynamic_overlay" };

        internal void StartBenchmark(string label, double seconds)
        {
            sampleSeconds = seconds;
            benchmarkLabel = label; benchmarkProcess = Process.GetCurrentProcess();
            HookBenchmarkPaints(this);
            preview.Paint += delegate { benchmarkPaints++; };
            benchmarkTimer = new System.Windows.Forms.Timer { Interval = 100 };
            benchmarkTimer.Tick += delegate {
                try { BenchmarkStep(); }
                catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; benchmarkTimer.Stop(); CloseBenchmark(); }
            };
            NextBenchmarkPhase(); benchmarkTimer.Start();
        }
        private void CloseBenchmark()
        {
            // Older source snapshots used Close() to exit; current versions keep it
            // running in the tray. Preserve support for measuring either snapshot.
            var exit = typeof(MainForm).GetMethod("RequestExit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (exit != null) exit.Invoke(this, null); else Close();
        }
        private void NextBenchmarkPhase()
        {
            benchmarkPhase++;
            if (benchmarkPhase == PhaseNames.Length)
            {
                benchmarkTimer.Stop();
                string path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "artifacts", "performance-" + benchmarkLabel + ".json"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, new JavaScriptSerializer().Serialize(new { label = benchmarkLabel, logicalProcessors = Environment.ProcessorCount, measurements = measurements }));
                Console.WriteLine("Saved: " + path); benchmarkProcess.Dispose(); benchmarkTimer.Dispose(); CloseBenchmark(); return;
            }
            settings = new Settings(); settings.Dynamic = benchmarkPhase == 1 || benchmarkPhase == 3;
            settings.FollowCs2 = settings.OnlyCs2 = false;
            keyboard.TestKeys = new MovementKeys(); liveModel.Reset(); demoModel.Reset();
            demo = benchmarkPhase == 1; heldPreview = false; enabled = benchmarkPhase >= 2;
            if (!enabled) overlay.Hide();
            if (benchmarkPhase < 2) { Show(); WindowState = FormWindowState.Normal; } else Hide();
            RefreshUi(); Changed(); dirty = false; lastInput = -1;
            Tick(this, EventArgs.Empty); benchmarkClock.Restart(); measuring = false;
            Console.WriteLine("Measuring " + PhaseNames[benchmarkPhase]);
        }
        private void BenchmarkStep()
        {
            double seconds = benchmarkClock.Elapsed.TotalSeconds;
            if (benchmarkPhase == 3)
            {
                int input = (int)(seconds * 2) % 4;
                if (input != lastInput)
                {
                    lastInput = input;
                    keyboard.TestKeys = new MovementKeys(input == 0 || input == 1, input == 2, false, false);
                    Tick(this, EventArgs.Empty);
                }
            }
            if (!measuring && seconds >= 1.5)
            {
                measuring = true; benchmarkProcess.Refresh(); measureCpu = benchmarkProcess.TotalProcessorTime.TotalMilliseconds;
                measureTime = seconds; startPaints = benchmarkPaints; startGen0 = GC.CollectionCount(0); startManaged = GC.GetTotalMemory(false);
                controlPaints.Clear();
            }
            if (!measuring || seconds - measureTime < sampleSeconds) return;
            benchmarkProcess.Refresh(); double elapsed = seconds - measureTime;
            var result = new {
                scenario = PhaseNames[benchmarkPhase], seconds = Math.Round(elapsed, 3),
                cpuMs = Math.Round(benchmarkProcess.TotalProcessorTime.TotalMilliseconds - measureCpu, 3),
                cpuOneCorePercent = Math.Round((benchmarkProcess.TotalProcessorTime.TotalMilliseconds - measureCpu) / (elapsed * 10), 3),
                workingSetMB = Math.Round(benchmarkProcess.WorkingSet64 / 1048576.0, 3),
                privateMB = Math.Round(benchmarkProcess.PrivateMemorySize64 / 1048576.0, 3),
                managedMB = Math.Round(GC.GetTotalMemory(false) / 1048576.0, 3),
                managedGrowthKB = Math.Round((GC.GetTotalMemory(false) - startManaged) / 1024.0, 1),
                gen0Collections = GC.CollectionCount(0) - startGen0, previewPaints = benchmarkPaints - startPaints,
                handles = benchmarkProcess.HandleCount, gdiObjects = GetGuiResources(benchmarkProcess.Handle, 0), controlPaints = new Dictionary<string, int>(controlPaints), runtimeTimer = timer.Enabled, runtimeInterval = timer.Interval,
                threads = ThreadTimes()
            };
            measurements.Add(result); Console.WriteLine(new JavaScriptSerializer().Serialize(result)); NextBenchmarkPhase();
        }
        private object ThreadTimes()
        {
            var threads = new List<object>();
            foreach (ProcessThread thread in benchmarkProcess.Threads)
                threads.Add(new { id = thread.Id, cpuMs = thread.TotalProcessorTime.TotalMilliseconds });
            return threads;
        }
    }
    internal static class PerformanceTests
    {
        [STAThread] private static void Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using (var form = new MainForm())
            { form.Shown += delegate { form.BeginInvoke(new Action(delegate { form.StartBenchmark(args.Length > 0 ? args[0] : "current", args.Length > 1 ? Double.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 10); })); }; Application.Run(form); }
        }
    }
}
