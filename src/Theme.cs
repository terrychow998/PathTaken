using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PathTaken
{
    // ============ 主题（颜色 / 字体 / 缩放） ============
    public static class Theme
    {
        public static Color Bg = Color.FromArgb(13, 17, 23);
        public static Color Card = Color.FromArgb(19, 25, 34);
        public static Color Card2 = Color.FromArgb(26, 33, 44);
        public static Color Border = Color.FromArgb(38, 48, 62);
        public static Color BorderHot = Color.FromArgb(76, 141, 255);
        public static Color Text = Color.FromArgb(232, 238, 245);
        public static Color Dim = Color.FromArgb(148, 163, 184);
        public static Color Muted = Color.FromArgb(100, 116, 139);
        public static Color Accent = Color.FromArgb(76, 141, 255);
        public static Color Green = Color.FromArgb(46, 196, 139);
        public static Color Red = Color.FromArgb(240, 82, 82);
        public static Color Amber = Color.FromArgb(242, 177, 74);

        public static Color[] Palette = new Color[]
        {
            Color.FromArgb(76, 141, 255),  Color.FromArgb(46, 196, 139),
            Color.FromArgb(242, 177, 74),  Color.FromArgb(185, 129, 255),
            Color.FromArgb(79, 195, 247),  Color.FromArgb(255, 138, 101),
            Color.FromArgb(163, 230, 53),  Color.FromArgb(240, 98, 146),
            Color.FromArgb(124, 156, 255), Color.FromArgb(52, 211, 153),
            Color.FromArgb(253, 186, 116), Color.FromArgb(192, 132, 252)
        };

        public static float S = 1f; // DPI 缩放系数

        static string family;
        static Dictionary<string, Font> fontCache = new Dictionary<string, Font>();

        static Theme()
        {
            string[] candidates = new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" };
            foreach (string c in candidates)
            {
                try { FontFamily ff = new FontFamily(c); family = c; break; }
                catch { }
            }
            if (family == null) family = "Arial";
        }

        public static int Px(float v) { return (int)Math.Round(v * S); }

        public static Font F(float size) { return F(size, FontStyle.Regular); }

        public static Font F(float size, FontStyle style)
        {
            string key = size.ToString("0.0") + "|" + ((int)style).ToString();
            Font f;
            if (fontCache.TryGetValue(key, out f)) return f;
            f = new Font(family, size, style, GraphicsUnit.Point);
            fontCache[key] = f;
            return f;
        }

        public static Color Alpha(Color c, float a)
        {
            int al = (int)(255 * Math.Max(0f, Math.Min(1f, a)));
            return Color.FromArgb(al, c.R, c.G, c.B);
        }

        public static Color AppColor(string app)
        {
            int h = 7;
            if (app == null) return Palette[0];
            for (int i = 0; i < app.Length; i++) h = (h * 131 + (int)app[i]) & 0x7fffffff;
            return Palette[h % Palette.Length];
        }
    }

    // ============ 绘制工具 ============
    public static class Painter
    {
        public static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            if (rad <= 0.5f || r.Width <= 1 || r.Height <= 1) { p.AddRectangle(r); return p; }
            float d = rad * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Fill(Graphics g, RectangleF r, float rad, Color c)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath p = Round(r, rad))
            using (SolidBrush b = new SolidBrush(c))
                g.FillPath(b, p);
        }

        public static void FillGrad(Graphics g, RectangleF r, float rad, Color c1, Color c2, LinearGradientMode mode)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (r.Width <= 1 || r.Height <= 1) return;
            using (GraphicsPath p = Round(r, rad))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(r, c1, c2, mode))
                    g.FillPath(b, p);
            }
        }

        public static void Stroke(Graphics g, RectangleF r, float rad, Color c, float w)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath p = Round(r, rad))
            using (Pen pen = new Pen(c, w))
                g.DrawPath(pen, p);
        }

        public static void Txt(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags flags)
        {
            if (s == null) s = "";
            TextRenderer.DrawText(g, s, f, r, c, flags);
        }

        public const TextFormatFlags L = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        public const TextFormatFlags R = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        public const TextFormatFlags C = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        public const TextFormatFlags LT = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
    }

    // ============ 按钮 ============
    public enum BtnStyle { Primary, Ghost, Danger, Quiet, Toggle }

    public class UiButton : Control
    {
        public BtnStyle Style = BtnStyle.Ghost;
        public bool Toggled = false;
        public Color Plate = Theme.Bg;
        bool hover, down;

        public UiButton(string text)
        {
            Text = text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = Theme.F(9.5f, FontStyle.Bold);
            TabStop = false;
            Size = new Size(Theme.Px(96), Theme.Px(34));
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Plate);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color fill = Color.Empty, border = Color.Empty, txt = Theme.Text;

            if (down) r.Y += 0.5f;

            switch (Style)
            {
                case BtnStyle.Primary:
                    fill = hover ? Color.FromArgb(98, 158, 255) : Theme.Accent;
                    txt = Color.White;
                    break;
                case BtnStyle.Danger:
                    fill = Color.FromArgb(hover ? 62 : 40, Theme.Red);
                    border = Theme.Alpha(Theme.Red, hover ? 0.7f : 0.45f);
                    txt = Color.FromArgb(255, 155, 155);
                    break;
                case BtnStyle.Toggle:
                    if (Toggled) { fill = Theme.Alpha(Theme.Green, hover ? 0.22f : 0.14f); border = Theme.Alpha(Theme.Green, 0.5f); txt = Theme.Green; }
                    else { fill = Theme.Alpha(Color.White, hover ? 0.07f : 0.03f); border = Theme.Border; txt = Theme.Muted; }
                    break;
                case BtnStyle.Quiet:
                    fill = hover ? Theme.Alpha(Color.White, 0.06f) : Color.Empty;
                    txt = hover ? Theme.Text : Theme.Dim;
                    break;
                default:
                    fill = hover ? Theme.Alpha(Color.White, 0.07f) : Theme.Alpha(Color.White, 0.02f);
                    border = Theme.Border;
                    txt = hover ? Theme.Text : Theme.Dim;
                    break;
            }

            if (fill != Color.Empty) Painter.Fill(g, r, Theme.Px(7), fill);
            if (border != Color.Empty) Painter.Stroke(g, r, Theme.Px(7), border, 1f);
            Painter.Txt(g, Text, Font, Rectangle.Round(r), txt, Painter.C);
        }
    }

    // ============ 勾选框 ============
    public class UiCheck : Control
    {
        bool hover;
        public bool Checked = false;
        public Color Plate = Theme.Card;
        public Color TextColor = Theme.Text;
        public Font FontOverride = null;

        public UiCheck(string text)
        {
            Text = text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = Theme.F(9.5f);
            Height = Theme.Px(26);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { Checked = !Checked; Invalidate(); base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Plate);
            int box = Theme.Px(16);
            int y = (Height - box) / 2;
            RectangleF br = new RectangleF(1, y, box, box);
            Painter.Fill(g, br, Theme.Px(4), Checked ? Theme.Accent : (hover ? Theme.Alpha(Color.White, 0.10f) : Theme.Alpha(Color.White, 0.05f)));
            Painter.Stroke(g, br, Theme.Px(4), Checked ? Theme.Accent : Theme.Border, 1f);
            if (Checked)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(Color.White, Theme.Px(1.8f)))
                {
                    float x0 = br.X + box * 0.28f, y0 = br.Y + box * 0.52f;
                    g.DrawLine(pen, x0, y0, x0 + box * 0.18f, y0 + box * 0.22f);
                    g.DrawLine(pen, x0 + box * 0.18f, y0 + box * 0.22f, x0 + box * 0.48f, y0 - box * 0.20f);
                }
            }
            Font f = FontOverride != null ? FontOverride : Font;
            Painter.Txt(g, Text, f, new Rectangle(box + Theme.Px(8), 0, Width - box - Theme.Px(10), Height),
                        Checked ? TextColor : Theme.Dim, Painter.L);
        }
    }

    // ============ 标签页 ============
    public class TabStrip : Control
    {
        public string[] Items = new string[0];
        public int Selected = 0;
        public Color Plate = Theme.Bg;
        public event EventHandler SelectedChanged;
        int hover = -1;

        public TabStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = Theme.F(10f, FontStyle.Bold);
        }

        int[] Measure(Graphics g)
        {
            int[] xs = new int[Items.Length + 1];
            int x = Theme.Px(4);
            for (int i = 0; i < Items.Length; i++)
            {
                xs[i] = x;
                x += TextRenderer.MeasureText(g, Items[i], Font).Width + Theme.Px(34);
            }
            xs[Items.Length] = x;
            return xs;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Plate);
            int[] xs = Measure(g);
            for (int i = 0; i < Items.Length; i++)
            {
                bool active = i == Selected;
                Color c = active ? Theme.Text : (i == hover ? Theme.Dim : Theme.Muted);
                Rectangle r = new Rectangle(xs[i], 0, xs[i + 1] - xs[i], Height - Theme.Px(4));
                Painter.Txt(g, Items[i], Font, r, c, Painter.C);
                if (active)
                    Painter.Fill(g, new RectangleF(xs[i] + Theme.Px(14), Height - Theme.Px(3), xs[i + 1] - xs[i] - Theme.Px(28), Theme.Px(2.5f)), Theme.Px(1.5f), Theme.Accent);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int[] xs = Measure(CreateGraphics());
            int idx = -1;
            for (int i = 0; i < Items.Length; i++) if (e.X >= xs[i] && e.X < xs[i + 1]) idx = i;
            if (idx != hover) { hover = idx; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int[] xs = Measure(CreateGraphics());
            for (int i = 0; i < Items.Length; i++)
            {
                if (e.X >= xs[i] && e.X < xs[i + 1])
                {
                    if (Selected != i)
                    {
                        Selected = i;
                        Invalidate();
                        if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
                    }
                    break;
                }
            }
            base.OnMouseDown(e);
        }
    }

    // ============ 顶部标题栏（自带最小化/最大化/关闭） ============
    public class HeaderBar : Control
    {
        public string Title = "Path Taken";
        public string Sub = "";
        public event EventHandler DragRequested;
        public event EventHandler CloseClicked;
        public event EventHandler MinClicked;
        public event EventHandler MaxClicked;

        int hoverWin = -1, downWin = -1;

        public HeaderBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        Rectangle[] WinRects()
        {
            int w = Theme.Px(34), h = Theme.Px(26), gap = Theme.Px(2);
            Rectangle[] r = new Rectangle[3];
            for (int i = 0; i < 3; i++)
                r[i] = new Rectangle(Width - Theme.Px(10) - (3 - i) * (w + gap), Theme.Px(6), w, h);
            return r;
        }

        int HitWin(Point p)
        {
            Rectangle[] rs = WinRects();
            for (int i = 0; i < 3; i++) if (rs[i].Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitWin(e.Location);
            if (h != hoverWin) { hoverWin = h; Invalidate(); }
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hoverWin = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int h = HitWin(e.Location);
            downWin = h;
            if (h < 0) { if (DragRequested != null) DragRequested(this, EventArgs.Empty); }
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            int h = HitWin(e.Location);
            if (h >= 0 && h == downWin)
            {
                if (h == 0 && MinClicked != null) MinClicked(this, EventArgs.Empty);
                if (h == 1 && MaxClicked != null) MaxClicked(this, EventArgs.Empty);
                if (h == 2 && CloseClicked != null) CloseClicked(this, EventArgs.Empty);
            }
            downWin = -1;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            if (HitWin(PointToClient(Cursor.Position)) < 0 && MaxClicked != null) MaxClicked(this, EventArgs.Empty);
            base.OnDoubleClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle r = ClientRectangle;
            using (LinearGradientBrush b = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(2, Width), Math.Max(2, Height)),
                Color.FromArgb(27, 36, 50), Color.FromArgb(13, 17, 23), LinearGradientMode.Horizontal))
                g.FillRectangle(b, r);

            // 右上角柔光
            int glow = Theme.Px(360);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(Width - glow / 2, -glow / 2, glow, glow);
                using (PathGradientBrush b = new PathGradientBrush(p))
                {
                    b.CenterColor = Theme.Alpha(Theme.Accent, 0.13f);
                    b.SurroundColors = new Color[] { Color.Transparent };
                    g.FillPath(b, p);
                }
            }

            int icon = Theme.Px(38);
            int x = Theme.Px(22), y = (Height - icon) / 2;
            Painter.FillGrad(g, new RectangleF(x, y, icon, icon), Theme.Px(9),
                Color.FromArgb(76, 141, 255), Color.FromArgb(46, 196, 139), LinearGradientMode.ForwardDiagonal);
            float bx = x + icon * 0.24f, bw = icon * 0.13f, baseY = y + icon * 0.72f;
            for (int i = 0; i < 3; i++)
            {
                float h = icon * (0.18f + i * 0.13f);
                Painter.Fill(g, new RectangleF(bx + i * icon * 0.20f, baseY - h, bw, h), Theme.Px(2), Color.FromArgb(245, 255, 255, 255));
            }

            int tx = x + icon + Theme.Px(14);
            Painter.Txt(g, Title, Theme.F(17f, FontStyle.Bold),
                new Rectangle(tx, y - Theme.Px(1), Width - tx - Theme.Px(150), Theme.Px(27)), Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (Sub.Length > 0)
                Painter.Txt(g, Sub, Theme.F(9f),
                    new Rectangle(tx + 2, y + Theme.Px(26), Width - tx - Theme.Px(150), Theme.Px(18)), Theme.Muted, Painter.L);

            // 窗口按钮
            Rectangle[] rs = WinRects();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < 3; i++)
            {
                bool hov = (i == hoverWin);
                if (hov)
                {
                    Color bg = (i == 2) ? Color.FromArgb(196, 43, 43) : Theme.Alpha(Color.White, 0.09f);
                    Painter.Fill(g, rs[i], Theme.Px(5), bg);
                }
                Color c = (hov && i == 2) ? Color.White : Theme.Dim;
                float cx = rs[i].Left + rs[i].Width / 2f, cy = rs[i].Top + rs[i].Height / 2f;
                float s = Theme.Px(4.6f);
                using (Pen pen = new Pen(c, Theme.Px(1.25f)))
                {
                    if (i == 0) g.DrawLine(pen, cx - s, cy, cx + s, cy);
                    else if (i == 1) g.DrawRectangle(pen, cx - s, cy - s, s * 2, s * 2);
                    else { g.DrawLine(pen, cx - s, cy - s, cx + s, cy + s); g.DrawLine(pen, cx + s, cy - s, cx - s, cy + s); }
                }
            }

            using (Pen pen = new Pen(Theme.Alpha(Theme.Accent, 0.20f), 1f))
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }

    // ============ 卡片容器（自带标题） ============
    public class CardHost : Control
    {
        public Color Plate = Theme.Bg;
        public Color FillColor = Theme.Card;
        public string Title = "";
        public string Sub = "";

        public CardHost()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Plate);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Painter.Fill(g, r, Theme.Px(12), FillColor);
            Painter.Stroke(g, r, Theme.Px(12), Theme.Border, 1f);
            if (Title.Length > 0)
                Painter.Txt(g, Title, Theme.F(12f, FontStyle.Bold),
                    new Rectangle(Theme.Px(20), Theme.Px(14), Width - Theme.Px(40), Theme.Px(24)), Theme.Text, Painter.L);
            if (Sub.Length > 0)
                Painter.Txt(g, Sub, Theme.F(8.8f),
                    new Rectangle(Theme.Px(20), Theme.Px(38), Width - Theme.Px(40), Theme.Px(20)), Theme.Muted, Painter.L);
        }
    }

    // ============ 单行文字 ============
    public class TextLine : Control
    {
        public new string Text = "";
        public Color Color2 = Theme.Dim;
        public Color Plate = Theme.Card;
        public Font Font2 = null;

        public TextLine()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Plate);
            Painter.Txt(e.Graphics, Text, Font2 != null ? Font2 : Theme.F(9f), ClientRectangle, Color2, Painter.L);
        }
    }

    // ============ 多行文字 ============
    public class TextBlock : Control
    {
        public new string Text = "";
        public Color Color2 = Theme.Muted;
        public Color Plate = Theme.Bg;
        public Font Font2 = null;

        public TextBlock()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Plate);
            TextRenderer.DrawText(e.Graphics, Text, Font2 != null ? Font2 : Theme.F(9f), ClientRectangle, Color2,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
    }

    // ============ 搜索框 ============
    public class SearchField : Control
    {
        public PlaceholderBox Box;
        public Color Plate = Theme.Card;
        string hint = "";
        bool hot = false;

        public SearchField()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Box = new PlaceholderBox();
            Box.BackColor = Theme.Card2;
            Box.ForeColor = Theme.Text;
            Box.Font = Theme.F(9f);
            Box.DimColor = Theme.Muted;
            Box.Visible = false;
            Box.GotFocus += delegate { Box.Visible = true; Invalidate(); };
            Box.LostFocus += delegate
            {
                if (Box.TextLength == 0) { Box.Visible = false; Invalidate(); }
            };
            Box.TextChanged += delegate
            {
                if (Box.Visible == false && Box.TextLength > 0) Box.Visible = true;
                Invalidate();
            };
            Controls.Add(Box);
        }

        public string Placeholder
        {
            set { hint = value; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Box.Visible = true;
            Box.Focus();
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = false; Invalidate(); }

        public string Value { get { return Box.Text; } }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Box == null) return;
            Box.Bounds = new Rectangle(Theme.Px(30), (Height - Box.PreferredHeight) / 2, Width - Theme.Px(40), Box.PreferredHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Plate);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Painter.Fill(g, r, Theme.Px(7), Theme.Card2);
            Painter.Stroke(g, r, Theme.Px(7), hot ? Theme.BorderHot : Theme.Border, 1f);

            // 放大镜
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = Theme.Px(15), cy = Height / 2f - Theme.Px(1), rad = Theme.Px(5);
            using (Pen pen = new Pen(Theme.Muted, Theme.Px(1.3f)))
            {
                g.DrawEllipse(pen, cx - rad, cy - rad, rad * 2, rad * 2);
                g.DrawLine(pen, cx + rad * 0.7f, cy + rad * 0.7f, cx + rad * 1.7f, cy + rad * 1.7f);
            }

            // 占位提示（未聚焦且为空时）
            if (!Box.Visible && hint.Length > 0)
                Painter.Txt(g, hint, Theme.F(9f), new Rectangle(Theme.Px(30), 0, Width - Theme.Px(36), Height), Theme.Muted, Painter.L);
        }
    }

    // ============ 统计小卡片 ============
    public class StatCard : Control
    {
        public string Caption = "";
        public string Value = "";
        public string Sub = "";
        public Color Tint = Theme.Accent;
        public Color Plate = Theme.Card;

        public StatCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Plate);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Painter.Fill(g, r, Theme.Px(10), Theme.Card2);
            Painter.Stroke(g, r, Theme.Px(10), Theme.Border, 1f);
            // 左侧色条
            Painter.Fill(g, new RectangleF(r.X + Theme.Px(1), r.Y + Theme.Px(12), Theme.Px(3), r.Height - Theme.Px(24)),
                Theme.Px(2), Theme.Alpha(Tint, 0.85f));

            int px = Theme.Px(18);
            Painter.Txt(g, Caption, Theme.F(9f), new Rectangle(px, Theme.Px(10), Width - px - Theme.Px(10), Theme.Px(16)), Theme.Muted, Painter.L);
            Painter.Txt(g, Value, Theme.F(15f, FontStyle.Bold), new Rectangle(px - 1, Theme.Px(27), Width - px - Theme.Px(8), Theme.Px(26)), Theme.Text, Painter.L);
            if (Sub.Length > 0)
                Painter.Txt(g, Sub, Theme.F(8.5f), new Rectangle(px, Theme.Px(52), Width - px - Theme.Px(10), Theme.Px(18)), Theme.Dim, Painter.L);
        }
    }

    // ============ 底部状态栏 ============
    public class FooterBar : Control
    {
        public Color TextColor = Theme.Dim;
        public string StatusText = "";
        public string TailText = "";
        public bool Live = false;
        public Color DotColor = Theme.Green;

        public FooterBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cx = Theme.Px(4), cy = Height / 2;
            Painter.Fill(g, new RectangleF(cx, cy - Theme.Px(4), Theme.Px(8), Theme.Px(8)), Theme.Px(4), DotColor);
            Painter.Txt(g, StatusText, Theme.F(9f), new Rectangle(cx + Theme.Px(14), 0, Width - Theme.Px(360), Height), Theme.Dim, Painter.L);
            if (TailText.Length > 0)
                Painter.Txt(g, TailText, Theme.F(9f), new Rectangle(Width - Theme.Px(340), 0, Theme.Px(330), Height), Theme.Muted, Painter.R);
        }
    }

    // ============ 时间线图 ============
    public class TimeLineCtl : Control
    {
        public List<Session> Sessions = new List<Session>();
        public DateTime Day = DateTime.Today;
        public Color Plate = Theme.Card;
        public bool Live = true;
        int hoverIdx = -1;
        RectangleF[] blockRects = new RectangleF[0];

        public TimeLineCtl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Theme.F(9f);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = -1;
            for (int i = 0; i < blockRects.Length; i++)
                if (blockRects[i].Contains(e.X, e.Y)) { idx = i; break; }
            if (idx != hoverIdx) { hoverIdx = idx; Invalidate(); }
            Cursor = idx >= 0 ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hoverIdx = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Plate);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Painter.Fill(g, r, Theme.Px(10), Theme.Card2);
            Painter.Stroke(g, r, Theme.Px(10), Theme.Border, 1f);

            int padL = Theme.Px(14), padR = Theme.Px(14);
            float top = Theme.Px(24);
            float barH = Height - top - Theme.Px(20);
            float x0 = r.X + padL;
            float w = r.Width - padL - padR;
            if (w < 20 || barH < 20) return;

            // 小时刻度
            for (int h = 0; h <= 24; h++)
            {
                float x = x0 + w * h / 24f;
                bool major = (h % 3 == 0);
                using (Pen pen = new Pen(Theme.Alpha(major ? Theme.Border : Color.White, major ? 0.55f : 0.06f), 1f))
                    g.DrawLine(pen, x, top - Theme.Px(6), x, top + barH);
                if (major && h < 24)
                {
                    string lab = ((h < 10) ? "0" : "") + h.ToString() + ":00";
                    Painter.Txt(g, lab, Theme.F(7.5f), new Rectangle((int)(x - Theme.Px(18)), (int)(top - Theme.Px(24)), Theme.Px(36), Theme.Px(16)),
                        Theme.Muted, Painter.C);
                }
            }
            if (!Live)
            {
                Painter.Txt(g, "现在", Theme.F(7.5f), new Rectangle((int)(x0 + w - Theme.Px(20)), (int)(top - Theme.Px(24)), Theme.Px(40), Theme.Px(16)), Theme.Muted, Painter.C);
            }

            // 分布到两条泳道
            Session[] list = Sessions.ToArray();
            float[] laneEnd = new float[2];
            int[][] laneOf = new int[list.Length][];
            RectangleF[] rects = new RectangleF[list.Length];
            int lanesUsed = 1;
            for (int i = 0; i < list.Length; i++)
            {
                double f1 = (list[i].Start - Day).TotalHours / 24.0;
                double f2 = (list[i].End - Day).TotalHours / 24.0;
                if (f1 < 0) f1 = 0;
                if (f2 > 1) f2 = 1;
                float bx1 = x0 + (float)(w * f1);
                float bx2 = x0 + (float)(w * f2);
                float minW = Theme.Px(2.5f);
                if (bx2 - bx1 < minW) bx2 = bx1 + minW;

                int lane = -1;
                for (int L = 0; L < 2; L++) if (laneEnd[L] <= bx1 + 0.6f) { lane = L; break; }
                if (lane < 0) lane = 0;
                laneEnd[lane] = bx2;
                if (lane + 1 > lanesUsed) lanesUsed = lane + 1;
                rects[i] = new RectangleF(bx1, 0, bx2 - bx1, 0);
                laneOf[i] = new int[] { lane };
            }

            float laneH = (barH - Theme.Px(4) * (lanesUsed - 1)) / lanesUsed;
            for (int i = 0; i < list.Length; i++)
            {
                int lane = laneOf[i][0];
                RectangleF b = rects[i];
                b.Y = top + lane * (laneH + Theme.Px(4));
                b.Height = laneH;
                Color c = Theme.AppColor(list[i].App);
                Color fillC = Theme.Alpha(c, 0.42f);
                Color borderC = Theme.Alpha(c, 0.9f);
                Painter.Fill(g, b, Theme.Px(4), fillC);
                Painter.Stroke(g, b, Theme.Px(4), borderC, 1f);
                if (b.Width > Theme.Px(52))
                {
                    Rectangle tr = new Rectangle((int)b.X + Theme.Px(6), (int)b.Y, (int)b.Width - Theme.Px(10), (int)b.Height);
                    string lab = list[i].App;
                    if (b.Width > Theme.Px(120)) lab += "  " + Fmt.DurShort(list[i].Duration);
                    Painter.Txt(g, lab, Theme.F(8f), tr, Theme.Text, Painter.L);
                }
                rects[i] = b;
            }
            blockRects = rects;

            // 现在线
            if (Live)
            {
                DateTime now = DateTime.Now;
                float nx = x0 + w * (float)(now.TimeOfDay.TotalHours / 24.0);
                using (Pen pen = new Pen(Theme.Alpha(Theme.Accent, 0.9f), Theme.Px(1.4f)))
                    g.DrawLine(pen, nx, top - Theme.Px(4), nx, top + barH + Theme.Px(2));
                Painter.Fill(g, new RectangleF(nx - Theme.Px(3), top - Theme.Px(9), Theme.Px(6), Theme.Px(6)), Theme.Px(3), Theme.Accent);
            }

            // 悬停详情
            if (hoverIdx >= 0 && hoverIdx < list.Length)
            {
                Session s = list[hoverIdx];
                int bw = Theme.Px(226), bh = Theme.Px(70);
                float bx = Math.Min(blockRects[hoverIdx].X + Theme.Px(6), Width - bw - Theme.Px(8));
                float by = Math.Max(Theme.Px(4), blockRects[hoverIdx].Y - bh - Theme.Px(6));
                RectangleF box = new RectangleF(bx, by, bw, bh);
                Painter.Fill(g, box, Theme.Px(8), Color.FromArgb(28, 35, 47));
                Painter.Stroke(g, box, Theme.Px(8), Theme.Alpha(Theme.AppColor(s.App), 0.7f), 1f);
                Painter.Txt(g, s.App, Theme.F(9.5f, FontStyle.Bold), new Rectangle((int)box.X + Theme.Px(10), (int)box.Y + Theme.Px(8), bw - Theme.Px(18), Theme.Px(18)), Theme.Text, Painter.L);
                Painter.Txt(g, Fmt.Clock(s.Start) + " — " + Fmt.Clock(s.End) + "   " + Fmt.Dur(s.Duration), Theme.F(8.5f),
                    new Rectangle((int)box.X + Theme.Px(10), (int)box.Y + Theme.Px(28), bw - Theme.Px(18), Theme.Px(16)), Theme.Dim, Painter.L);
                if (s.Title.Length > 0)
                    Painter.Txt(g, s.Title, Theme.F(8f), new Rectangle((int)box.X + Theme.Px(10), (int)box.Y + Theme.Px(46), bw - Theme.Px(18), Theme.Px(16)), Theme.Muted, Painter.L);
            }
        }
    }

    // ============ 带占位符的输入框 ============
    public class PlaceholderBox : TextBox
    {
        public string Placeholder = "";
        public Color DimColor = Theme.Muted;

        public PlaceholderBox()
        {
            BorderStyle = BorderStyle.None;
        }
    }

    // ============ 表格工厂 ============
    public static class Grids
    {
        public static DataGridView Make()
        {
            DataGridView g = new DataGridView();
            g.Dock = DockStyle.None;
            g.BorderStyle = BorderStyle.None;
            g.BackgroundColor = Theme.Card;
            g.GridColor = Color.FromArgb(31, 40, 53);
            g.EnableHeadersVisualStyles = false;
            g.RowHeadersVisible = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.AllowUserToOrderColumns = false;
            g.ReadOnly = true;
            g.MultiSelect = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = Theme.Px(34);
            g.RowTemplate.Height = Theme.Px(33);
            g.ScrollBars = ScrollBars.Vertical;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.Font = Theme.F(9.2f);

            DataGridViewCellStyle hs = new DataGridViewCellStyle();
            hs.BackColor = Theme.Card2;
            hs.ForeColor = Theme.Muted;
            hs.SelectionBackColor = Theme.Card2;
            hs.SelectionForeColor = Theme.Muted;
            hs.Font = Theme.F(9f, FontStyle.Bold);
            hs.Padding = new Padding(Theme.Px(6), 0, Theme.Px(4), 0);
            hs.Alignment = DataGridViewContentAlignment.MiddleLeft;
            g.ColumnHeadersDefaultCellStyle = hs;

            DataGridViewCellStyle cs = new DataGridViewCellStyle();
            cs.BackColor = Theme.Card;
            cs.ForeColor = Theme.Text;
            cs.SelectionBackColor = Color.FromArgb(34, 48, 72);
            cs.SelectionForeColor = Theme.Text;
            cs.Font = Theme.F(9.2f);
            cs.Padding = new Padding(Theme.Px(6), 0, Theme.Px(4), 0);
            cs.WrapMode = DataGridViewTriState.False;
            g.DefaultCellStyle = cs;

            DataGridViewCellStyle alt = new DataGridViewCellStyle();
            alt.BackColor = Color.FromArgb(21, 27, 37);
            g.AlternatingRowsDefaultCellStyle = alt;
            return g;
        }

        public static DataGridViewTextBoxColumn Col(string name, string header, float weight, bool right)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.Name = name;
            c.HeaderText = header;
            c.FillWeight = weight;
            c.SortMode = DataGridViewColumnSortMode.Programmatic;
            c.DefaultCellStyle.Alignment = right ? DataGridViewContentAlignment.MiddleRight : DataGridViewContentAlignment.MiddleLeft;
            c.HeaderCell.Style.Alignment = c.DefaultCellStyle.Alignment;
            return c;
        }
    }
}
