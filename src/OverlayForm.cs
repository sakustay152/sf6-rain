using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Sf6Rain
{
    /// <summary>A 360×46 click-through card over the game, only while the rain plays.</summary>
    public class OverlayForm : Form
    {
        readonly Timer _pulse;
        readonly float _k;
        string _track = "";
        int _phase;

        public OverlayForm()
        {
            // Point-sized fonts grow with the monitor DPI; the card must grow
            // with them or the title clips. WinForms auto-scaling is unreliable
            // at 150%, so the geometry is scaled by hand.
            _k = WinApi.SystemDpi() / 96f;
            if (_k < 1f) _k = 1f;
            if (_k > 3f) _k = 3f;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(Sc(360), Sc(46));
            BackColor = Color.FromArgb(16, 21, 33);
            DoubleBuffered = true;
            _pulse = new Timer();
            _pulse.Interval = 90;
            _pulse.Tick += delegate
            {
                _phase = (_phase + 1) % 26;
                Invalidate();
            };
            UpdateRegion();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }

        int Sc(int v) { return (int)Math.Round(v * _k); }

        void UpdateRegion()
        {
            using (GraphicsPath path = Theme.Round(new Rectangle(0, 0, Width, Height), Sc(12)))
                Region = new Region(path);
        }

        public void ShowFor(IntPtr gameWindow, string trackName)
        {
            _track = trackName == null ? "" : trackName;
            Rectangle client = WinApi.ClientOnScreen(gameWindow);
            Screen screen = gameWindow != IntPtr.Zero
                ? Screen.FromHandle(gameWindow) : Screen.PrimaryScreen;
            Rectangle work = screen.WorkingArea;
            int x, y;
            if (client.IsEmpty)
            {
                x = work.X + (work.Width - Width) / 2;
                y = work.Y + 54;
            }
            else
            {
                x = client.X + (client.Width - Width) / 2;
                y = client.Y + Math.Max(Sc(40), (int)(client.Height * 0.045));
            }
            if (x < work.X) x = work.X;
            if (x + Width > work.Right) x = work.Right - Width;
            if (y < work.Y) y = work.Y;
            Location = new Point(x, y);
            if (!Visible) Show();
            _pulse.Start();
            Invalidate();
        }

        public void HideOverlay()
        {
            _pulse.Stop();
            if (Visible) Hide();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.Round(r, Sc(12)))
            using (var b = new SolidBrush(Color.FromArgb(244, 16, 21, 33)))
                g.FillPath(b, path);
            using (GraphicsPath path = Theme.Round(r, Sc(12)))
            using (var pen = new Pen(Color.FromArgb(110, Theme.Yellow), 1f))
                g.DrawPath(pen, path);

            // Tiny yellow HP bar with rain strokes.
            using (var b = new SolidBrush(Color.FromArgb(40, Theme.Yellow)))
                g.FillRectangle(b, Sc(12), Sc(11), Sc(30), Sc(24));
            var bar = new Point[]
            {
                new Point(Sc(16), Sc(24)), new Point(Sc(38), Sc(18)),
                new Point(Sc(38), Sc(23)), new Point(Sc(16), Sc(29))
            };
            using (var b = new SolidBrush(Theme.Yellow))
                g.FillPolygon(b, bar);
            using (var pen = new Pen(Color.FromArgb(140, Theme.YellowSoft), 1f))
            {
                g.DrawLine(pen, Sc(19), Sc(8), Sc(17), Sc(14));
                g.DrawLine(pen, Sc(25), Sc(7), Sc(23), Sc(13));
                g.DrawLine(pen, Sc(31), Sc(8), Sc(29), Sc(14));
            }

            Theme.Str(g, "雨正在下", Theme.F(9.5f, FontStyle.Bold), Theme.Yellow,
                new Rectangle(Sc(54), Sc(6), Sc(220), Sc(19)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            string sub = "我方血量见黄" + (_track.Length > 0 ? " · " + _track : "");
            Theme.Str(g, sub, Theme.F(7.5f), Theme.Muted,
                new Rectangle(Sc(54), Sc(24), Sc(270), Sc(16)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            double wave = Math.Sin(_phase / 26.0 * Math.PI * 2);
            int alpha = 110 + (int)(80 * (wave + 1) / 2);
            using (var b = new SolidBrush(Color.FromArgb(alpha, Theme.Yellow)))
                g.FillEllipse(b, Width - Sc(24), Height / 2 - Sc(4), Sc(8), Sc(8));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRegion();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }
    }
}
