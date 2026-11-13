using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PathTaken
{
    // ==================== 主窗口 ====================
    public class MainForm : Form
    {
        const int HeaderH = 88, TabRowH = 46, MarginX = 18, FooterH = 44;

        HeaderBar header;
        TabStrip tabs;
        UiButton bRefresh, bExport, bClear, bTrack, bAuto, bFolder;
        CardHost card;
        FooterBar footer;
        TodayView vToday;
        StatsView vStats;
        HistoryView vHistory;
        DetailView vDetail;
        Timer timer;

        List<Session> sessions = new List<Session>();
        Session live;
        List<Launch> launches = null;
        string lastSig = "";
        bool autoEnabled;

        public string ShotPath = null;
        public int ShotTab = 0;

        public MainForm()
        {
            Text = "Path Taken";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            ClientSize = new Size(Theme.Px(1120), Theme.Px(716));
            MinimumSize = new Size(Theme.Px(980), Theme.Px(600));
            SetStyle(ControlStyles.ResizeRedraw, true);
            try { Icon = Icon.ExtractAssociatedIcon(AppInfo.ExePath); }
            catch { }

            header = new HeaderBar();
            header.Sub = "应用足迹 · 本机软件使用记录，数据只保存在本机，不联网";
            header.DragRequested += HeaderDrag;
            header.CloseClicked += delegate { Close(); };
            header.MinClicked += delegate { WindowState = FormWindowState.Minimized; };
            header.MaxClicked += delegate { ToggleMax(); };

            tabs = new TabStrip();
            tabs.Items = new string[] { "今日时间线", "应用统计", "启动历史", "会话明细" };
            tabs.SelectedChanged += delegate { SwitchTab(); };

            bRefresh = new UiButton("刷新");
            bRefresh.Plate = Theme.Bg;
            bRefresh.Click += delegate { ReloadAll(true); ToastRefresh(); };

            bExport = new UiButton("导出 CSV");
            bExport.Plate = Theme.Bg;
            bExport.Click += delegate { ExportCsv(); };

            bClear = new UiButton("一键清除");
            bClear.Style = BtnStyle.Danger;
            bClear.Plate = Theme.Bg;
            bClear.Click += delegate { OpenClearDialog(); };

            card = new CardHost();

            vToday = new TodayView();
            vStats = new StatsView();
            vHistory = new HistoryView();
            vDetail = new DetailView();

            footer = new FooterBar();

            bTrack = new UiButton("后台记录：开");
            bTrack.Style = BtnStyle.Toggle;
            bTrack.Toggled = true;
            bTrack.Plate = Theme.Bg;
            bTrack.Click += delegate { ToggleTracking(); };

            bAuto = new UiButton("开机自启：关");
            bAuto.Style = BtnStyle.Toggle;
            bAuto.Plate = Theme.Bg;
            bAuto.Click += delegate { ToggleAutoStart(); };

            bFolder = new UiButton("数据目录");
            bFolder.Style = BtnStyle.Quiet;
            bFolder.Plate = Theme.Bg;
            bFolder.Click += delegate { OpenDataFolder(); };

            Controls.Add(header);
            Controls.Add(tabs);
            Controls.Add(card);
            Controls.Add(vToday);
            Controls.Add(vStats);
            Controls.Add(vHistory);
            Controls.Add(vDetail);
            Controls.Add(footer);
            Controls.Add(bRefresh);
            Controls.Add(bExport);
            Controls.Add(bClear);
            Controls.Add(bTrack);
            Controls.Add(bAuto);
            Controls.Add(bFolder);

            card.Controls.Add(vToday);
            card.Controls.Add(vStats);
            card.Controls.Add(vHistory);
            card.Controls.Add(vDetail);

            vStats.Visible = false;
            vHistory.Visible = false;
            vDetail.Visible = false;

            autoEnabled = AutoStart.IsEnabled();
            bAuto.Toggled = autoEnabled;
            bAuto.Text = autoEnabled ? "开机自启：开" : "开机自启：关";

            Store.Ensure();
            Tracker.EnsureRunning();
            ReloadAll(true);
            SwitchTab();

            timer = new Timer();
            timer.Interval = 3000;
            timer.Tick += delegate { Tick(); };
            timer.Start();
        }

        // ---------- 布局 ----------
        void DoLayout()
        {
            if (header == null) return;
            int W = ClientSize.Width, H = ClientSize.Height;
            header.Bounds = new Rectangle(0, 0, W, Theme.Px(HeaderH));

            int tabTop = Theme.Px(HeaderH + 6);
            tabs.Bounds = new Rectangle(Theme.Px(MarginX), tabTop, Theme.Px(470), Theme.Px(TabRowH - 6));

            int bh = Theme.Px(32);
            int by = tabTop + Theme.Px(2);
            int bw1 = Theme.Px(84), bw2 = Theme.Px(92), bw3 = Theme.Px(88), gap = Theme.Px(8);
            int right = W - Theme.Px(MarginX);
            bClear.Bounds = new Rectangle(right - bw3, by, bw3, bh);
            bExport.Bounds = new Rectangle(bClear.Left - gap - bw2, by, bw2, bh);
            bRefresh.Bounds = new Rectangle(bExport.Left - gap - bw1, by, bw1, bh);

            int cardTop = Theme.Px(HeaderH + TabRowH + 2);
            int cardH = H - cardTop - Theme.Px(FooterH + 10);
            card.Bounds = new Rectangle(Theme.Px(MarginX), cardTop, W - Theme.Px(MarginX * 2), cardH);

            int pad = Theme.Px(14), top = Theme.Px(66);
            Rectangle inner = new Rectangle(pad, top, card.Width - pad * 2, card.Height - top - Theme.Px(14));
            vToday.Bounds = inner;
            vStats.Bounds = inner;
            vHistory.Bounds = inner;
            vDetail.Bounds = inner;

            footer.Bounds = new Rectangle(Theme.Px(MarginX), H - Theme.Px(FooterH + 4), W - Theme.Px(MarginX * 2), Theme.Px(FooterH - 4));

            int fbh = Theme.Px(30), fby = footer.Top + (footer.Height - fbh) / 2;
            int fr = footer.Bounds.Right;
            bw3 = Theme.Px(88);
            bFolder.Bounds = new Rectangle(fr - bw3, fby, bw3, fbh);
            bw2 = Theme.Px(104);
            bAuto.Bounds = new Rectangle(bFolder.Left - gap - bw2, fby, bw2, fbh);
            bw1 = Theme.Px(112);
            bTrack.Bounds = new Rectangle(bAuto.Left - gap - bw1, fby, bw1, fbh);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            DoLayout();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            DoLayout();
            if (ShotTab > 0)
            {
                tabs.Selected = ShotTab;
                SwitchTab();
                tabs.Invalidate();
            }
            if (ShotPath != null) StartShot();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
        }

        // ---------- 窗口拖动 / 缩放 ----------
        const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 2, WM_NCHITTEST = 0x84;

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        void HeaderDrag(object sender, EventArgs e)
        {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }

        void ToggleMax()
        {
            if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal;
            else
            {
                MaximizedBounds = Screen.FromControl(this).WorkingArea;
                WindowState = FormWindowState.Maximized;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                int e = Theme.Px(6);
                int lp = m.LParam.ToInt32();
                Point scr = new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
                Point p = PointToClient(scr);
                bool L = p.X <= e, R = p.X >= ClientSize.Width - e - 1;
                bool T = p.Y <= e, B = p.Y >= ClientSize.Height - e - 1;
                if (T && L) { m.Result = (IntPtr)13; return; }
                if (T && R) { m.Result = (IntPtr)14; return; }
                if (B && L) { m.Result = (IntPtr)16; return; }
                if (B && R) { m.Result = (IntPtr)17; return; }
                if (L) { m.Result = (IntPtr)10; return; }
                if (R) { m.Result = (IntPtr)11; return; }
                if (T) { m.Result = (IntPtr)12; return; }
                if (B) { m.Result = (IntPtr)15; return; }
            }
            base.WndProc(ref m);
        }

        // ---------- 数据 ----------
        void ReloadAll(bool forceLaunch)
        {
            sessions = SessionUtil.Coalesce(Store.Load());

            live = null;
            if (Store.TrackerAlive())
            {
                DateTime hb;
                Session cur = Store.ReadCurrent(out hb);
                if (cur != null && (DateTime.Now - hb).TotalSeconds < 25)
                {
                    cur.End = DateTime.Now;
                    cur.Live = true;
                    live = cur;
                }
            }

            if (forceLaunch || launches == null || (DateTime.Now - lastLoad).TotalSeconds > 60)
            {
                launches = WinHistory.ReadAll();
                lastLoad = DateTime.Now;
            }

            lastSig = Sig();
            PushViews();
            UpdateFooter();
        }

        DateTime lastLoad = DateTime.MinValue;

        string Sig()
        {
            int n = sessions.Count;
            DateTime last = n > 0 ? sessions[n - 1].End : DateTime.MinValue;
            string lv = live == null ? "-" : live.App + (live.Duration.TotalMinutes.ToString("0"));
            return n + "|" + last.ToString("yyyyMMddHHmmss") + "|" + lv;
        }

        void PushViews()
        {
            List<Session> withLive = new List<Session>();
            withLive.AddRange(sessions);
            if (live != null) withLive.Add(live);
            vToday.RefreshData(withLive);
            vStats.RefreshData(withLive);
            vDetail.RefreshData(withLive);
            vHistory.RefreshData(launches);
        }

        void Tick()
        {
            bool alive = Store.TrackerAlive();
            if (!alive) Tracker.EnsureRunning();

            DateTime hb;
            Session cur = Store.ReadCurrent(out hb);
            Session newLive = null;
            if (alive && cur != null && (DateTime.Now - hb).TotalSeconds < 25)
            {
                cur.End = DateTime.Now;
                cur.Live = true;
                newLive = cur;
            }
            live = newLive;

            if (Sig() != lastSig)
            {
                ReloadAll(false);
            }
            else
            {
                vToday.TickLive(live);
                vStats.TickLive();
                UpdateFooter();
            }
        }

        void UpdateFooter()
        {
            bool alive = Store.TrackerAlive();
            int todayCount = 0;
            TimeSpan todaySpan = TimeSpan.Zero;
            DateTime dayStart = DateTime.Today, dayEnd = dayStart.AddDays(1);
            foreach (Session s in sessions)
            {
                TimeSpan ov = TimeUtil.Overlap(s, dayStart, dayEnd);
                if (ov.TotalSeconds > 0) { todayCount++; todaySpan += ov; }
            }
            if (live != null)
            {
                TimeSpan ov = TimeUtil.Overlap(live, dayStart, dayEnd);
                if (ov.TotalSeconds > 0) { todayCount++; todaySpan += ov; }
            }

            string left;
            if (alive)
            {
                left = "记录器运行中 · 今日 " + Fmt.DurShort(todaySpan) + " · " + todayCount + " 段会话";
                if (live != null) left += " · 正在使用 " + live.App;
                footer.DotColor = Theme.Green;
            }
            else
            {
                left = "记录器未运行 · 今日 " + Fmt.DurShort(todaySpan) + " · 累计 " + sessions.Count + " 段";
                footer.DotColor = Theme.Amber;
            }
            footer.StatusText = left;
            footer.TailText = "";
            footer.Invalidate();
        }

        void ToastRefresh()
        {
            UpdateFooter();
        }

        void SwitchTab()
        {
            int i = tabs.Selected;
            vToday.Visible = i == 0;
            vStats.Visible = i == 1;
            vHistory.Visible = i == 2;
            vDetail.Visible = i == 3;

            if (i == 0) { card.Title = "今日时间线"; card.Sub = "今天每一段前台使用的时间段：几点开、几点关、用了多久"; }
            if (i == 1) { card.Title = "应用统计"; card.Sub = "按程序汇总：今天 / 近 7 天 / 累计用了多久、用了几次"; }
            if (i == 2) { card.Title = "启动历史"; card.Sub = "Windows 自己记的“程序启动记录”（只有启动时间和次数，没有时长）"; }
            if (i == 3) { card.Title = "会话明细"; card.Sub = "每一段使用的完整清单，可导出 CSV"; }
            card.Invalidate();

            if (i == 2 && launches == null) LoadLaunches();
        }

        void LoadLaunches()
        {
            launches = WinHistory.ReadAll();
            lastLoad = DateTime.Now;
            vHistory.RefreshData(launches);
        }

        // ---------- 功能按钮 ----------
        void ToggleTracking()
        {
            if (bTrack.Toggled)
            {
                Tracker.Stop();
                bTrack.Toggled = false;
                bTrack.Text = "后台记录：关";
                Store.ClearHeartbeat();
            }
            else
            {
                Tracker.EnsureRunning();
                bTrack.Toggled = true;
                bTrack.Text = "后台记录：开";
            }
            bTrack.Invalidate();
            UpdateFooter();
        }

        void ToggleAutoStart()
        {
            try
            {
                bool want = !AutoStart.IsEnabled();
                AutoStart.Set(want);
                bAuto.Toggled = want;
                bAuto.Text = want ? "开机自启：开" : "开机自启：关";
                bAuto.Invalidate();
            }
            catch
            {
                MessageBox.Show("写入开机自启失败（可能是安全软件拦截）。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void OpenDataFolder()
        {
            try
            {
                Store.Ensure();
                Process.Start("explorer.exe", "\"" + Store.Root + "\"");
            }
            catch { }
        }

        void ExportCsv()
        {
            try
            {
                SaveFileDialog dlg = new SaveFileDialog();
                dlg.Filter = "CSV 文件 (*.csv)|*.csv";
                dlg.FileName = "PathTaken_" + DateTime.Now.ToString("yyyyMMdd") + ".csv";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("日期,应用,窗口标题,开始时间,结束时间,时长(分钟),时长");
                List<Session> all = new List<Session>();
                all.AddRange(sessions);
                if (live != null) all.Add(live);
                all.Sort(delegate (Session a, Session b) { return b.Start.CompareTo(a.Start); });
                foreach (Session s in all)
                {
                    sb.Append(Csv(s.Start.ToString("yyyy-MM-dd"))).Append(',');
                    sb.Append(Csv(s.App)).Append(',');
                    sb.Append(Csv(s.Title)).Append(',');
                    sb.Append(Csv(s.Start.ToString("HH:mm:ss"))).Append(',');
                    sb.Append(Csv(s.End.ToString("HH:mm:ss"))).Append(',');
                    sb.Append(Math.Round(s.Duration.TotalMinutes, 1).ToString()).Append(',');
                    sb.AppendLine(Csv(Fmt.Dur(s.Duration)));
                }
                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                MessageBox.Show("已导出到：\n" + dlg.FileName, "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("导出失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        static string Csv(string s)
        {
            if (s == null) s = "";
            if (s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0 || s.IndexOf('\n') >= 0)
                s = "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        void OpenClearDialog()
        {
            ClearDialog dlg = new ClearDialog();
            dlg.ShowDialog(this);
            if (dlg.Done)
            {
                ReloadAll(true);
                MessageBox.Show(dlg.ResultText, "清除完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // ---------- 截图（用于生成 README 预览图） ----------
        void StartShot()
        {
            Timer t = new Timer();
            t.Interval = 1500;
            t.Tick += delegate
            {
                t.Stop();
                try
                {
                    Bitmap bmp = new Bitmap(Width, Height);
                    using (Graphics g = Graphics.FromImage(bmp)) DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                    bmp.Save(ShotPath, ImageFormat.Png);
                    bmp.Dispose();
                }
                catch { }
                Close();
            };
            t.Start();
        }
    }

    // ==================== 时间计算工具 ====================
    public static class TimeUtil
    {
        public static TimeSpan Overlap(Session s, DateTime from, DateTime to)
        {
            DateTime a = s.Start > from ? s.Start : from;
            DateTime b = s.End < to ? s.End : to;
            if (s.Live && b > DateTime.Now) b = DateTime.Now;
            if (b <= a) return TimeSpan.Zero;
            return b - a;
        }
    }

    // ==================== 今日时间线 ====================
    public class TodayView : UserControl
    {
        StatCard c1, c2, c3;
        TextLine line;
        TimeLineCtl timeline;
        DataGridView grid;
        List<Session> data = new List<Session>();

        public TodayView()
        {
            BackColor = Theme.Card;

            c1 = new StatCard(); c1.Caption = "今日使用时长"; c1.Tint = Theme.Accent; c1.Plate = Theme.Card;
            c2 = new StatCard(); c2.Caption = "使用过的应用"; c2.Tint = Theme.Green; c2.Plate = Theme.Card;
            c3 = new StatCard(); c3.Caption = "今日使用最多"; c3.Tint = Theme.Amber; c3.Plate = Theme.Card;

            line = new TextLine();
            line.Plate = Theme.Card;
            line.Color2 = Theme.Dim;
            line.Text = "正在读取…";

            timeline = new TimeLineCtl();
            timeline.Plate = Theme.Card;
            timeline.Live = true;

            grid = Grids.Make();
            grid.Columns.Add(Grids.Col("app", "应用", 20, false));
            grid.Columns.Add(Grids.Col("title", "窗口标题", 40, false));
            grid.Columns.Add(Grids.Col("start", "开始", 12, true));
            grid.Columns.Add(Grids.Col("end", "结束", 12, true));
            grid.Columns.Add(Grids.Col("dur", "时长", 13, true));

            Controls.Add(c1); Controls.Add(c2); Controls.Add(c3);
            Controls.Add(line); Controls.Add(timeline); Controls.Add(grid);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            DoLayout();
        }

        void DoLayout()
        {
            int W = Width, gap = Theme.Px(10);
            int cardH = Theme.Px(82);
            int cw = (W - gap * 2) / 3;
            c1.Bounds = new Rectangle(0, 0, cw, cardH);
            c2.Bounds = new Rectangle(cw + gap, 0, cw, cardH);
            c3.Bounds = new Rectangle((cw + gap) * 2, 0, W - (cw + gap) * 2, cardH);

            line.Bounds = new Rectangle(Theme.Px(2), cardH + Theme.Px(8), W - Theme.Px(4), Theme.Px(20));
            int tlTop = cardH + Theme.Px(30);
            timeline.Bounds = new Rectangle(0, tlTop, W, Theme.Px(108));
            int gridTop = tlTop + Theme.Px(108) + Theme.Px(12);
            grid.Bounds = new Rectangle(0, gridTop, W, Math.Max(Theme.Px(60), Height - gridTop));
        }

        public void RefreshData(List<Session> all)
        {
            data = all;
            DateTime dayStart = DateTime.Today, dayEnd = dayStart.AddDays(1);
            TimeSpan total = TimeSpan.Zero;
            HashSet<string> apps = new HashSet<string>();
            Dictionary<string, TimeSpan> per = new Dictionary<string, TimeSpan>();
            List<Session> today = new List<Session>();

            foreach (Session s in all)
            {
                TimeSpan ov = TimeUtil.Overlap(s, dayStart, dayEnd);
                if (ov.TotalSeconds < 1) continue;
                total += ov;
                apps.Add(s.App);
                if (per.ContainsKey(s.App)) per[s.App] = per[s.App] + ov; else per[s.App] = ov;

                Session c = new Session();
                c.App = s.App; c.Title = s.Title; c.Path = s.Path; c.Live = s.Live;
                c.Start = s.Start < dayStart ? dayStart : s.Start;
                c.End = s.End > dayEnd ? dayEnd : s.End;
                today.Add(c);
            }
            today.Sort(delegate (Session a, Session b) { return a.Start.CompareTo(b.Start); });

            c1.Value = Fmt.DurShort(total);
            c1.Sub = total.TotalSeconds < 1 ? "今天还没有记录" : "占一天 " + (total.TotalMinutes / 1440.0 * 100).ToString("0.0") + "%";
            c2.Value = apps.Count + " 个";
            c2.Sub = today.Count + " 段会话";
            c1.Invalidate(); c2.Invalidate();

            string topApp = "";
            TimeSpan topSpan = TimeSpan.Zero;
            foreach (KeyValuePair<string, TimeSpan> kv in per)
                if (kv.Value > topSpan) { topSpan = kv.Value; topApp = kv.Key; }
            c3.Value = topApp.Length > 0 ? topApp : "—";
            c3.Sub = topApp.Length > 0 ? "累计 " + Fmt.DurShort(topSpan) : "暂无数据";
            c3.Invalidate();

            timeline.Day = DateTime.Today;
            timeline.Sessions = today;
            timeline.Live = true;
            timeline.Invalidate();

            Fill(today);
            Session lv = null;
            foreach (Session x in today) if (x.Live) lv = x;
            TickLive(lv);
        }

        public void TickLive(Session liveSession)
        {
            bool alive = Store.TrackerAlive();
            if (!alive)
            {
                line.Text = "记录器未运行，时间线不会增长。点右下角“后台记录：关”可开启。";
                line.Color2 = Theme.Amber;
            }
            else if (liveSession != null)
            {
                line.Text = "正在使用：" + liveSession.App + "  ·  已持续 " + Fmt.DurShort(liveSession.Duration) +
                            (liveSession.Title.Length > 0 ? "   " + liveSession.Title : "");
                line.Color2 = Theme.Dim;
            }
            else
            {
                line.Text = "记录器运行中，当前没有前台窗口在使用（或已判定为空闲）。";
                line.Color2 = Theme.Muted;
            }
            line.Invalidate();
            timeline.Invalidate();
            // 进行中的会话同步刷新时长
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                Session s = grid.Rows[i].Tag as Session;
                if (s != null && s.Live)
                {
                    s.End = DateTime.Now;
                    grid.Rows[i].Cells["end"].Value = "进行中";
                    grid.Rows[i].Cells["dur"].Value = Fmt.DurShort(s.Duration);
                }
            }
        }

        void Fill(List<Session> today)
        {
            int first = 0;
            try { if (grid.FirstDisplayedScrollingRowIndex > 0) first = grid.FirstDisplayedScrollingRowIndex; } catch { }
            grid.SuspendLayout();
            grid.Rows.Clear();
            foreach (Session s in today)
            {
                int i = grid.Rows.Add();
                DataGridViewRow r = grid.Rows[i];
                r.Tag = s;
                r.Cells["app"].Value = s.App;
                r.Cells["app"].Style.ForeColor = Theme.AppColor(s.App);
                r.Cells["title"].Value = s.Title;
                r.Cells["title"].Style.ForeColor = Theme.Muted;
                r.Cells["start"].Value = Fmt.Clock(s.Start);
                r.Cells["end"].Value = s.Live ? "进行中" : Fmt.Clock(s.End);
                r.Cells["dur"].Value = Fmt.DurShort(s.Duration);
                if (s.Live)
                {
                    r.Cells["end"].Style.ForeColor = Theme.Green;
                    r.Cells["dur"].Style.ForeColor = Theme.Green;
                }
            }
            grid.ResumeLayout();
            try { if (first > 0 && grid.Rows.Count > first) grid.FirstDisplayedScrollingRowIndex = first; } catch { }
        }
    }

    // ==================== 应用统计 ====================
    public class StatsView : UserControl
    {
        class Row
        {
            public string App; public TimeSpan Today, Week, Total; public int Count; public DateTime Last;
        }

        DataGridView grid;
        List<Row> rows = new List<Row>();
        string sortCol = "total";
        bool sortDesc = true;

        public StatsView()
        {
            BackColor = Theme.Card;
            grid = Grids.Make();
            grid.Columns.Add(Grids.Col("app", "应用", 26, false));
            grid.Columns.Add(Grids.Col("today", "今日", 11, true));
            grid.Columns.Add(Grids.Col("week", "近 7 天", 11, true));
            grid.Columns.Add(Grids.Col("total", "累计", 12, true));
            grid.Columns.Add(Grids.Col("count", "使用次数", 10, true));
            grid.Columns.Add(Grids.Col("last", "最近使用", 18, true));
            grid.ColumnHeaderMouseClick += Grid_HeaderClick;
            Controls.Add(grid);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            grid.Bounds = new Rectangle(0, 0, Width, Height);
        }

        void Grid_HeaderClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            string col = grid.Columns[e.ColumnIndex].Name;
            if (sortCol == col) sortDesc = !sortDesc; else { sortCol = col; sortDesc = true; }
            Sort();
            Fill();
        }

        public void RefreshData(List<Session> all)
        {
            DateTime now = DateTime.Now;
            DateTime dayStart = DateTime.Today;
            DateTime weekStart = dayStart.AddDays(-6);

            Dictionary<string, Row> map = new Dictionary<string, Row>();
            foreach (Session s in all)
            {
                Row r;
                if (!map.TryGetValue(s.App, out r))
                {
                    r = new Row();
                    r.App = s.App;
                    r.Last = s.Start;
                    map[s.App] = r;
                }
                TimeSpan tot = TimeUtil.Overlap(s, DateTime.MinValue.AddYears(1), now.AddDays(1));
                r.Total += tot;
                r.Today += TimeUtil.Overlap(s, dayStart, dayStart.AddDays(1));
                r.Week += TimeUtil.Overlap(s, weekStart, dayStart.AddDays(1));
                r.Count++;
                if (s.End > r.Last) r.Last = s.End;
            }
            rows = new List<Row>();
            foreach (Row r in map.Values) rows.Add(r);

            Sort();
            Fill();
        }

        void Sort()
        {
            Comparison<Row> cmp = null;
            switch (sortCol)
            {
                case "app": cmp = delegate (Row a, Row b) { return string.Compare(a.App, b.App); }; break;
                case "today": cmp = delegate (Row a, Row b) { return a.Today.CompareTo(b.Today); }; break;
                case "week": cmp = delegate (Row a, Row b) { return a.Week.CompareTo(b.Week); }; break;
                case "count": cmp = delegate (Row a, Row b) { return a.Count.CompareTo(b.Count); }; break;
                case "last": cmp = delegate (Row a, Row b) { return a.Last.CompareTo(b.Last); }; break;
                default: cmp = delegate (Row a, Row b) { return a.Total.CompareTo(b.Total); }; break;
            }
            rows.Sort(cmp);
            if (sortDesc)
            {
                List<Row> rev = new List<Row>(rows);
                rev.Reverse();
                rows = rev;
            }
        }

        void Fill()
        {
            int first = 0;
            try { if (grid.FirstDisplayedScrollingRowIndex > 0) first = grid.FirstDisplayedScrollingRowIndex; } catch { }
            grid.SuspendLayout();
            grid.Rows.Clear();
            foreach (Row r in rows)
            {
                int i = grid.Rows.Add();
                DataGridViewRow gr = grid.Rows[i];
                gr.Cells["app"].Value = r.App;
                gr.Cells["app"].Style.ForeColor = Theme.AppColor(r.App);
                gr.Cells["today"].Value = r.Today.TotalSeconds < 1 ? "—" : Fmt.DurShort(r.Today);
                gr.Cells["week"].Value = r.Week.TotalSeconds < 1 ? "—" : Fmt.DurShort(r.Week);
                gr.Cells["total"].Value = Fmt.DurShort(r.Total);
                gr.Cells["total"].Style.ForeColor = Theme.Text;
                gr.Cells["count"].Value = r.Count + " 次";
                gr.Cells["count"].Style.ForeColor = Theme.Muted;
                gr.Cells["last"].Value = Fmt.Ago(r.Last);
                gr.Cells["last"].Style.ForeColor = Theme.Muted;
            }
            grid.ResumeLayout();
            try { if (first > 0 && grid.Rows.Count > first) grid.FirstDisplayedScrollingRowIndex = first; } catch { }
        }

        public void TickLive() { }
    }

    // ==================== 启动历史（系统记录） ====================
    public class HistoryView : UserControl
    {
        UiButton bRange;
        TextLine hint;
        SearchField search;
        DataGridView grid;
        List<Launch> all = new List<Launch>();
        int range = 0; // 0全部 1今天 2近7天 3近30天
        string sortCol = "last";
        bool sortDesc = true;

        public HistoryView()
        {
            BackColor = Theme.Card;

            bRange = new UiButton("全部时间");
            bRange.Style = BtnStyle.Ghost;
            bRange.Plate = Theme.Card;
            bRange.Click += delegate
            {
                range = (range + 1) % 4;
                string[] names = new string[] { "全部时间", "今天", "近 7 天", "近 30 天" };
                bRange.Text = names[range];
                bRange.Invalidate();
                Fill();
            };

            hint = new TextLine();
            hint.Plate = Theme.Card;
            hint.Color2 = Theme.Muted;
            hint.Font2 = Theme.F(8.5f);
            hint.Text = "";

            search = new SearchField();
            search.Plate = Theme.Card;
            search.Placeholder = "搜索程序名…";
            search.Box.TextChanged += delegate { Fill(); };

            grid = Grids.Make();
            grid.Columns.Add(Grids.Col("app", "应用", 40, false));
            grid.Columns.Add(Grids.Col("kind", "类型", 12, false));
            grid.Columns.Add(Grids.Col("last", "最后启动", 24, true));
            grid.Columns.Add(Grids.Col("runs", "启动次数", 12, true));
            grid.Columns.Add(Grids.Col("src", "来源", 18, true));
            grid.ColumnHeaderMouseClick += Grid_HeaderClick;

            Controls.Add(bRange); Controls.Add(hint); Controls.Add(search); Controls.Add(grid);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int top = Theme.Px(44);
            bRange.Bounds = new Rectangle(0, 0, Theme.Px(96), Theme.Px(30));
            hint.Bounds = new Rectangle(Theme.Px(108), 0, Math.Max(Theme.Px(80), Width - Theme.Px(108) - Theme.Px(256)), Theme.Px(32));
            int sw = Theme.Px(240);
            search.Bounds = new Rectangle(Width - sw, Theme.Px(1), sw, Theme.Px(30));
            grid.Bounds = new Rectangle(0, top, Width, Math.Max(Theme.Px(80), Height - top));
        }

        void Grid_HeaderClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            string col = grid.Columns[e.ColumnIndex].Name;
            if (sortCol == col) sortDesc = !sortDesc; else { sortCol = col; sortDesc = true; }
            Fill();
        }

        public void RefreshData(List<Launch> list)
        {
            all = list == null ? new List<Launch>() : list;
            Fill();
        }

        void Fill()
        {
            int first = 0;
            try { if (grid.FirstDisplayedScrollingRowIndex > 0) first = grid.FirstDisplayedScrollingRowIndex; } catch { }

            List<Launch> items = new List<Launch>();
            DateTime dayStart = DateTime.Today;
            DateTime from = DateTime.MinValue;
            if (range == 1) from = dayStart;
            else if (range == 2) from = dayStart.AddDays(-6);
            else if (range == 3) from = dayStart.AddDays(-29);

            string q = search.Value.Trim().ToLowerInvariant();
            int hidden = 0;
            foreach (Launch l in all)
            {
                if (l.Kind == "系统项") { hidden++; continue; }
                if (l.LastRun < from) continue;
                if (q.Length > 0 && l.Name.ToLowerInvariant().IndexOf(q) < 0) continue;
                items.Add(l);
            }

            Comparison<Launch> cmp = null;
            switch (sortCol)
            {
                case "app": cmp = delegate (Launch a, Launch b) { return string.Compare(a.Name, b.Name); }; break;
                case "kind": cmp = delegate (Launch a, Launch b) { return string.Compare(a.Kind, b.Kind); }; break;
                case "runs": cmp = delegate (Launch a, Launch b) { return a.Runs.CompareTo(b.Runs); }; break;
                case "src": cmp = delegate (Launch a, Launch b) { return string.Compare(a.Source, b.Source); }; break;
                default: cmp = delegate (Launch a, Launch b) { return a.LastRun.CompareTo(b.LastRun); }; break;
            }
            items.Sort(cmp);
            if (sortDesc) items.Reverse();

            grid.SuspendLayout();
            grid.Rows.Clear();
            foreach (Launch l in items)
            {
                int i = grid.Rows.Add();
                DataGridViewRow r = grid.Rows[i];
                r.Cells["app"].Value = l.Name;
                r.Cells["app"].Style.ForeColor = Theme.AppColor(l.Name);
                r.Cells["kind"].Value = l.Kind;
                r.Cells["kind"].Style.ForeColor = Theme.Muted;
                r.Cells["last"].Value = Fmt.Stamp(l.LastRun);
                r.Cells["runs"].Value = l.Runs > 0 ? l.Runs + " 次" : "—";
                r.Cells["runs"].Style.ForeColor = Theme.Muted;
                r.Cells["src"].Value = l.Source;
                r.Cells["src"].Style.ForeColor = Theme.Muted;
            }
            grid.ResumeLayout();
            try { if (first > 0 && grid.Rows.Count > first) grid.FirstDisplayedScrollingRowIndex = first; } catch { }

            hint.Text = "只显示常用程序（浏览器 / 游戏 / 办公 / 工具），已自动过滤 " + hidden + " 条系统组件";
        }
    }

    // ==================== 会话明细 ====================
    public class DetailView : UserControl
    {
        UiButton bRange;
        DataGridView grid;
        List<Session> all = new List<Session>();
        int range = 3;

        public DetailView()
        {
            BackColor = Theme.Card;
            bRange = new UiButton("全部时间");
            bRange.Style = BtnStyle.Ghost;
            bRange.Plate = Theme.Card;
            bRange.Click += delegate
            {
                range = (range + 1) % 4;
                string[] names = new string[] { "全部时间", "今天", "近 7 天", "近 30 天" };
                bRange.Text = names[range];
                bRange.Invalidate();
                Fill();
            };

            grid = Grids.Make();
            grid.Columns.Add(Grids.Col("day", "日期", 14, false));
            grid.Columns.Add(Grids.Col("app", "应用", 20, false));
            grid.Columns.Add(Grids.Col("title", "窗口标题", 34, false));
            grid.Columns.Add(Grids.Col("start", "开始", 10, true));
            grid.Columns.Add(Grids.Col("end", "结束", 10, true));
            grid.Columns.Add(Grids.Col("dur", "时长", 10, true));
            Controls.Add(bRange); Controls.Add(grid);
            bRange.Text = "全部时间";
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int top = Theme.Px(44);
            bRange.Bounds = new Rectangle(0, 0, Theme.Px(96), Theme.Px(30));
            grid.Bounds = new Rectangle(0, top, Width, Math.Max(Theme.Px(80), Height - top));
        }

        public void RefreshData(List<Session> list)
        {
            all = list == null ? new List<Session>() : list;
            Fill();
        }

        void Fill()
        {
            int first = 0;
            try { if (grid.FirstDisplayedScrollingRowIndex > 0) first = grid.FirstDisplayedScrollingRowIndex; } catch { }

            DateTime dayStart = DateTime.Today;
            DateTime from = DateTime.MinValue;
            if (range == 1) from = dayStart;
            else if (range == 2) from = dayStart.AddDays(-6);
            else if (range == 3) from = dayStart.AddDays(-29);

            List<Session> items = new List<Session>();
            foreach (Session s in all) if (s.End >= from) items.Add(s);
            items.Sort(delegate (Session a, Session b) { return b.Start.CompareTo(a.Start); });

            grid.SuspendLayout();
            grid.Rows.Clear();
            string lastDay = "";
            foreach (Session s in items)
            {
                int i = grid.Rows.Add();
                DataGridViewRow r = grid.Rows[i];
                string d = s.Start.ToString("yyyy-MM-dd");
                r.Cells["day"].Value = d == lastDay ? "" : Fmt.DayName(s.Start) + " " + d.Substring(5);
                lastDay = d;
                r.Cells["app"].Value = s.App;
                r.Cells["app"].Style.ForeColor = Theme.AppColor(s.App);
                r.Cells["title"].Value = s.Title;
                r.Cells["title"].Style.ForeColor = Theme.Muted;
                r.Cells["start"].Value = Fmt.Clock(s.Start);
                r.Cells["end"].Value = s.Live ? "进行中" : Fmt.Clock(s.End);
                r.Cells["dur"].Value = Fmt.DurShort(s.Duration);
                if (s.Live) { r.Cells["end"].Style.ForeColor = Theme.Green; r.Cells["dur"].Style.ForeColor = Theme.Green; }
            }
            grid.ResumeLayout();
            try { if (first > 0 && grid.Rows.Count > first) grid.FirstDisplayedScrollingRowIndex = first; } catch { }
        }
    }

    // ==================== 一键清除对话框 ====================
    public class ClearDialog : Form
    {
        public bool Done = false;
        public string ResultText = "";

        UiCheck cOwn, cSys, cBackup;
        UiButton bCancel, bOk;
        TextBlock note;

        public ClearDialog()
        {
            Text = "一键清除";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg;
            ClientSize = new Size(Theme.Px(560), Theme.Px(478));
            DoubleBuffered = true;
            ShowInTaskbar = false;

            cOwn = new UiCheck("本软件记录的使用时长（时间线 / 统计 / 明细）");
            cOwn.Checked = true;
            cOwn.Plate = Theme.Bg;
            cOwn.Height = Theme.Px(28);

            cSys = new UiCheck("Windows 自带的程序启动记录（UserAssist）");
            cSys.Plate = Theme.Bg;
            cSys.Height = Theme.Px(28);

            cBackup = new UiCheck("清除前先备份一份到本地 backup 目录");
            cBackup.Checked = true;
            cBackup.Plate = Theme.Bg;
            cBackup.Height = Theme.Px(28);

            note = new TextBlock();
            note.Plate = Theme.Card;
            note.Color2 = Theme.Muted;
            note.Font2 = Theme.F(8.8f);
            note.Text = "说明：\n" +
                "· 本软件记录是精确的“开始 / 结束 / 时长”，清除后无法恢复。\n" +
                "· Windows 启动记录只有“最后启动时间 + 启动次数”，清除后这些条目会消失；\n" +
                "   正在运行的程序会被系统重新写回一条。\n" +
                "· 只清上面两项，不删你的任何文件、浏览器历史或系统日志。";

            bCancel = new UiButton("取消");
            bCancel.Style = BtnStyle.Ghost;
            bCancel.Plate = Theme.Bg;
            bCancel.Click += delegate { Close(); };

            bOk = new UiButton("确认清除");
            bOk.Style = BtnStyle.Danger;
            bOk.Plate = Theme.Bg;
            bOk.Click += delegate { Execute(); };

            Controls.Add(cOwn); Controls.Add(cSys); Controls.Add(cBackup);
            Controls.Add(note); Controls.Add(bCancel); Controls.Add(bOk);
            MouseDown += Drag;
            // 触发一次布局：ctor 里设 ClientSize 时子控件还没建，OnResize 会提前返回
            ClientSize = new Size(Theme.Px(560), Theme.Px(478));
        }

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        void Drag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, 2, 0);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (cOwn == null || cSys == null || cBackup == null || note == null || bCancel == null || bOk == null) return;
            int x = Theme.Px(28);
            cOwn.Bounds = new Rectangle(x, Theme.Px(132), ClientSize.Width - x * 2, Theme.Px(28));
            cSys.Bounds = new Rectangle(x, Theme.Px(166), ClientSize.Width - x * 2, Theme.Px(28));
            cBackup.Bounds = new Rectangle(x, Theme.Px(200), ClientSize.Width - x * 2, Theme.Px(28));
            note.Bounds = new Rectangle(x, Theme.Px(236), ClientSize.Width - x * 2, Theme.Px(150));
            bCancel.Bounds = new Rectangle(ClientSize.Width - Theme.Px(28) - Theme.Px(90) - Theme.Px(10) - Theme.Px(112),
                ClientSize.Height - Theme.Px(58), Theme.Px(112), Theme.Px(34));
            bOk.Bounds = new Rectangle(ClientSize.Width - Theme.Px(28) - Theme.Px(90), ClientSize.Height - Theme.Px(58),
                Theme.Px(90), Theme.Px(34));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg);
            RectangleF r = new RectangleF(0.5f, 0.5f, ClientSize.Width - 1.5f, ClientSize.Height - 1.5f);
            Painter.Fill(g, r, Theme.Px(12), Theme.Card);
            Painter.Stroke(g, r, Theme.Px(12), Theme.Border, 1f);

            Painter.Txt(g, "一键清除", Theme.F(15f, FontStyle.Bold), new Rectangle(Theme.Px(28), Theme.Px(22), Theme.Px(300), Theme.Px(30)), Theme.Text, Painter.L);
            Painter.Txt(g, "选择要清除的内容，未勾选的不会被改动", Theme.F(9f), new Rectangle(Theme.Px(28), Theme.Px(52), ClientSize.Width - Theme.Px(56), Theme.Px(20)), Theme.Muted, Painter.L);

            using (Pen pen = new Pen(Theme.Border, 1f))
                g.DrawLine(pen, Theme.Px(28), Theme.Px(112), ClientSize.Width - Theme.Px(28), Theme.Px(112));
            base.OnPaint(e);
        }

        void Execute()
        {
            if (!cOwn.Checked && !cSys.Checked)
            {
                MessageBox.Show("请至少勾选一项。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult a = MessageBox.Show("确认要清除选中的记录吗？此操作不可撤销。", "请再确认一次",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            if (a != DialogResult.OK) return;

            ResultText = DoClear(cOwn.Checked, cSys.Checked, cBackup.Checked);
            Done = true;
            Close();
        }

        // 真正执行清除：与界面无关，便于自测
        public static string DoClear(bool own, bool sys, bool backup)
        {
            StringBuilder sb = new StringBuilder();
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

            if (backup)
            {
                try
                {
                    string dir = Path.Combine(Store.BackupDir, stamp);
                    Directory.CreateDirectory(dir);
                    if (File.Exists(Store.SessionsFile))
                        File.Copy(Store.SessionsFile, Path.Combine(dir, "sessions.log"), true);
                    if (sys)
                    {
                        StringBuilder tsv = new StringBuilder();
                        tsv.AppendLine("名称\t类型\t最后启动\t启动次数\t来源\t路径");
                        foreach (Launch l in WinHistory.ReadAll())
                            tsv.AppendLine(l.Name + "\t" + l.Kind + "\t" + Fmt.Stamp(l.LastRun) + "\t" + l.Runs + "\t" + l.Source + "\t" + l.Path);
                        File.WriteAllText(Path.Combine(dir, "系统启动记录.tsv"), tsv.ToString(), new UTF8Encoding(false));
                    }
                    sb.AppendLine("· 已备份到：" + dir);
                }
                catch (Exception ex) { sb.AppendLine("· 备份失败：" + ex.Message); }
            }

            if (own)
            {
                int sessionsBefore = Store.Load().Count;
                Tracker.Stop();
                Store.ClearOwn();
                Store.ClearHeartbeat();
                sb.AppendLine("· 已清除本软件全部使用记录（" + sessionsBefore + " 段会话，" + stamp + "）");
                if (AutoStart.IsEnabled()) Tracker.EnsureRunning();
            }

            if (sys)
            {
                int removed = WinHistory.ClearUserAssist();
                sb.AppendLine("· 已清除 Windows 启动记录 " + removed + " 条");
            }

            return sb.ToString().TrimEnd();
        }
    }

    // ==================== 开机自启 ====================
    public static class AutoStart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "PathTaken";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    if (k == null) return false;
                    return k.GetValue(RunName) != null;
                }
            }
            catch { return false; }
        }

        public static void Set(bool on)
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (k == null) return;
                if (on)
                {
                    k.SetValue(RunName, "\"" + AppInfo.ExePath + "\" --tracker");
                    k.DeleteValue("AppTrace", false);   // 清掉旧版名字，避免开机启两次
                }
                else
                {
                    k.DeleteValue(RunName, false);
                    k.DeleteValue("AppTrace", false);
                }
            }
        }
    }
}
