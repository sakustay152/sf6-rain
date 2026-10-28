using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Sf6Rain
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "NimvedNoAme.Sf6Rain", out created))
            {
                if (!created)
                {
                    MessageBox.Show(
                        "「街霸六的雨」已经在运行了。\n\n看一下任务栏；如果窗口最小化了，点任务栏把它叫回来。",
                        "街霸六的雨", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                WinApi.SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                AppPaths.Ensure();
                Application.ThreadException += OnThreadException;
                AppDomain.CurrentDomain.UnhandledException += OnDomainException;

                try
                {
                    Application.Run(new MainForm());
                }
                catch (Exception ex)
                {
                    Crash(ex, "主界面");
                }
            }
        }

        static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Crash(e.Exception, "界面线程");
        }

        static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            Crash(e.ExceptionObject as Exception, "后台线程");
        }

        static void Crash(Exception ex, string where)
        {
            if (ex == null) return;
            string text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  [" + where + "]\r\n" +
                ex.ToString() + "\r\n\r\n";
            try
            {
                File.AppendAllText(AppPaths.CrashFile, text, new UTF8Encoding(false));
            }
            catch { }
            try
            {
                MessageBox.Show(
                    "程序遇到了问题，已经停止这次操作。\n\n" +
                    "详细信息写进了 崩溃.log，可以把它发给开发者。\n\n" +
                    ex.Message,
                    "街霸六的雨", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}
