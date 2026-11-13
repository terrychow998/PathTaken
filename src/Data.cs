using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace PathTaken
{
    // ============ 数据模型 ============
    public class Session
    {
        public string App = "";
        public string Title = "";
        public string Path = "";
        public DateTime Start;
        public DateTime End;
        public bool Live = false;

        public TimeSpan Duration
        {
            get { return End > Start ? End - Start : TimeSpan.Zero; }
        }
    }

    public class Launch
    {
        public string Name = "";
        public string Path = "";
        public string Kind = "";
        public string Source = "";
        public DateTime LastRun;
        public int Runs;
    }

    // ============ 格式化 ============
    public static class Fmt
    {
        public static string Dur(TimeSpan t)
        {
            long sec = (long)t.TotalSeconds;
            if (sec < 1) return "0 秒";
            if (sec < 60) return sec + " 秒";
            long m = sec / 60, h = m / 60;
            if (h > 0) return h + " 小时 " + (m % 60) + " 分";
            return m + " 分 " + (sec % 60) + " 秒";
        }

        public static string DurShort(TimeSpan t)
        {
            long sec = (long)t.TotalSeconds;
            if (sec < 60) return sec + "秒";
            long m = sec / 60, h = m / 60;
            if (h > 0) return h + "小时" + (m % 60) + "分";
            return m + "分";
        }

        public static string Clock(DateTime d) { return d.ToString("HH:mm:ss"); }

        public static string Stamp(DateTime d) { return d.ToString("yyyy-MM-dd HH:mm"); }

        public static string DayName(DateTime d)
        {
            string[] n = new string[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            if (d.Date == DateTime.Today) return "今天 " + n[(int)d.DayOfWeek];
            if (d.Date == DateTime.Today.AddDays(-1)) return "昨天";
            return d.ToString("MM-dd") + " " + n[(int)d.DayOfWeek];
        }

        public static string Ago(DateTime d)
        {
            TimeSpan t = DateTime.Now - d;
            if (t.TotalSeconds < 60) return "刚刚";
            if (t.TotalMinutes < 60) return (int)t.TotalMinutes + " 分钟前";
            if (t.TotalHours < 24) return (int)t.TotalHours + " 小时前";
            if (t.TotalDays < 30) return (int)t.TotalDays + " 天前";
            return Stamp(d);
        }
    }

    // ============ 本地存储 ============
    public static class Store
    {
        public static readonly string Root =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PathTaken");
        public static string SessionsFile { get { return Path.Combine(Root, "sessions.log"); } }
        public static string CurrentFile { get { return Path.Combine(Root, "current.log"); } }
        public static string HeartbeatFile { get { return Path.Combine(Root, "heartbeat.log"); } }
        public static string PidFile { get { return Path.Combine(Root, "tracker.pid"); } }
        public static string BackupDir { get { return Path.Combine(Root, "backup"); } }

        static readonly object Locker = new object();

        public static void Ensure()
        {
            try
            {
                // 从旧版数据目录（AppTrace）迁移一次，升级后历史记录不丢
                string legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppTrace");
                if (!Directory.Exists(Root) && Directory.Exists(legacy))
                {
                    try { Directory.Move(legacy, Root); } catch { }
                }
                if (!Directory.Exists(Root)) Directory.CreateDirectory(Root);
            }
            catch { }
        }

        static string Clean(string s)
        {
            if (s == null) return "";
            return s.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');
        }

        public static DateTime ParseTime(string s)
        {
            DateTime d;
            if (DateTime.TryParseExact(s, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out d)) return d;
            if (DateTime.TryParse(s, out d)) return d;
            return DateTime.MinValue;
        }

        public static List<Session> Load()
        {
            List<Session> list = new List<Session>();
            try
            {
                if (!File.Exists(SessionsFile)) return list;
                string[] lines = File.ReadAllLines(SessionsFile, Encoding.UTF8);
                foreach (string line in lines)
                {
                    if (line.Length == 0) continue;
                    string[] p = line.Split('\t');
                    if (p.Length < 4) continue;
                    DateTime st = ParseTime(p[0]);
                    DateTime en = ParseTime(p[1]);
                    if (st == DateTime.MinValue || en == DateTime.MinValue) continue;
                    if (en < st) continue;
                    if ((en - st).TotalHours > 26) continue;
                    Session s = new Session();
                    s.Start = st;
                    s.End = en;
                    s.App = p[2].Trim();
                    s.Title = p[3];
                    if (p.Length > 4) s.Path = p[4];
                    if (s.App.Length == 0) continue;
                    list.Add(s);
                }
            }
            catch { }
            list.Sort(delegate (Session a, Session b) { return a.Start.CompareTo(b.Start); });
            return list;
        }

        public static void Append(Session s)
        {
            if (s == null) return;
            try
            {
                Ensure();
                string line = string.Join("\t", new string[]
                {
                    s.Start.ToString("yyyy-MM-ddTHH:mm:ss"),
                    s.End.ToString("yyyy-MM-ddTHH:mm:ss"),
                    Clean(s.App), Clean(s.Title), Clean(s.Path)
                });
                lock (Locker) File.AppendAllText(SessionsFile, line + "\r\n", new UTF8Encoding(false));
            }
            catch { }
        }

        public static void WriteCurrent(Session s)
        {
            try
            {
                Ensure();
                if (s == null)
                {
                    if (File.Exists(CurrentFile)) File.Delete(CurrentFile);
                    return;
                }
                string line = string.Join("\t", new string[]
                {
                    s.Start.ToString("yyyy-MM-ddTHH:mm:ss"),
                    Clean(s.App), Clean(s.Title), Clean(s.Path)
                });
                lock (Locker) File.WriteAllText(CurrentFile, line, new UTF8Encoding(false));
            }
            catch { }
        }

        public static Session ReadCurrent(out DateTime heartbeat)
        {
            heartbeat = DateTime.MinValue;
            try
            {
                if (!File.Exists(CurrentFile)) return null;
                heartbeat = File.GetLastWriteTime(CurrentFile);
                string line = File.ReadAllText(CurrentFile, Encoding.UTF8).Trim();
                if (line.Length == 0) return null;
                string[] p = line.Split('\t');
                if (p.Length < 2) return null;
                Session s = new Session();
                s.Start = ParseTime(p[0]);
                if (s.Start == DateTime.MinValue) return null;
                if (heartbeat > s.Start) s.End = heartbeat; else s.End = s.Start;
                s.App = p[1];
                if (p.Length > 2) s.Title = p[2];
                if (p.Length > 3) s.Path = p[3];
                if (s.App.Trim().Length == 0) return null;
                return s;
            }
            catch { return null; }
        }

        public static void Heartbeat()
        {
            try
            {
                Ensure();
                lock (Locker) File.WriteAllText(HeartbeatFile, DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), new UTF8Encoding(false));
            }
            catch { }
        }

        public static bool TrackerAlive()
        {
            try
            {
                return File.Exists(HeartbeatFile) &&
                       (DateTime.Now - File.GetLastWriteTime(HeartbeatFile)).TotalSeconds < 20;
            }
            catch { return false; }
        }

        public static DateTime TrackerBeat()
        {
            try { if (File.Exists(HeartbeatFile)) return File.GetLastWriteTime(HeartbeatFile); }
            catch { }
            return DateTime.MinValue;
        }

        public static void ClearOwn()
        {
            try
            {
                if (File.Exists(SessionsFile)) File.Delete(SessionsFile);
                if (File.Exists(CurrentFile)) File.Delete(CurrentFile);
            }
            catch { }
        }

        public static void ClearHeartbeat()
        {
            try { if (File.Exists(HeartbeatFile)) File.Delete(HeartbeatFile); }
            catch { }
        }
    }

    // ============ 前台使用追踪器 ============
    public static class Tracker
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);
        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

        public const int IdleLimitSeconds = 120;   // 无操作 2 分钟视为离开（可在命令行 --idle=<秒> 覆盖）
        public static int IdleLimit = IdleLimitSeconds;
        public const int TickMs = 2000;            // 采样间隔
        const int MinSessionSeconds = 3;           // 太短的切换不记录

        static readonly Dictionary<string, string> NiceNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"chrome","Google Chrome"}, {"msedge","Microsoft Edge"}, {"firefox","Firefox"},
            {"iexplore","Internet Explorer"}, {"explorer","文件资源管理器"},
            {"Code","VS Code"}, {"devenv","Visual Studio"}, {"notepad","记事本"},
            {"notepad++","Notepad++"}, {"WINWORD","Word"}, {"EXCEL","Excel"},
            {"POWERPNT","PowerPoint"}, {"wps","WPS 文字"}, {"et","WPS 表格"}, {"wpp","WPS 演示"},
            {"Doubao","豆包"}, {"WeChat","微信"}, {"Weixin","微信"}, {"QQ","QQ"}, {"TIM","TIM"},
            {"DingTalk","钉钉"}, {"Feishu","飞书"}, {"Lark","飞书"}, {"Telegram","Telegram"},
            {"Discord","Discord"}, {"steam","Steam"}, {"steamwebhelper","Steam"},
            {"Taskmgr","任务管理器"}, {"mspaint","画图"}, {"calc","计算器"},
            {"cmd","命令提示符"}, {"powershell","PowerShell"}, {"WindowsTerminal","Windows Terminal"},
            {"wt","Windows Terminal"}, {"7zFM","7-Zip"}, {"WinRAR","WinRAR"}, {"vlc","VLC"},
            {"PotPlayerMini64","PotPlayer"}, {"photoshop","Photoshop"}, {"Acrobat","Acrobat"},
            {"SumatraPDF","SumatraPDF"}, {"Obsidian","Obsidian"}, {"Typora","Typora"},
            {"wpspdf","WPS PDF"}, {"YoudaoDict","网易有道词典"}, {"BaiduNetdisk","百度网盘"},
            {"aliyundrive","阿里云盘"}, {"qdesktop","腾讯会议"}, {"wemeetapp","腾讯会议"},
            {"NVIDIA Overlay","NVIDIA Overlay"}
        };

        static readonly string[] Noise = new string[]
        {
            "ShellExperienceHost", "StartMenuExperienceHost", "SearchUI", "SearchApp",
            "SearchHost", "LockApp", "TextInputHost", "ApplicationFrameHost", "Widgets",
            "PickerHost", "SystemSettings", "SecHealthUI", "WWAHost", "backgroundTaskHost",
            "dwm", "csrss", "winlogon", "logonui"
        };

        public static int Run()
        {
            bool created;
            using (Mutex m = new Mutex(true, "Local\\PathTakenTracker", out created))
            {
                if (!created) return 0;
                Store.Ensure();
                try { File.WriteAllText(Store.PidFile, Process.GetCurrentProcess().Id.ToString(), new UTF8Encoding(false)); }
                catch { }
                Recover();
                TrackerLoop();
            }
            return 0;
        }

        // 停掉后台记录进程（只杀我们自己的 exe，避免误杀别的进程）
        public static void Stop()
        {
            try
            {
                if (!File.Exists(Store.PidFile)) return;
                int pid = int.Parse(File.ReadAllText(Store.PidFile).Trim());
                if (pid <= 0 || pid == Process.GetCurrentProcess().Id) return;
                Process p = Process.GetProcessById(pid);
                string path = "";
                try { path = p.MainModule.FileName; } catch { }
                if (path.Length == 0 || string.Equals(path, AppInfo.ExePath, StringComparison.OrdinalIgnoreCase))
                    p.Kill();
                try { File.Delete(Store.PidFile); } catch { }
            }
            catch { }
        }

        static long IdleSeconds()
        {
            LASTINPUTINFO li = new LASTINPUTINFO();
            li.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
            if (!GetLastInputInfo(ref li)) return 0;
            uint now = (uint)Environment.TickCount;
            uint diff = now - li.dwTime;
            return diff / 1000;
        }

        static string TitleOf(IntPtr hwnd)
        {
            try
            {
                StringBuilder sb = new StringBuilder(512);
                GetWindowText(hwnd, sb, 512);
                return sb.ToString();
            }
            catch { return ""; }
        }

        static bool IsNoise(string proc)
        {
            foreach (string n in Noise)
                if (string.Equals(n, proc, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static void Recover()
        {
            DateTime hb;
            Session s = Store.ReadCurrent(out hb);
            if (s == null) { Store.WriteCurrent(null); return; }
            s.End = hb;
            if (s.End > s.Start && (s.End - s.Start).TotalSeconds >= MinSessionSeconds)
            {
                if ((s.End - s.Start).TotalHours > 12) s.End = s.Start.AddHours(12);
                Store.Append(s);
            }
            Store.WriteCurrent(null);
        }

        static void Close(Session s)
        {
            if (s == null) return;
            if ((s.End - s.Start).TotalSeconds < MinSessionSeconds) return;
            if ((s.End - s.Start).TotalHours > 12) s.End = s.Start.AddHours(12);
            Store.Append(s);
        }

        static void TrackerLoop()
        {
            Session cur = null;
            string self = Process.GetCurrentProcess().ProcessName;

            while (true)
            {
                try
                {
                    Thread.Sleep(TickMs);
                    Store.Heartbeat();

                    IntPtr hwnd = GetForegroundWindow();
                    uint pid = 0;
                    if (hwnd != IntPtr.Zero) GetWindowThreadProcessId(hwnd, out pid);

                    string proc = "", title = "", path = "";
                    if (pid != 0)
                    {
                        try
                        {
                            using (Process p = Process.GetProcessById((int)pid))
                            {
                                proc = p.ProcessName;
                                try { title = p.MainWindowTitle; } catch { }
                                try { path = p.MainModule.FileName; } catch { }
                            }
                        }
                        catch { }
                    }
                    if (title.Length == 0 && hwnd != IntPtr.Zero) title = TitleOf(hwnd);

                    string display = "";
                    if (proc.Length > 0)
                    {
                        string nice;
                        display = NiceNames.TryGetValue(proc, out nice) ? nice : proc;
                    }
                    if (proc.Length == 0) display = "";

                    DateTime now = DateTime.Now;
                    long idle = IdleSeconds();

                    if (idle >= IdleLimit)
                    {
                        if (cur != null)
                        {
                            cur.End = now.AddSeconds(-Math.Min(idle, 6 * 3600));
                            if (cur.End < cur.Start) cur.End = cur.Start;
                            Close(cur);
                            cur = null;
                            Store.WriteCurrent(null);
                        }
                        continue;
                    }

                    bool skip = display.Length == 0 || IsNoise(proc) ||
                                string.Equals(proc, self, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(proc, "应用足迹", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(proc, "PathTaken", StringComparison.OrdinalIgnoreCase);

                    if (skip)
                    {
                        if (cur != null)
                        {
                            cur.End = now;
                            Close(cur);
                            cur = null;
                            Store.WriteCurrent(null);
                        }
                        continue;
                    }

                    if (cur == null || !string.Equals(cur.App, display, StringComparison.Ordinal))
                    {
                        if (cur != null) { cur.End = now; Close(cur); }
                        cur = new Session();
                        cur.App = display;
                        cur.Title = title;
                        cur.Path = path;
                        cur.Start = now;
                        cur.End = now;
                    }
                    else
                    {
                        cur.End = now;
                        cur.Title = title;
                        if (path.Length > 0) cur.Path = path;
                    }
                    Store.WriteCurrent(cur);
                }
                catch { }
            }
        }

        public static void EnsureRunning()
        {
            if (Store.TrackerAlive()) return;
            string exe = AppInfo.ExePath;
            if (exe.Length == 0) return;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exe;
                psi.Arguments = "--tracker";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                try { psi.WorkingDirectory = Path.GetDirectoryName(exe); } catch { }
                Process.Start(psi);
            }
            catch { }
        }
    }

    // ============ 读取 Windows 自带的使用痕迹 ============
    public static class WinHistory
    {
        public static string Rot13(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                char x = c;
                if (c >= 'a' && c <= 'z') x = (char)('a' + (c - 'a' + 13) % 26);
                else if (c >= 'A' && c <= 'Z') x = (char)('A' + (c - 'A' + 13) % 26);
                sb.Append(x);
            }
            return sb.ToString();
        }

        static DateTime FromFileTime(long ft)
        {
            try
            {
                if (ft <= 0) return DateTime.MinValue;
                return DateTime.FromFileTimeUtc(ft).ToLocalTime();
            }
            catch { return DateTime.MinValue; }
        }

        public static string Display(string full)
        {
            string n = full == null ? "" : full;
            int i = n.LastIndexOf('\\');
            if (i >= 0 && i + 1 < n.Length) n = n.Substring(i + 1);
            int dot = n.LastIndexOf('.');
            if (dot > 0)
            {
                string ext = n.Substring(dot).ToLowerInvariant();
                if (ext == ".exe" || ext == ".lnk" || ext == ".com" || ext == ".bat" || ext == ".cmd")
                    n = n.Substring(0, dot);
            }
            return n;
        }

        public static string KindOf(string full)
        {
            if (full == null) full = "";
            string lower = full.ToLowerInvariant();

            // 任务栏固定项 / UEME 统计项 / 系统组件，一律当系统项（界面默认不显示）
            if (lower.IndexOf("{9e3995ab") >= 0) return "系统项";
            if (lower.IndexOf("\\taskbar\\") >= 0) return "系统项";
            if (lower.IndexOf("ueme_") == 0) return "系统项";
            if (lower.EndsWith(".dll") || lower.EndsWith(".sys") || lower.EndsWith(".ocx")) return "系统项";

            // Windows 系统目录里的东西（记事本/画图这类除外）
            if (lower.IndexOf("\\windows\\system32\\") >= 0 || lower.IndexOf("\\windows\\syswow64\\") >= 0 ||
                lower.IndexOf("\\windows\\winsxs\\") >= 0)
            {
                if (lower.IndexOf("notepad.exe") >= 0 || lower.IndexOf("mspaint.exe") >= 0 || lower.IndexOf("calc.exe") >= 0)
                    return "程序";
                return "系统项";
            }

            // 系统功能页的包名（Microsoft.Windows.Computer / MicrosoftDevice.* 之类）
            if (lower.StartsWith("microsoft.") || lower.StartsWith("microsoftdevice.") || lower.StartsWith("microsoftrequired."))
                return "系统项";

            if (lower.EndsWith(".exe")) return "程序";
            if (lower.EndsWith(".lnk")) return "快捷方式";
            if (lower.EndsWith(".bat") || lower.EndsWith(".cmd")) return "脚本";
            if (lower.Length > 0 && lower.IndexOf('\\') < 0 && lower.IndexOf(':') < 0) return "应用";
            return "系统项";
        }

        public static List<Launch> ReadAll()
        {
            List<Launch> list = new List<Launch>();
            ReadUserAssist(list);
            ReadBam(list);
            list.Sort(delegate (Launch a, Launch b) { return b.LastRun.CompareTo(a.LastRun); });
            return list;
        }

        static void ReadUserAssist(List<Launch> list)
        {
            try
            {
                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist"))
                {
                    if (root == null) return;
                    foreach (string guid in root.GetSubKeyNames())
                    {
                        using (RegistryKey cnt = root.OpenSubKey(guid + @"\Count"))
                        {
                            if (cnt == null) continue;
                            foreach (string rawName in cnt.GetValueNames())
                            {
                                byte[] data = cnt.GetValue(rawName) as byte[];
                                if (data == null || data.Length < 16) continue;
                                int runs;
                                DateTime last;
                                if (data.Length >= 72)
                                {
                                    runs = BitConverter.ToInt32(data, 4);
                                    last = FromFileTime(BitConverter.ToInt64(data, 60));
                                }
                                else
                                {
                                    runs = BitConverter.ToInt32(data, 0);
                                    last = FromFileTime(BitConverter.ToInt64(data, 8));
                                }
                                if (last.Year < 2000 || last.Year > 2100) continue;
                                string name = Rot13(rawName);
                                Launch l = new Launch();
                                l.Path = name;
                                l.Name = Display(name);
                                l.Kind = KindOf(name);
                                l.LastRun = last;
                                l.Runs = runs < 0 ? 0 : runs;
                                l.Source = "系统(UserAssist)";
                                if (l.Name.Length > 0) list.Add(l);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        static void ReadBam(List<Launch> list)
        {
            try
            {
                string sid = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
                string[] subs = new string[] { @"UserSettings", @"Parameters" };
                foreach (string sub in subs)
                {
                    using (RegistryKey k = Registry.LocalMachine.OpenSubKey(
                        @"SYSTEM\CurrentControlSet\Services\bam\State\User\" + sid + @"\" + sub))
                    {
                        if (k == null) continue;
                        foreach (string name in k.GetValueNames())
                        {
                            byte[] data = k.GetValue(name) as byte[];
                            if (data == null || data.Length < 8) continue;
                            DateTime last = FromFileTime(BitConverter.ToInt64(data, 0));
                            if (last.Year < 2000 || last.Year > 2100) continue;
                            Launch l = new Launch();
                            l.Path = name;
                            l.Name = Display(name);
                            if (l.Name.Length == 0) continue;
                            l.Kind = KindOf(name);
                            l.LastRun = last;
                            l.Runs = 0;
                            l.Source = "系统(BAM)";
                            list.Add(l);
                        }
                    }
                }
            }
            catch { }
        }

        // 清除 UserAssist 记录（只删值，不删键，避免破坏系统结构）
        public static int ClearUserAssist()
        {
            int removed = 0;
            try
            {
                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist", true))
                {
                    if (root == null) return 0;
                    foreach (string guid in root.GetSubKeyNames())
                    {
                        using (RegistryKey cnt = root.OpenSubKey(guid + @"\Count", true))
                        {
                            if (cnt == null) continue;
                            string[] names = cnt.GetValueNames();
                            foreach (string n in names)
                            {
                                try { cnt.DeleteValue(n, false); removed++; }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch { }
            return removed;
        }
    }


    // ============ 会话整理：把同一程序相邻的短片段合并，列表更干净 ============
    public static class SessionUtil
    {
        public const int MergeGapSeconds = 60;

        public static List<Session> Coalesce(List<Session> raw)
        {
            List<Session> list = new List<Session>();
            if (raw != null) foreach (Session s in raw) list.Add(s);
            list.Sort(delegate (Session a, Session b) { return a.Start.CompareTo(b.Start); });

            List<Session> merged = new List<Session>();
            foreach (Session s in list)
            {
                Session last = merged.Count > 0 ? merged[merged.Count - 1] : null;
                if (last != null && !last.Live && !s.Live &&
                    string.Equals(last.App, s.App, StringComparison.Ordinal) &&
                    (s.Start - last.End).TotalSeconds <= MergeGapSeconds)
                {
                    if (s.End > last.End) last.End = s.End;
                    if (s.Title.Length > 0) last.Title = s.Title;
                    continue;
                }
                Session c = new Session();
                c.App = s.App; c.Title = s.Title; c.Path = s.Path;
                c.Start = s.Start; c.End = s.End; c.Live = s.Live;
                merged.Add(c);
            }
            return merged;
        }
    }
    // 取当前 exe 的完整路径（用于拉起后台追踪进程 / 写开机自启）
    public static class AppInfo
    {
        public static string ExePath
        {
            get
            {
                try { return Process.GetCurrentProcess().MainModule.FileName; }
                catch { }
                try { return System.Reflection.Assembly.GetEntryAssembly().Location; }
                catch { }
                return "";
            }
        }
    }
}
