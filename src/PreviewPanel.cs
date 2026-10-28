using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Sf6Rain
{
    public class PreviewPanel : Control
    {
        Bitmap _frame;
        Detection _det;
        bool _hasManual;
        RectangleF _manual;
        bool _selectionMode;
        bool _dragging;
        Point _dragStart;
        RectangleF _draft;

        public event Action<RectangleF> SelectionMade;

        public bool HasFrame { get { return _frame != null; } }

        public bool SelectionMode
        {
            get { return _selectionMode; }
            set
            {
                _selectionMode = value;
                Cursor = value ? Cursors.Cross : Cursors.Default;
                Invalidate();
            }
        }

        public PreviewPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable, true);
            BackColor = Theme.Input;
            TabStop = false;
        }

        public void SetFrame(Bitmap frame, Detection det)
        {
            Bitmap old = _frame;
            _frame = frame;
            _det = det;
            if (old != null && !ReferenceEquals(old, frame)) old.Dispose();
            Invalidate();
        }

        public void ClearFrame()
        {
            if (_frame != null) _frame.Dispose();
            _frame = null;
            _det = null;
            Invalidate();
        }

        public Bitmap FrameClone()
        {
            return _frame == null ? null : (Bitmap)_frame.Clone();
        }

        public void SetManualSelection(RectangleF rect, bool has)
        {
            _manual = rect;
            _hasManual = has;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            Rectangle outer = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.Round(outer, 8))
            using (var b = new SolidBrush(Theme.Input))
                g.FillPath(b, path);

            if (_frame == null)
            {
                DrawPlaceholder(g, outer);
            }
            else
            {
                Rectangle dest = FitRect(_frame.Size, outer);
                g.DrawImage(_frame, dest);
                DrawDetection(g, dest);
                if (_hasManual)
                {
                    RectangleF r = MapNormalized(_manual, dest);
                    using (var pen = new Pen(Theme.Yellow, 1.5f))
                    {
                        pen.DashStyle = DashStyle.Dash;
                        g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                    }
                    DrawTag(g, "手动框选", new Point((int)r.X, (int)r.Y - 20), Theme.Yellow, Theme.Input);
                }
            }

            if (_selectionMode && _frame != null)
            {
                RectangleF draft = _dragging ? _draft : RectangleF.Empty;
                if (_dragging) draft = NormalizedDraft();
                if (draft.Width > 0.002f && draft.Height > 0.002f)
                {
                    RectangleF r = MapNormalized(draft, FitRect(_frame.Size, outer));
                    using (var pen = new Pen(Theme.Yellow, 2f))
                    {
                        pen.DashStyle = DashStyle.Dash;
                        g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                    }
                }
                string hint = "按住左键，把我方血条（含黄色段）框起来";
                Size sz = TextRenderer.MeasureText(hint, Theme.F(8.5f, FontStyle.Bold));
                int bw = sz.Width + 26;
                Rectangle banner = new Rectangle((Width - bw) / 2, 10, bw, 28);
                Theme.FillRound(g, banner, 14, Color.FromArgb(235, 18, 22, 32));
                Theme.DrawRound(g, banner, 14, Theme.Blend(Theme.Border, Theme.Yellow, 0.5), 1f);
                Theme.Str(g, hint, Theme.F(8.5f, FontStyle.Bold), Theme.YellowSoft, banner,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            using (GraphicsPath path = Theme.Round(outer, 8))
            using (var pen = new Pen(_selectionMode ? Theme.Blend(Theme.Border, Theme.Yellow, 0.55) : Theme.BorderSoft, 1f))
                g.DrawPath(pen, path);
        }

        void DrawPlaceholder(Graphics g, Rectangle outer)
        {
            int cy = Height / 2;
            // A small slanted HP bar, like the one this program waits for.
            var pts = new Point[]
            {
                new Point(Width / 2 - 64, cy - 22), new Point(Width / 2 + 64, cy - 30),
                new Point(Width / 2 + 64, cy - 18), new Point(Width / 2 - 64, cy - 10)
            };
            using (var b = new SolidBrush(Color.FromArgb(70, Theme.Yellow)))
                g.FillPolygon(b, pts);
            using (var pen = new Pen(Color.FromArgb(110, Theme.Yellow), 1f))
                g.DrawPolygon(pen, pts);

            Rectangle title = new Rectangle(0, cy + 6, Width, 26);
            Theme.Str(g, "还没有画面", Theme.F(12f, FontStyle.Bold), Theme.Muted, title,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            Rectangle sub = new Rectangle(0, cy + 32, Width, 22);
            Theme.Str(g, "选好游戏窗口，再点右边的「开始监看」", Theme.F(8.5f), Theme.Faint, sub,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            Rectangle foot = new Rectangle(0, outer.Bottom - 30, Width, 20);
            Theme.Str(g, "无边框窗口模式 · 画面保持在前台", Theme.F(8f), Theme.Faint, foot,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        Rectangle FitRect(Size image, Rectangle outer)
        {
            int pad = 4;
            Rectangle avail = new Rectangle(outer.X + pad, outer.Y + pad,
                outer.Width - pad * 2, outer.Height - pad * 2);
            double scale = Math.Min(avail.Width / (double)image.Width, avail.Height / (double)image.Height);
            int w = Math.Max(1, (int)Math.Round(image.Width * scale));
            int h = Math.Max(1, (int)Math.Round(image.Height * scale));
            return new Rectangle(avail.X + (avail.Width - w) / 2, avail.Y + (avail.Height - h) / 2, w, h);
        }

        void DrawDetection(Graphics g, Rectangle dest)
        {
            Detection det = _det;
            if (det == null || !det.HudFound) return;
            bool hasSide = det.SideKnown && (det.Side == "left" || det.Side == "right");
            string mine = det.Side;

            if (det.HasLeft)
            {
                bool active = hasSide && mine == "left";
                DrawBar(g, det.Left, dest, active, det.YellowLeft,
                    active ? "我方" : (hasSide ? "对手" : "左"), active);
            }
            if (det.HasRight)
            {
                bool active = hasSide && mine == "right";
                DrawBar(g, det.Right, dest, active, det.YellowRight,
                    active ? "我方" : (hasSide ? "对手" : "右"), active);
            }
            if (det.Side == "manual" && det.HasActive)
            {
                DrawBar(g, det.Active, dest, true, det.ActiveFraction, "我方", true);
            }
        }

        void DrawBar(Graphics g, BarBox box, Rectangle dest, bool active, double yellow, string label,
            bool showYellow)
        {
            float sx = dest.Width / (float)Math.Max(1, _frame.Width);
            float sy = dest.Height / (float)Math.Max(1, _frame.Height);
            RectangleF r = new RectangleF(dest.X + box.X0 * sx, dest.Y + box.Y0 * sy,
                Math.Max(1, box.Width * sx), Math.Max(1, box.Height * sy));
            Color color = active ? Theme.Yellow : Theme.Blend(Theme.Blue, Theme.Faint, 0.3);
            if (active)
            {
                using (var b = new SolidBrush(Color.FromArgb(34, color)))
                    g.FillRectangle(b, r);
            }
            using (var pen = new Pen(color, active ? 2f : 1.2f))
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);

            if (showYellow && yellow > 0.01)
            {
                string pct = (int)Math.Round(yellow * 100) + "%";
                DrawTag(g, label + " " + pct, new Point((int)r.X, (int)r.Y - 20),
                    active ? Theme.Yellow : color, Theme.Input);
            }
            else
            {
                DrawTag(g, label, new Point((int)r.X, (int)r.Y - 20),
                    active ? Theme.Yellow : color, Theme.Input);
            }
        }

        void DrawTag(Graphics g, string text, Point at, Color color, Color bg)
        {
            Font f = Theme.F(7.5f, FontStyle.Bold);
            Size sz = TextRenderer.MeasureText(text, f);
            Rectangle r = new Rectangle(at.X, at.Y, sz.Width + 12, 19);
            if (r.X < 4) r.X = 4;
            if (r.Y < 4) r.Y = (int)(at.Y + 24);
            if (r.Right > Width - 4) r.X = Width - 4 - r.Width;
            Theme.FillRound(g, r, 5, Color.FromArgb(232, bg));
            Theme.DrawRound(g, r, 5, Theme.Blend(color, bg, 0.55), 1f);
            Theme.Str(g, text, f, color, r,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        RectangleF MapNormalized(RectangleF n, Rectangle dest)
        {
            return new RectangleF(dest.X + n.X * dest.Width, dest.Y + n.Y * dest.Height,
                n.Width * dest.Width, n.Height * dest.Height);
        }

        RectangleF NormalizedDraft()
        {
            if (_frame == null) return RectangleF.Empty;
            Rectangle dest = FitRect(_frame.Size, new Rectangle(0, 0, Width - 1, Height - 1));
            Point a = _dragStart, b = _dragCurrent;
            int x0 = Math.Min(a.X, b.X), y0 = Math.Min(a.Y, b.Y);
            int x1 = Math.Max(a.X, b.X), y1 = Math.Max(a.Y, b.Y);
            float nx0 = (x0 - dest.X) / (float)dest.Width;
            float ny0 = (y0 - dest.Y) / (float)dest.Height;
            float nx1 = (x1 - dest.X) / (float)dest.Width;
            float ny1 = (y1 - dest.Y) / (float)dest.Height;
            nx0 = Math.Max(0f, Math.Min(1f, nx0));
            ny0 = Math.Max(0f, Math.Min(1f, ny0));
            nx1 = Math.Max(0f, Math.Min(1f, nx1));
            ny1 = Math.Max(0f, Math.Min(1f, ny1));
            return new RectangleF(nx0, ny0, nx1 - nx0, ny1 - ny0);
        }

        Point _dragCurrent;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (_selectionMode && _frame != null && e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragStart = e.Location;
                _dragCurrent = e.Location;
                Capture = true;
                Invalidate();
                return;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                _dragCurrent = e.Location;
                _draft = NormalizedDraft();
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dragging)
            {
                _dragging = false;
                Capture = false;
                _dragCurrent = e.Location;
                RectangleF n = NormalizedDraft();
                if (n.Width > 0.01f && n.Height > 0.006f)
                {
                    _manual = n;
                    _hasManual = true;
                    SelectionMode = false;
                    var h = SelectionMade;
                    if (h != null) h(n);
                }
                Invalidate();
                return;
            }
            base.OnMouseUp(e);
        }
    }
}
