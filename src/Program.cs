using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
namespace LegacyCrosshair
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            bool created;
            using (var mutex = new Mutex(true, "Local\\LegacyCrosshair.v1", out created))
            {
                if (!created) { MessageBox.Show("程序已经运行。请双击托盘图标，或按 Ctrl+Alt+S 打开设置。", "Legacy Crosshair"); return; }
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { Report(e.Exception); };
                try { Application.Run(new MainForm()); } catch (Exception ex) { Report(ex); }
                mutex.ReleaseMutex();
            }
        }
        private static void Report(Exception ex)
        {
            try { Directory.CreateDirectory(ConfigStore.Folder); File.AppendAllText(Path.Combine(ConfigStore.Folder, "error.log"), DateTime.Now.ToString("s") + "\r\n" + ex + "\r\n"); } catch { }
            MessageBox.Show("程序遇到错误：" + ex.Message + "\n诊断日志：" + Path.Combine(ConfigStore.Folder, "error.log"), "Legacy Crosshair", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
