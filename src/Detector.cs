using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace Sf6Rain
{
    /// <summary>One health-bar rectangle in client pixels.</summary>
    public struct BarBox
    {
        public int X0, Y0, X1, Y1;

        public BarBox(int x0, int y0, int x1, int y1)
        {
            X0 = x0; Y0 = y0; X1 = x1; Y1 = y1;
        }

        public int Width { get { return X1 - X0; } }
        public int Height { get { return Y1 - Y0; } }
        public Rectangle Rect { get { return Rectangle.FromLTRB(X0, Y0, X1, Y1); } }
        public bool IsEmpty { get { return X1 - X0 < 6 || Y1 - Y0 < 6; } }
    }

    /// <summary>Everything one frame told us. UI and state machine read only this.</summary>
    public sealed class Detection
    {
        public bool HudFound;
        public bool FromCache;
        public bool BlackFrame;

        public bool SideKnown;
        public string Side = "";           // left / right / manual
        public int WhiteLeft;
        public int WhiteRight;

        public bool HasLeft;
        public bool HasRight;
        public BarBox Left;
        public BarBox Right;

        public double YellowLeft;
        public double YellowRight;
        public bool YellowLeftHit;
        public bool YellowRightHit;

        public bool HasActive;
        public BarBox Active;
        public bool Yellow;
        public bool YellowTooWide;

        public string Info = "";
        public string Note = "";

        public double ActiveFraction
        {
            get
            {
                if (Side == "left") return YellowLeft;
                if (Side == "right") return YellowRight;
                if (Side == "manual") return YellowLeft;
                return 0;
            }
        }
    }

    /// <summary>Side lock plus a short-lived geometry cache, owned by the capture thread.</summary>
    public sealed class DetectorState
    {
        public string Side = "";
        public int SideSeenAt;
        public int OppositeVotes;

        public bool HasCache;
        public int CacheAt;
        public BarBox CachedLeft;
        public BarBox CachedRight;

        public int WarnedAt;
        public string LastWarning = "";
    }

    public static class Detector
    {
        // ------------------------------------------------------------------
        // Public entry point
        // ------------------------------------------------------------------
        public static Detection Analyze(Bitmap frame, Settings cfg, DetectorState st, int now)
        {
            var det = new Detection();
            if (frame == null || frame.Width < 320 || frame.Height < 180)
            {
                det.BlackFrame = true;
                det.Info = "画面尺寸异常";
                return det;
            }

            using (var px = new Pixels(frame))
            {
                det.BlackFrame = IsBlack(px);

                if (cfg.SideMode == "manual" && cfg.ManualBar != null && cfg.ManualBar.Length == 4)
                {
                    AnalyzeManual(px, cfg, det);
                    return det;
                }

                int w = px.Width, h = px.Height;
                double[] lay = cfg.LayoutFor(w, h);
                Band band = FindBand(px);
                BarBox left, right;

                if (band != null)
                {
                    int priorW = (int)Math.Round(lay[0] * h);
                    int fullW = Math.Max(band.WidthL, band.WidthR);
                    string src = "实测";
                    if (fullW < h * 0.45 || fullW > w * 0.55)
                    {
                        fullW = priorW;
                        src = "按高度估算";
                    }
                    else if (Math.Abs(fullW - priorW) <= priorW * 0.08)
                    {
                        fullW = priorW;
                    }
                    int pad = Math.Max(2, (int)(h * 0.003));
                    int top = Math.Max(0, band.Y0 - pad);
                    int bottom = Math.Min(h, band.Y1 + pad);
                    left = new BarBox(band.InnerL - fullW, top, band.InnerL, bottom);
                    right = new BarBox(band.InnerR, top, band.InnerR + fullW, bottom);
                    left = ClampBox(left, w, h);
                    right = ClampBox(right, w, h);

                    st.HasCache = true;
                    st.CacheAt = now;
                    st.CachedLeft = left;
                    st.CachedRight = right;

                    if (src == "实测")
                    {
                        cfg.RememberLayout(w, h, fullW,
                            (band.InnerR - band.InnerL) / 2.0, band.Y0, band.Y1);
                    }

                    det.Info = string.Format(
                        "血条 y={0}-{1} 内缘 {2}/{3} 宽 {4}({5}) 行 {6}",
                        band.Y0, band.Y1, band.InnerL, band.InnerR,
                        fullW, src, band.Rows);
                }
                else if (st.HasCache && now - st.CacheAt <= 900)
                {
                    left = st.CachedLeft;
                    right = st.CachedRight;
                    det.FromCache = true;
                    det.Info = "血条暂时被遮挡，沿用上一帧位置";
                }
                else
                {
                    det.Info = det.BlackFrame ? "全黑画面" : "未找到血条";
                    return det;
                }

                if (left.IsEmpty || right.IsEmpty)
                {
                    det.Info = "血条位置异常";
                    return det;
                }

                det.HudFound = true;
                det.HasLeft = true;
                det.HasRight = true;
                det.Left = left;
                det.Right = right;

                // ---- which side is mine: the white 「我」 sits on the bar near
                // its outer end. Sampling the outer 80% of the bar survives a
                // width estimate that is off by several percent.
                int bandH = left.Height;
                int ty0 = left.Y0 + bandH / 4;
                int ty1 = left.Y1 - bandH / 4;
                int bw = left.Width;
                // A little margin past the computed outer end covers the case
                // where the bar was already damaged on the first frame and the
                // width came from the height estimate rather than measurement.
                int lw = CountWhite(px,
                    left.X0 - (int)(bw * 0.20), left.X0 + (int)(bw * 0.80), ty0, ty1);
                int rw = CountWhite(px,
                    right.X1 - (int)(bw * 0.80), right.X1 + (int)(bw * 0.20), ty0, ty1);
                det.WhiteLeft = lw;
                det.WhiteRight = rw;

                int minWhite = Math.Max(12, (int)(0.00002 * h * h));
                bool strongL = lw >= minWhite && lw >= rw * 2.0;
                bool strongR = rw >= minWhite && rw >= lw * 2.0;

                string forced = cfg.SideMode;
                if (forced == "left" || forced == "right")
                {
                    det.Side = forced;
                    det.SideKnown = true;
                    det.Note = "已强制按" + (forced == "left" ? "左侧" : "右侧") + "血条识别";
                }
                else if (strongL || strongR)
                {
                    string seen = strongL ? "left" : "right";
                    if (st.Side == seen)
                    {
                        st.OppositeVotes = 0;
                    }
                    else if (st.Side.Length == 0)
                    {
                        st.Side = seen;
                        st.OppositeVotes = 0;
                    }
                    else
                    {
                        st.OppositeVotes++;
                        if (st.OppositeVotes >= 3)
                        {
                            st.Side = seen;
                            st.OppositeVotes = 0;
                        }
                    }
                    st.SideSeenAt = now;
                }

                if (forced != "left" && forced != "right")
                {
                    if (st.Side.Length > 0 && now - st.SideSeenAt <= 1500)
                    {
                        det.Side = st.Side;
                        det.SideKnown = true;
                        if (!strongL && !strongR)
                            det.Note = "「我」字暂时不可见，沿用上次方位";
                    }
                    else
                    {
                        st.Side = "";
                        det.Side = "";
                        det.SideKnown = false;
                        det.Note = "没认出「我」字，先不触发";
                    }
                }

                // ---- yellow on both bars, for the preview, then on mine.
                bool tooWideL, tooWideR;
                bool hitL, hitR;
                det.YellowLeft = MeasureYellow(px, left, true, false, cfg.YellowMaxFraction,
                    out tooWideL, out hitL);
                det.YellowRight = MeasureYellow(px, right, false, false, cfg.YellowMaxFraction,
                    out tooWideR, out hitR);
                det.YellowLeftHit = hitL;
                det.YellowRightHit = hitR;

                if (det.SideKnown)
                {
                    if (det.Side == "left")
                    {
                        det.Active = left;
                        det.Yellow = hitL;
                        det.YellowTooWide = tooWideL;
                    }
                    else if (det.Side == "right")
                    {
                        det.Active = right;
                        det.Yellow = hitR;
                        det.YellowTooWide = tooWideR;
                    }
                    det.HasActive = true;
                }

                if (det.YellowTooWide)
                {
                    det.Yellow = false;
                    if (st.LastWarning != "wide" || now - st.WarnedAt > 10000)
                    {
                        st.LastWarning = "wide";
                        st.WarnedAt = now;
                        det.Note = "黄色占了血条大半，像是自定义 HUD，已忽略";
                    }
                }
                return det;
            }
        }

        // ------------------------------------------------------------------
        // Manual rectangle: the user told us exactly where their bar is, so
        // there is no 「我」 label and no inner-edge anchor to rely on.
        // ------------------------------------------------------------------
        static void AnalyzeManual(Pixels px, Settings cfg, Detection det)
        {
            int w = px.Width, h = px.Height;
            int x0 = (int)Math.Round(cfg.ManualBar[0] * w);
            int y0 = (int)Math.Round(cfg.ManualBar[1] * h);
            int x1 = (int)Math.Round((cfg.ManualBar[0] + cfg.ManualBar[2]) * w);
            int y1 = (int)Math.Round((cfg.ManualBar[1] + cfg.ManualBar[3]) * h);
            var box = ClampBox(new BarBox(x0, y0, x1, y1), w, h);
            det.Side = "manual";
            det.SideKnown = true;
            det.Note = "手动框选";
            det.Info = string.Format("手动框 {0},{1} {2}x{3}", box.X0, box.Y0, box.Width, box.Height);
            if (box.IsEmpty || box.Width < 24 || box.Height < 6)
            {
                det.Note = "框选区域太小，请重新框选";
                return;
            }
            det.HudFound = true;
            det.HasActive = true;
            det.Active = box;
            det.HasLeft = true;
            det.Left = box;
            bool tooWide, hit;
            det.YellowLeft = MeasureYellow(px, box, true, true, cfg.YellowMaxFraction,
                out tooWide, out hit);
            det.Yellow = hit;
            det.YellowLeftHit = hit;
            det.YellowTooWide = tooWide;
            if (tooWide)
            {
                det.Yellow = false;
                det.Note = "黄色占了血条大半，请把框收紧到我方血条";
            }
        }

        // ------------------------------------------------------------------
        // HUD locator. Health drains from the outside in, so the coloured fill
        // always ends at the inner edge nearest the centre; that edge is
        // symmetric and survives a heavily damaged bar.
        // ------------------------------------------------------------------
        sealed class Band
        {
            public int Y0, Y1, InnerL, InnerR, WidthL, WidthR, Rows;
        }

        static Band FindBand(Pixels px)
        {
            int w = px.Width, h = px.Height;
            int yStart = (int)(h * 0.035);
            int yEnd = (int)(h * 0.24);
            int ystep = Math.Max(2, h / 400);
            int skip = (int)(w * 0.035);
            int minRun = (int)(w * 0.04);
            var rows = new List<int[]>();

            for (int y = yStart; y < yEnd; y += ystep)
            {
                int il = -1, ir = -1;
                for (int x = w / 2 - skip; x > w / 3; x--)
                {
                    int k = Kind(px, x, y);
                    if (k != 0 && k != 4)
                    {
                        int run = 0;
                        for (int xx = x; xx > x - minRun && xx >= 0; xx--)
                        {
                            int kk = Kind(px, xx, y);
                            if (kk == 0 || kk == 4) break;
                            run++;
                        }
                        if (run >= minRun) { il = x; break; }
                    }
                }
                for (int x = w / 2 + skip; x < w * 2 / 3; x++)
                {
                    int k = Kind(px, x, y);
                    if (k != 0 && k != 4)
                    {
                        int run = 0;
                        for (int xx = x; xx < x + minRun && xx < w; xx++)
                        {
                            int kk = Kind(px, xx, y);
                            if (kk == 0 || kk == 4) break;
                            run++;
                        }
                        if (run >= minRun) { ir = x; break; }
                    }
                }
                if (il > 0 && ir > 0 && Math.Abs((w - ir) - il) <= w * 0.02)
                    rows.Add(new[] { y, il, ir });
            }

            if (rows.Count < 2) return null;

            int bestStart = 0, bestLen = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                int j = i;
                while (j + 1 < rows.Count && rows[j + 1][0] - rows[j][0] <= ystep * 2) j++;
                if (j - i + 1 > bestLen)
                {
                    bestLen = j - i + 1;
                    bestStart = i;
                }
            }

            int y0 = int.MaxValue, y1 = int.MinValue;
            var ils = new List<int>();
            var irs = new List<int>();
            for (int i = bestStart; i < bestStart + bestLen; i++)
            {
                int[] r = rows[i];
                if (r[0] < y0) y0 = r[0];
                if (r[0] > y1) y1 = r[0];
                ils.Add(r[1]);
                irs.Add(r[2]);
            }
            ils.Sort();
            irs.Sort();
            int innerL = ils[ils.Count / 2];
            int innerR = irs[irs.Count / 2];
            int gap = ((w / 2 - innerL) + (innerR - w / 2)) / 2;
            innerL = w / 2 - gap;
            innerR = w / 2 + gap;

            int midY = (y0 + y1) / 2;
            int wl = MeasureWidth(px, midY, innerL, -1);
            int wr = MeasureWidth(px, midY, innerR, +1);

            var band = new Band();
            band.Y0 = y0; band.Y1 = y1;
            band.InnerL = innerL; band.InnerR = innerR;
            band.WidthL = wl; band.WidthR = wr;
            band.Rows = bestLen;
            return band;
        }

        static int MeasureWidth(Pixels px, int y, int from, int dir)
        {
            int w = px.Width;
            int gapTol = Math.Max(4, (int)(w * 0.008));
            int lastHit = from, start = from;
            for (int x = from; x >= 0 && x < w; x += dir)
            {
                int k = Kind(px, x, y);
                if (k != 0 && k != 4) lastHit = x;
                else if (Math.Abs(x - lastHit) > gapTol) break;
            }
            return Math.Abs(lastHit - start);
        }

        // Longest yellow run in each interior row, anchored at the inner edge
        // (or either edge for a manual box). Yellow scenery outside the fill
        // cannot satisfy the anchor, and a wholly yellow custom HUD trips the
        // fraction guard instead of firing.
        static double MeasureYellow(Pixels px, BarBox b, bool leftSide, bool manual,
            double maxFraction, out bool tooWide, out bool hit)
        {
            tooWide = false;
            hit = false;
            int bw = b.Width;
            if (bw < 8) return 0;
            int y0 = b.Y0 + (int)(b.Height * 0.30);
            int y1 = b.Y1 - (int)(b.Height * 0.30);
            if (y1 <= y0) { y0 = b.Y0; y1 = b.Y1 - 1; }
            int anchorTol = (int)(bw * 0.06);
            int minRun = Math.Max(3, (int)(bw * 0.02));
            int rows = 0, hits = 0;
            var lens = new List<int>();

            for (int y = y0; y <= y1; y++)
            {
                rows++;
                int bestStart = -1, bestEnd = -1, bestLen = 0;
                int start = -1, last = -1;
                for (int x = b.X0; x <= b.X1; x++)
                {
                    int r, g, bl;
                    px.Rgb(x, y, out r, out g, out bl);
                    bool yel = IsYellow(r, g, bl);
                    if (yel)
                    {
                        if (start < 0) start = x;
                        last = x;
                    }
                    else if (start >= 0 && x - last > 3)
                    {
                        if (last - start > bestLen)
                        {
                            bestLen = last - start;
                            bestStart = start;
                            bestEnd = last;
                        }
                        start = -1;
                    }
                }
                if (start >= 0 && last - start > bestLen)
                {
                    bestLen = last - start;
                    bestStart = start;
                    bestEnd = last;
                }
                bool anchor;
                if (manual)
                    anchor = bestStart <= b.X0 + anchorTol || bestEnd >= b.X1 - anchorTol;
                else if (leftSide)
                    anchor = bestEnd >= b.X1 - anchorTol;
                else
                    anchor = bestStart <= b.X0 + anchorTol;
                if (bestLen >= minRun && anchor)
                {
                    hits++;
                    lens.Add(bestLen);
                }
            }

            if (lens.Count == 0) return 0;
            lens.Sort();
            double fraction = lens[lens.Count / 2] / (double)bw;
            double ratio = rows == 0 ? 0 : hits / (double)rows;
            hit = ratio >= 0.55 && fraction >= 0.02;
            tooWide = ratio >= 0.55 && fraction > maxFraction;
            if (tooWide) hit = false;
            return fraction;
        }

        static int CountWhite(Pixels px, int x0, int x1, int y0, int y1)
        {
            x0 = Math.Max(0, x0);
            y0 = Math.Max(0, y0);
            x1 = Math.Min(px.Width - 1, x1);
            y1 = Math.Min(px.Height - 1, y1);
            int n = 0;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int r, g, b;
                    px.Rgb(x, y, out r, out g, out b);
                    int mx = Math.Max(r, Math.Max(g, b));
                    int mn = Math.Min(r, Math.Min(g, b));
                    // 0.15, not 0.28: the yellow fill's specular highlight is
                    // tinted (S~0.2), and counting it would give the opponent's
                    // low-health bar a bigger "white" score than my 「我」 tag.
                    if (mx >= 205 && (mx - mn) <= 0.15 * mx) n++;
                }
            return n;
        }

        static BarBox ClampBox(BarBox b, int w, int h)
        {
            int x0 = Math.Max(0, Math.Min(w - 1, b.X0));
            int x1 = Math.Max(0, Math.Min(w, b.X1));
            int y0 = Math.Max(0, Math.Min(h - 1, b.Y0));
            int y1 = Math.Max(0, Math.Min(h, b.Y1));
            return new BarBox(x0, y0, x1, y1);
        }

        static bool IsBlack(Pixels px)
        {
            int step = Math.Max(8, px.Width / 80);
            int total = 0, dark = 0;
            for (int y = 0; y < px.Height; y += step)
                for (int x = 0; x < px.Width; x += step)
                {
                    int r, g, b;
                    px.Rgb(x, y, out r, out g, out b);
                    total++;
                    if (r < 10 && g < 10 && b < 10) dark++;
                }
            return total > 0 && dark >= total * 0.985;
        }

        // ------------------------------------------------------------------
        // Colour classes measured from real SF6 frames (see 使用说明).
        // ------------------------------------------------------------------
        static bool IsYellow(int r, int g, int b)
        {
            double h, s, v;
            Hsv(r, g, b, out h, out s, out v);
            return h >= 38 && h <= 78 && s >= 0.30 && v >= 0.52;
        }

        static int Kind(Pixels px, int x, int y)
        {
            int r, g, b;
            px.Rgb(x, y, out r, out g, out b);
            return Kind(r, g, b);
        }

        static int Kind(int r, int g, int b)
        {
            double h, s, v;
            Hsv(r, g, b, out h, out s, out v);
            if (v < 0.24 || s < 0.38) return 0;
            if (h >= 38 && h <= 76 && v >= 0.55) return 3;    // yellow
            if ((h >= 282 || h <= 14) && s >= 0.45) return 1;  // magenta / red
            if (h >= 192 && h <= 244 && s >= 0.45) return 2;   // blue
            if (h > 76 && h < 170) return 4;                   // green (drive gauge)
            return 0;
        }

        static void Hsv(int r, int g, int b, out double h, out double s, out double v)
        {
            int mx = Math.Max(r, Math.Max(g, b));
            int mn = Math.Min(r, Math.Min(g, b));
            int d = mx - mn;
            v = mx / 255.0;
            s = mx == 0 ? 0 : d / (double)mx;
            if (d == 0) { h = 0; return; }
            double hh;
            if (mx == r) hh = ((g - b) / (double)d) % 6.0;
            else if (mx == g) hh = (b - r) / (double)d + 2.0;
            else hh = (r - g) / (double)d + 4.0;
            h = hh * 60.0;
            if (h < 0) h += 360;
        }

        // ------------------------------------------------------------------
        // LockBits once per frame instead of GetPixel tens of thousands of times.
        // ------------------------------------------------------------------
        sealed unsafe class Pixels : IDisposable
        {
            Bitmap _source;
            bool _owns;
            BitmapData _data;
            byte* _base;
            public readonly int Width, Height, Stride;

            public Pixels(Bitmap bmp)
            {
                _source = bmp;
                if (bmp.PixelFormat != PixelFormat.Format32bppArgb)
                {
                    _source = bmp.Clone(new Rectangle(0, 0, bmp.Width, bmp.Height),
                        PixelFormat.Format32bppArgb);
                    _owns = true;
                }
                Width = _source.Width;
                Height = _source.Height;
                _data = _source.LockBits(new Rectangle(0, 0, Width, Height),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                Stride = _data.Stride;
                _base = (byte*)_data.Scan0;
            }

            public void Rgb(int x, int y, out int r, out int g, out int b)
            {
                byte* p = _base + (long)y * Stride + (long)x * 4;
                b = p[0];
                g = p[1];
                r = p[2];
            }

            public void Dispose()
            {
                if (_source != null && _data.Scan0 != IntPtr.Zero)
                    _source.UnlockBits(_data);
                _data = new BitmapData();
                if (_owns && _source != null) _source.Dispose();
                _source = null;
            }
        }
    }
}
