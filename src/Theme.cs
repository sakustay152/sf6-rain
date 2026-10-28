using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace Sf6Rain
{
    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(10, 13, 20);
        public static readonly Color BgRaised = Color.FromArgb(14, 18, 27);
        public static readonly Color Panel = Color.FromArgb(19, 24, 37);
        public static readonly Color PanelAlt = Color.FromArgb(27, 34, 51);
        public static readonly Color Input = Color.FromArgb(12, 16, 25);
        public static readonly Color Border = Color.FromArgb(40, 50, 72);
        public static readonly Color BorderSoft = Color.FromArgb(31, 39, 57);
        public static readonly Color Text = Color.FromArgb(232, 236, 246);
        public static readonly Color Muted = Color.FromArgb(139, 150, 173);
        public static readonly Color Faint = Color.FromArgb(96, 107, 130);
        public static readonly Color Yellow = Color.FromArgb(245, 211, 74);
        public static readonly Color YellowSoft = Color.FromArgb(255, 235, 135);
        public static readonly Color Green = Color.FromArgb(62, 200, 142);
        public static readonly Color Red = Color.FromArgb(229, 86, 74);
        public static readonly Color Blue = Color.FromArgb(72, 140, 235);
        public static readonly Color Magenta = Color.FromArgb(198, 48, 108);

        static string _family;
        static readonly Dictionary<string, Font> Fonts = new Dictionary<string, Font>();

        public static string Family
        {
            get
            {
                if (_family != null) return _family;
                string[] preferred = { "Microsoft YaHei UI", "Microsoft YaHei", "PingFang SC", "Segoe UI" };
                foreach (string name in preferred)
                {
                    try
                    {
                        using (var test = new FontFamily(name)) { }
                        _family = name;
                        return _family;
                    }
                    catch { }
                }
                _family = System.Drawing.FontFamily.GenericSansSerif.Name;
                return _family;
            }
        }

        public static Font F(float size)
        {
            return F(size, FontStyle.Regular);
        }

        public static Font F(float size, FontStyle style)
        {
            string key = size.ToString("0.0") + "/" + (int)style;
            Font f;
            if (Fonts.TryGetValue(key, out f)) return f;
            f = new Font(Family, size, style, GraphicsUnit.Point);
            Fonts[key] = f;
            return f;
        }

        public static Color Blend(Color a, Color b, double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static Color Shift(Color c, int delta)
        {
            int r = c.R + delta, g = c.G + delta, b = c.B + delta;
            return Color.FromArgb(
                r < 0 ? 0 : r > 255 ? 255 : r,
                g < 0 ? 0 : g > 255 ? 255 : g,
                b < 0 ? 0 : b > 255 ? 255 : b);
        }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            if (r.Width <= 0 || r.Height <= 0) return path;
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 1)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color color)
        {
            using (GraphicsPath p = Round(r, radius))
            using (var b = new SolidBrush(color))
                g.FillPath(b, p);
        }

        public static void DrawRound(Graphics g, Rectangle r, int radius, Color color, float width)
        {
            using (GraphicsPath p = Round(r, radius))
            using (var pen = new Pen(color, width))
                g.DrawPath(pen, p);
        }

        public static void Str(Graphics g, string text, Font font, Color color, Rectangle r,
            TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, text == null ? "" : text, font, r, color,
                flags | TextFormatFlags.NoPrefix);
        }

        public static void Label(Graphics g, string text, Font font, Color color, Rectangle r,
            TextFormatFlags flags)
        {
            Str(g, text, font, color, r, flags);
        }

        public static void StyleCombo(ComboBox combo)
        {
            combo.FlatStyle = FlatStyle.Flat;
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.BackColor = PanelAlt;
            combo.ForeColor = Text;
            combo.Font = F(9f);
            combo.ItemHeight = 30;
            combo.DropDownHeight = 300;
            combo.DrawItem += ComboDraw;
        }

        static void ComboDraw(object sender, DrawItemEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo == null) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color back = selected ? Color.FromArgb(55, 66, 94) : PanelAlt;
            using (var b = new SolidBrush(back))
                e.Graphics.FillRectangle(b, e.Bounds);
            if (e.Index < 0) return;
            string text = combo.GetItemText(combo.Items[e.Index]);
            Rectangle r = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
            Str(e.Graphics, text, combo.Font, selected ? Text : Text, r,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        public static void StyleNumeric(NumericUpDown box)
        {
            box.BackColor = Input;
            box.ForeColor = Text;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Font = F(9.5f);
            box.TextAlign = HorizontalAlignment.Right;
            box.ThousandsSeparator = false;
        }
    }

    /// <summary>Rounded plate. Its plate is painted in OnPaintBackground so
    /// transparent children can reuse it.</summary>
    public class Card : Panel
    {
        public string Title = "";
        public string Hint = "";
        public int Radius = 12;
        public Color Accent = Theme.Yellow;

        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(1, 1, Width - 2, Height - 2);
            Theme.FillRound(g, r, Radius, Theme.Panel);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Theme.DrawRound(g, new Rectangle(1, 1, Width - 3, Height - 3), Radius, Theme.Border, 1f);
            if (Title.Length > 0)
            {
                using (var b = new SolidBrush(Accent))
                    g.FillRectangle(b, 15, 15, 3, 12);
                Theme.Str(g, Title, Theme.F(9.5f, FontStyle.Bold), Theme.Text,
                    new Rectangle(25, 9, Width - 200, 24),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
            if (Hint.Length > 0)
            {
                Theme.Str(g, Hint, Theme.F(8f), Theme.Faint,
                    new Rectangle(Width - 260, 10, 246, 22),
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }

    public class FlatButton : Control
    {
        public bool Primary;
        public bool Danger;
        public bool Quiet;
        public Color AccentColor = Theme.Yellow;
        public int Radius = 8;
        bool _hover, _down;

        public FlatButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Panel;
            Font = Theme.F(9.5f, FontStyle.Bold);
            Cursor = Cursors.Hand;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var b = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(b, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill, border, text;
            if (Primary)
            {
                fill = Danger ? Theme.Red : AccentColor;
                if (_down) fill = Theme.Shift(fill, -22);
                else if (_hover) fill = Theme.Shift(fill, 16);
                border = Theme.Shift(fill, 24);
                text = Color.FromArgb(18, 21, 30);
            }
            else if (Quiet)
            {
                fill = _hover ? Theme.PanelAlt : BackColor;
                border = Color.Transparent;
                text = _hover ? Theme.Text : Theme.Muted;
            }
            else
            {
                fill = _down ? Theme.Panel : (_hover ? Theme.PanelAlt : Color.FromArgb(24, 30, 44));
                border = _hover ? Theme.Border : Theme.BorderSoft;
                text = Theme.Text;
            }
            if (!Enabled)
            {
                fill = Theme.Blend(fill, BackColor, 0.55);
                border = Theme.Blend(border, BackColor, 0.55);
                text = Theme.Faint;
            }
            Theme.FillRound(g, r, Radius, fill);
            if (border != Color.Transparent)
                Theme.DrawRound(g, r, Radius, border, 1f);
            Theme.Str(g, Text, Font, text, r,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    }

    public class NavButton : Control
    {
        public bool Active;
        bool _hover;

        public NavButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Font = Theme.F(9.5f, FontStyle.Bold);
            Cursor = Cursors.Hand;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var b = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(b, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (Active)
            {
                Theme.FillRound(g, r, 8, Color.FromArgb(26, 32, 48));
                Theme.DrawRound(g, r, 8, Theme.BorderSoft, 1f);
                using (var b = new SolidBrush(Theme.Yellow))
                    g.FillRectangle(b, 16, Height - 2, Width - 32, 2);
            }
            else if (_hover)
            {
                Theme.FillRound(g, r, 8, Color.FromArgb(20, 25, 38));
            }
            Theme.Str(g, Text, Font, Active ? Theme.Yellow : (_hover ? Theme.Text : Theme.Muted), r,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    }

    public class Toggle : Control
    {
        bool _checked;
        bool _hover;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                var h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public Toggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Panel;
            Font = Theme.F(9f);
            Cursor = Cursors.Hand;
            Height = 30;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var b = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(b, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color textColor = _hover ? Theme.Text : Theme.Blend(Theme.Text, Theme.Muted, 0.25);
            int sw = Math.Max(36, (Height * 42) / 30);
            int sh = Math.Max(18, (Height * 22) / 30);
            Theme.Str(g, Text, Font, textColor,
                new Rectangle(0, 0, Width - sw - 16, Height),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            int sx = Width - sw - 2, sy = (Height - sh) / 2;
            Rectangle track = new Rectangle(sx, sy, sw, sh);
            Color trackFill = _checked
                ? (_hover ? Theme.Shift(Theme.Yellow, 10) : Theme.Yellow)
                : Color.FromArgb(38, 47, 67);
            Theme.FillRound(g, track, sh / 2, trackFill);
            if (!_checked) Theme.DrawRound(g, track, sh / 2, Theme.Border, 1f);
            int knob = sh - 6;
            int kx = _checked ? (sx + sw - knob - 3) : (sx + 3);
            using (var b = new SolidBrush(_checked ? Color.FromArgb(24, 27, 36) : Theme.Muted))
                g.FillEllipse(b, kx, sy + 3, knob, knob);
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    }

    public class SlimSlider : Control
    {
        double _value;
        bool _drag;
        bool _hover;

        public event EventHandler ValueChanged;

        public bool ShowValue = true;
        public string Suffix = "%";

        public double Value
        {
            get { return _value; }
            set
            {
                double v = value;
                if (v < 0) v = 0;
                if (v > 1) v = 1;
                if (Math.Abs(v - _value) < 0.0001) return;
                _value = v;
                Invalidate();
                var h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public SlimSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Panel;
            Font = Theme.F(8.5f, FontStyle.Bold);
            Height = 26;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var b = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(b, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int right = ShowValue ? 48 : 8;
            int trackW = Width - right - 6;
            int cy = Height / 2;
            Rectangle track = new Rectangle(3, cy - 2, trackW, 5);
            Theme.FillRound(g, track, 3, Color.FromArgb(38, 47, 67));
            int fillW = (int)(trackW * _value);
            if (fillW > 2)
                Theme.FillRound(g, new Rectangle(3, cy - 2, fillW, 5), 3, Theme.Yellow);
            int kx = 3 + (int)(trackW * _value);
            using (var b = new SolidBrush(_hover || _drag ? Theme.YellowSoft : Theme.Yellow))
                g.FillEllipse(b, kx - 7, cy - 7, 14, 14);
            if (ShowValue)
            {
                string txt = Math.Round(_value * 100) + Suffix;
                Theme.Str(g, txt, Font, Theme.Text,
                    new Rectangle(Width - right + 2, 0, right + 4, Height),
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        }

        void SetFromX(int x)
        {
            int right = ShowValue ? 48 : 8;
            int trackW = Math.Max(1, Width - right - 6);
            double v = (x - 3) / (double)trackW;
            Value = v;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { _drag = true; SetFromX(e.X); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_drag) SetFromX(e.X);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _drag = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; if (!_drag) Invalidate(); base.OnMouseLeave(e); }
    }

    public class Meter : Control
    {
        double _value;
        public Color AccentColor = Theme.Yellow;

        public double Value
        {
            get { return _value; }
            set
            {
                double v = value;
                if (v < 0) v = 0;
                if (v > 1) v = 1;
                _value = v;
                Invalidate();
            }
        }

        public Meter()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Panel;
            Height = 12;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var b = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(b, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 1, Width - 1, Height - 2);
            Theme.FillRound(g, r, Height / 2, Color.FromArgb(34, 42, 60));
            int w = (int)((Width - 2) * _value);
            if (w > 2)
                Theme.FillRound(g, new Rectangle(1, 1, w, Height - 2), Height / 2, AccentColor);
            using (var pen = new Pen(Color.FromArgb(28, 34, 49), 1f))
            {
                for (int i = 1; i < 10; i++)
                {
                    int x = Width * i / 10;
                    g.DrawLine(pen, x, 2, x, Height - 2);
                }
            }
        }
    }

    public class Pill : Control
    {
        string _text = "";
        Color _color = Theme.Muted;

        public Pill()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Font = Theme.F(8.5f, FontStyle.Bold);
        }

        public void Set(string text, Color color)
        {
            if (_text == text && _color == color) return;
            _text = text == null ? "" : text;
            _color = color;
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var b = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(b, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = Theme.Blend(Theme.Panel, _color, 0.16);
            Theme.FillRound(g, r, Height / 2, fill);
            Theme.DrawRound(g, r, Height / 2, Theme.Blend(Theme.Border, _color, 0.45), 1f);
            using (var b = new SolidBrush(_color))
                g.FillEllipse(b, 12, Height / 2 - 4, 8, 8);
            Theme.Str(g, _text, Font, Theme.Blend(Theme.Text, _color, 0.35),
                new Rectangle(26, 0, Width - 34, Height),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Owner-drawn list with a thin dark scrollbar. Replaces ListBox so
    /// no white native scrollbar shows up in the dark UI.</summary>
    public class DarkList : Control
    {
        public readonly List<object> Items = new List<object>();
        public bool Numbered;
        public bool ShowSeparators;

        int _top;
        int _selected = -1;
        int _hover = -1;
        bool _drag;

        public DarkList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable, true);
            TabStop = false;
            BackColor = Theme.Panel;
            ForeColor = Theme.Text;
            Font = Theme.F(9f);
            Cursor = Cursors.Default;
        }

        public int RowHeight
        {
            get
            {
                int h = (int)Math.Round(Font.GetHeight() + 12);
                return Math.Max(24, h);
            }
        }

        int RowsVisible
        {
            get { return Math.Max(1, Height / RowHeight); }
        }

        int MaxTop
        {
            get { return Math.Max(0, Items.Count - RowsVisible); }
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                int v = value;
                if (v < -1) v = -1;
                if (v >= Items.Count) v = Items.Count - 1;
                if (v == _selected) return;
                _selected = v;
                EnsureVisible(_selected);
                Invalidate();
            }
        }

        public object SelectedItem
        {
            get { return _selected >= 0 && _selected < Items.Count ? Items[_selected] : null; }
        }

        public int TopIndex
        {
            get { return _top; }
            set
            {
                int v = value;
                if (v < 0) v = 0;
                if (v > MaxTop) v = MaxTop;
                if (v == _top) return;
                _top = v;
                Invalidate();
            }
        }

        public void BeginUpdate() { }
        public void EndUpdate() { Invalidate(); }

        public void Add(object item)
        {
            Items.Add(item);
            if (Items.Count - RowsVisible < _top) _top = 0;
            Invalidate();
        }

        public void Clear()
        {
            Items.Clear();
            _selected = -1;
            _top = 0;
            Invalidate();
        }

        void EnsureVisible(int index)
        {
            if (index < 0) return;
            if (index < _top) _top = index;
            else if (index >= _top + RowsVisible) _top = index - RowsVisible + 1;
            if (_top < 0) _top = 0;
            if (_top > MaxTop) _top = MaxTop;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var b = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(b, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int rh = RowHeight;
            int y = 0;
            for (int i = _top; i < Items.Count && y < Height; i++, y += rh)
            {
                DrawRow(g, i, new Rectangle(0, y, Width, Math.Min(rh, Height - y)));
            }
            DrawScrollbar(g);
        }

        void DrawRow(Graphics g, int index, Rectangle r)
        {
            bool selected = index == _selected;
            bool hover = index == _hover && !selected;
            Color back = selected ? Color.FromArgb(35, 44, 64)
                : (hover ? Color.FromArgb(25, 32, 46) : Theme.Panel);
            using (var b = new SolidBrush(back))
                g.FillRectangle(b, r);
            if (selected)
            {
                using (var b = new SolidBrush(Theme.Yellow))
                    g.FillRectangle(b, r.X, r.Y + 5, 3, r.Height - 10);
            }

            int k = Math.Max(1, r.Height / 30);
            Rectangle text = new Rectangle(r.X + 1, r.Y, r.Width - 2, r.Height);
            if (Numbered)
            {
                string num = (index + 1).ToString("00");
                Theme.Str(g, num, Theme.F(8f, FontStyle.Bold), Theme.Faint,
                    new Rectangle(text.X + 10 * k, text.Y, 26 * k, text.Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                text = new Rectangle(text.X + 40 * k, text.Y, text.Width - 46 * k, text.Height);
            }
            string line = Items[index] == null ? "" : Items[index].ToString();
            int split = -1;
            if (!Numbered && line.Length > 14 && line[2] == ':' && line[5] == ':')
                split = line.IndexOf(']');
            if (split > 0 && split + 1 < line.Length)
            {
                string head = line.Substring(0, split + 1);
                string body = line.Substring(split + 1).TrimStart();
                Theme.Str(g, head, Theme.F(8f), Theme.Faint,
                    new Rectangle(text.X + 10 * k, text.Y, 150 * k, text.Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                Theme.Str(g, body, Font, selected ? Theme.Text : Theme.Blend(Theme.Text, Theme.Muted, 0.2),
                    new Rectangle(text.X + 158 * k, text.Y, text.Width - 166 * k, text.Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            else
            {
                Theme.Str(g, line, Font, Theme.Text,
                    new Rectangle(text.X + 14 * k, text.Y, text.Width - 20 * k, text.Height),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            if (ShowSeparators)
                using (var pen = new Pen(Theme.BorderSoft))
                    g.DrawLine(pen, r.X + 12, r.Bottom - 1, r.Right - 12, r.Bottom - 1);
        }

        bool HasBar
        {
            get { return Items.Count > RowsVisible; }
        }

        Rectangle Thumb()
        {
            int trackH = Height - 8;
            int thumbH = Math.Max(24, trackH * RowsVisible / Math.Max(1, Items.Count));
            int maxTop = MaxTop;
            int ty = 4;
            if (maxTop > 0) ty += (trackH - thumbH) * _top / maxTop;
            return new Rectangle(Width - 8, ty, 4, thumbH);
        }

        void DrawScrollbar(Graphics g)
        {
            if (!HasBar) return;
            Theme.FillRound(g, new Rectangle(Width - 8, 4, 4, Height - 8), 2,
                Color.FromArgb(24, 31, 46));
            Theme.FillRound(g, Thumb(), 2,
                _drag || _hover >= 0 ? Theme.Blend(Theme.Muted, Theme.Panel, 0.35)
                    : Theme.Blend(Theme.Faint, Theme.Panel, 0.25));
        }

        int IndexAt(int y)
        {
            if (Items.Count == 0) return -1;
            int i = _top + y / RowHeight;
            return i >= 0 && i < Items.Count ? i : -1;
        }

        void ScrollTo(int y)
        {
            int trackH = Height - 8;
            int thumbH = Math.Max(24, trackH * RowsVisible / Math.Max(1, Items.Count));
            int span = Math.Max(1, trackH - thumbH);
            int maxTop = MaxTop;
            int v = (int)Math.Round((y - 4 - thumbH / 2.0) * maxTop / span);
            TopIndex = v;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            if (e.X >= Width - 12 && HasBar)
            {
                _drag = true;
                ScrollTo(e.Y);
                Invalidate();
                return;
            }
            int i = IndexAt(e.Y);
            if (i >= 0) SelectedIndex = i;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_drag)
            {
                ScrollTo(e.Y);
                return;
            }
            int i = IndexAt(e.Y);
            if (i != _hover)
            {
                _hover = i;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _drag = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hover != -1) { _hover = -1; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (e.Delta > 0) TopIndex = _top - 3;
            else if (e.Delta < 0) TopIndex = _top + 3;
            base.OnMouseWheel(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_top > MaxTop) _top = MaxTop;
        }
    }

    public class LogoBox : Control
    {
        static Image _image;
        static bool _tried;

        public LogoBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        static Image Logo
        {
            get
            {
                if (_tried) return _image;
                _tried = true;
                try
                {
                    using (var stream = Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream("Sf6Rain.logo.png"))
                    {
                        if (stream != null) _image = Image.FromStream(stream);
                    }
                }
                catch { }
                return _image;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var b = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(b, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath clip = Theme.Round(r, 9))
            {
                GraphicsState state = g.Save();
                g.SetClip(clip);
                Image img = Logo;
                if (img != null) g.DrawImage(img, r);
                else
                {
                    using (var b = new SolidBrush(Color.FromArgb(20, 28, 44)))
                        g.FillRectangle(b, r);
                    using (var b = new SolidBrush(Theme.Yellow))
                    {
                        var bar = new Point[]
                        {
                            new Point(7, 22), new Point(31, 13),
                            new Point(31, 21), new Point(7, 30)
                        };
                        g.FillPolygon(b, bar);
                    }
                }
                g.Restore(state);
            }
            Theme.DrawRound(g, r, 9, Theme.Blend(Theme.Border, Theme.Yellow, 0.25), 1f);
        }
    }

    /// <summary>Number field drawn by us: native NumericUpDown does not follow
    /// the dark theme and clips its text at non-96 DPI.</summary>
    public class NumberBox : Control
    {
        double _value;
        double _min;
        double _max = 100;
        double _step = 1;
        int _decimals = 2;
        int _hot;
        bool _focus;

        public event EventHandler ValueChanged;

        public double Minimum
        {
            get { return _min; }
            set { _min = value; if (_value < _min) Value = _min; }
        }

        public double Maximum
        {
            get { return _max; }
            set { _max = value; if (_value > _max) Value = _max; }
        }

        public double Increment
        {
            get { return _step; }
            set { if (value > 0) _step = value; }
        }

        public int DecimalPlaces
        {
            get { return _decimals; }
            set { if (value >= 0 && value <= 6) _decimals = value; Invalidate(); }
        }

        public double Value
        {
            get { return _value; }
            set
            {
                double v = Math.Round(value, _decimals);
                if (v < _min) v = _min;
                if (v > _max) v = _max;
                if (Math.Abs(v - _value) < 0.0000001) return;
                _value = v;
                Invalidate();
                var h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public NumberBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable, true);
            BackColor = Theme.Panel;
            Font = Theme.F(9.5f);
            Height = 30;
            TabStop = true;
            Cursor = Cursors.Hand;
        }

        int ArrowWidth()
        {
            return Math.Max(18, (Height * 55) / 100);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var b = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(b, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, r, 6, Theme.Input);
            Color edge = _focus ? Theme.Yellow : Theme.Border;
            Theme.DrawRound(g, r, 6, edge, _focus ? 1.6f : 1f);

            int aw = ArrowWidth();
            int split = Width - aw;
            using (var p = new Pen(Theme.Border, 1f))
                g.DrawLine(p, split, 5, split, Height - 6);

            string txt = _value.ToString("F" + _decimals);
            Theme.Str(g, txt, Font, Theme.Text,
                new Rectangle(8, 0, split - 14, Height),
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            DrawArrow(g, new Rectangle(split, 0, aw, Height / 2), true);
            DrawArrow(g, new Rectangle(split, Height / 2, aw, Height - Height / 2), false);
        }

        void DrawArrow(Graphics g, Rectangle r, bool up)
        {
            bool hot = up ? _hot > 0 : _hot < 0;
            if (hot)
            {
                using (var b = new SolidBrush(Color.FromArgb(60, Theme.Border)))
                    g.FillRectangle(b, r);
            }
            int s = Math.Max(3, Height / 12);
            int cx = r.Left + r.Width / 2;
            int cy = r.Top + r.Height / 2;
            Point[] p = up
                ? new[] { new Point(cx - s, cy + s / 2), new Point(cx, cy - s / 2), new Point(cx + s, cy + s / 2) }
                : new[] { new Point(cx - s, cy - s / 2), new Point(cx, cy + s / 2), new Point(cx + s, cy - s / 2) };
            using (var b = new SolidBrush(hot ? Theme.Yellow : Theme.Muted))
                g.FillPolygon(b, p);
        }

        int HitArrow(int x, int y)
        {
            if (x < Width - ArrowWidth()) return 0;
            return y < Height / 2 ? 1 : -1;
        }

        void Step(int dir)
        {
            Value = _value + dir * _step;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int hit = HitArrow(e.X, e.Y);
            if (e.Button == MouseButtons.Left && hit != 0) Step(hit);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int hit = HitArrow(e.X, e.Y);
            if (hit != _hot) { _hot = hit; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hot != 0) { _hot = 0; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (Focused) Step(e.Delta > 0 ? 1 : -1);
            base.OnMouseWheel(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up) { Step(1); e.Handled = true; }
            else if (e.KeyCode == Keys.Down) { Step(-1); e.Handled = true; }
            else if (e.KeyCode == Keys.PageUp) { Value = _value + _step * 5; e.Handled = true; }
            else if (e.KeyCode == Keys.PageDown) { Value = _value - _step * 5; e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { _focus = true; Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { _focus = false; Invalidate(); base.OnLostFocus(e); }
    }

    /// <summary>Dark drop-down built from our own control + popup list, so no
    /// native white button appears and long lists still scroll.</summary>
    public class SelectBox : Control
    {
        public readonly List<object> Items = new List<object>();
        int _index = -1;
        bool _focus;
        bool _open;
        SelectPopup _popup;

        public event EventHandler SelectedIndexChanged;

        public SelectBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable, true);
            BackColor = Theme.Bg;
            Font = Theme.F(9f);
            Height = 30;
            TabStop = true;
            Cursor = Cursors.Hand;
        }

        public int Count { get { return Items.Count; } }

        public int SelectedIndex
        {
            get { return _index; }
            set
            {
                int v = value;
                if (v < -1) v = -1;
                if (v >= Items.Count) v = Items.Count - 1;
                if (v == _index) return;
                _index = v;
                Invalidate();
                var h = SelectedIndexChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public object SelectedItem
        {
            get { return _index >= 0 && _index < Items.Count ? Items[_index] : null; }
        }

        public void BeginUpdate() { }
        public void EndUpdate() { Invalidate(); }
        public void Add(object item) { Items.Add(item); Invalidate(); }
        public void Clear() { Items.Clear(); _index = -1; Invalidate(); }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var b = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(b, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, r, 6, Theme.Input);
            Theme.DrawRound(g, r, 6, _focus || _open ? Theme.Yellow : Theme.Border,
                _focus || _open ? 1.6f : 1f);

            object item = SelectedItem;
            string txt = item == null ? "—" : item.ToString();
            Theme.Str(g, txt, Font, item == null ? Theme.Faint : Theme.Text,
                new Rectangle(12, 0, Width - 44, Height),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            int cx = Width - 22;
            int cy = Height / 2;
            int s = Math.Max(3, Height / 10);
            using (var p = new Pen(Theme.Muted, 2f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawLines(p, new[]
                {
                    new Point(cx - s, cy - s / 2),
                    new Point(cx, cy + s / 2),
                    new Point(cx + s, cy - s / 2)
                });
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button == MouseButtons.Left) ShowMenu();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up) SelectedIndex = _index - 1;
            else if (e.KeyCode == Keys.Down) SelectedIndex = _index + 1;
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) ShowMenu();
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { _focus = true; Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { _focus = false; Invalidate(); base.OnLostFocus(e); }

        void ShowMenu()
        {
            if (Items.Count == 0) return;
            if (_popup != null)
            {
                _popup.Close();
                _popup = null;
            }
            _popup = new SelectPopup(this, Items.ToArray(), _index);
            Rectangle screen = Screen.FromControl(this).WorkingArea;
            Point p = PointToScreen(new Point(0, Height + 2));
            int h = _popup.Height;
            if (p.Y + h > screen.Bottom) p.Y = PointToScreen(new Point(0, 0)).Y - h - 2;
            if (p.X + _popup.Width > screen.Right) p.X = screen.Right - _popup.Width;
            if (p.X < screen.Left) p.X = screen.Left;
            if (p.Y < screen.Top) p.Y = screen.Top;
            _popup.Location = p;
            _open = true;
            Invalidate();
            _popup.FormClosed += delegate
            {
                _open = false;
                _popup = null;
                Invalidate();
            };
            _popup.Show();
            _popup.Activate();
        }
    }

    public class SelectPopup : Form
    {
        readonly SelectBox _owner;
        readonly ListBox _list;

        public SelectPopup(SelectBox owner, object[] items, int index)
        {
            _owner = owner;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Border;
            Font = owner.Font;

            int itemH = Math.Max(26, owner.Height - 4);
            int count = Math.Min(items.Length, 12);
            int w = Math.Max(owner.Width, 220);
            int h = Math.Max(itemH + 4, count * itemH + 4);
            ClientSize = new Size(w, h);

            _list = new ListBox();
            _list.BorderStyle = BorderStyle.None;
            _list.DrawMode = DrawMode.OwnerDrawFixed;
            _list.ItemHeight = itemH;
            _list.Font = owner.Font;
            _list.BackColor = Theme.PanelAlt;
            _list.ForeColor = Theme.Text;
            _list.IntegralHeight = false;
            _list.Bounds = new Rectangle(1, 1, w - 2, h - 2);
            _list.Items.AddRange(items);
            if (index >= 0 && index < items.Length) _list.SelectedIndex = index;
            _list.DrawItem += OnDrawItem;
            _list.MouseClick += delegate
            {
                int i = _list.IndexFromPoint(_list.PointToClient(Cursor.Position));
                if (i >= 0) Choose(i);
            };
            _list.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    Choose(_list.SelectedIndex);
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Escape) Close();
            };
            Controls.Add(_list);
            Deactivate += delegate { Close(); };
        }

        void Choose(int index)
        {
            if (index >= 0 && index < _owner.Count) _owner.SelectedIndex = index;
            Close();
        }

        void OnDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            bool current = e.Index == _owner.SelectedIndex;
            using (var b = new SolidBrush(sel ? Theme.Border : Theme.PanelAlt))
                e.Graphics.FillRectangle(b, e.Bounds);
            string txt = _list.Items[e.Index] == null ? "" : _list.Items[e.Index].ToString();
            Color c = current ? Theme.Yellow : (sel ? Theme.Text : Theme.Muted);
            Theme.Str(e.Graphics, (current ? "●  " : "    ") + txt, e.Font, c,
                new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        protected override bool ShowWithoutActivation { get { return false; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                return cp;
            }
        }
    }
} 
