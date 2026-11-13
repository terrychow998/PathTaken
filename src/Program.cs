using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace PathTaken
{
    // 入口：同一个 exe 既当界面，也当后台记录器（--tracker）
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        static Mutex UiMutex;   // 持有到进程结束，防止被 GC 回收后释放互斥体

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "--tracker", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 1; i < args.Length; i++)
                {
                    string a = args[i];
                    if (a.StartsWith("--idle="))
                    {
                        int v = 0;
                        int.TryParse(a.Substring(7), out v);
                        if (v > 0) Tracker.IdleLimit = v;
                    }
                }
                Tracker.Run();
                return;
            }

            try { SetProcessDPIAware(); }
            catch { }
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    Theme.S = g.DpiX / 96f;
                    if (Theme.S < 1f) Theme.S = 1f;
                }
            }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e)
            {
                try { MessageBox.Show("程序遇到一个问题：\n" + e.Exception.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                catch { }
            };
            AppDomain.CurrentDomain.UnhandledException += delegate { };

            // 单实例：抢一个命名互斥体，抢不到说明已经有一个窗口开着
            // 必须存成静态字段：否则本地变量出作用域后被 GC 回收，互斥体会被释放，单实例就失效了
            UiMutex = new Mutex(false, "PathTaken_SingleInstance_2F3A");
            bool owned;
            try { owned = UiMutex.WaitOne(0, true); }
            catch (AbandonedMutexException) { owned = true; }
            catch { owned = true; }
            if (!owned)
            {
                ActivateExisting();
                return;
            }

            Store.Ensure();
            Tracker.EnsureRunning();

            // 调试：只清本软件记录（不清 Windows 启动记录），用于自测
            if (args.Length >= 2 && string.Equals(args[0], "--cleartest", StringComparison.OrdinalIgnoreCase))
            {
                string res = ClearDialog.DoClear(true, false, false);
                System.IO.File.WriteAllText(args[1], res, System.Text.Encoding.UTF8);
                return;
            }

            // 调试：只截“一键清除”对话框，用于无 GUI 验收
            if (args.Length >= 2 && string.Equals(args[0], "--shotdlg", StringComparison.OrdinalIgnoreCase))
            {
                string dlgPath = args[1];
                string errPath = dlgPath + ".err.txt";
                try
                {
                    ClearDialog d = new ClearDialog();
                    d.StartPosition = FormStartPosition.Manual;
                    d.Location = new Point(240, 240);
                    d.Show();
                    for (int i = 0; i < 6; i++)
                    {
                        Application.DoEvents();
                        d.Refresh();
                        System.Threading.Thread.Sleep(120);
                    }
                    using (Bitmap b = new Bitmap(d.Width, d.Height))
                    {
                        d.DrawToBitmap(b, new Rectangle(0, 0, d.Width, d.Height));
                        b.Save(dlgPath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    d.Close();
                }
                catch (Exception ex)
                {
                    System.IO.File.WriteAllText(errPath, ex.ToString(), System.Text.Encoding.UTF8);
                }
                return;
            }

            string shot = null;
            int shotTab = 0;
            if (args.Length >= 2 && string.Equals(args[0], "--shot", StringComparison.OrdinalIgnoreCase))
            {
                shot = args[1];
                if (args.Length >= 3) int.TryParse(args[2], out shotTab);
            }

            MainForm form = new MainForm();
            form.ShotPath = shot;
            form.ShotTab = shotTab;
            Application.Run(form);
        }

        // 已经有一个窗口在开着时：把它拉到前台，而不是再开一个
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int cmd);

        static void ActivateExisting()
        {
            try
            {
                IntPtr h = FindWindow(null, "Path Taken");
                if (h == IntPtr.Zero) h = FindWindow(null, "AppTrace");
                if (h == IntPtr.Zero) return;
                ShowWindow(h, 9);           // SW_RESTORE
                SetForegroundWindow(h);
            }
            catch { }
        }
    }
}
