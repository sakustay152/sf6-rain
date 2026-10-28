using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Text;

namespace Sf6Rain
{
    public sealed class SelfTestResult
    {
        public bool Pass = true;
        public readonly List<string> Lines = new List<string>();
    }

    /// <summary>
    /// Synthetic HUDs, generated here, checked by the same Detector the app uses.
    /// No game screenshot is shipped, and the colours/geometry mirror the ones
    /// measured from 2560×1600 captures.
    /// </summary>
    public static class SelfTest
    {
        public static SelfTestResult Run()
        {
            var result = new SelfTestResult();
            var cfg = new Settings();

            Check(result, "1600×900 满血，我方在左", 1600, 900, false, true, false, true, cfg,
                "left", false, 0.0);
            Check(result, "1600×900 黄血 17%，我方在左", 1600, 900, true, true, false, true, cfg,
                "left", true, 0.17);
            Check(result, "1600×900 黄血 17%，我方在右（镜像）", 1600, 900, true, false, false, true, cfg,
                "right", true, 0.17);
            Check(result, "1920×1080 满血，我方在右", 1920, 1080, false, false, false, true, cfg,
                "right", false, 0.0);
            Check(result, "1280×720 黄血 17%，我方在左", 1280, 720, true, true, false, true, cfg,
                "left", true, 0.17);
            Check(result, "无「我」字：禁止触发", 1600, 900, true, true, false, false, cfg,
                "", false, 0.0);
            Check(result, "整条黄色（自定义 HUD）：拒绝触发", 1600, 900, false, true, true, true, cfg,
                "left", false, 1.0);

            ManualTest(result);

            result.Lines.Add("");
            result.Lines.Add(result.Pass ? "结论：全部通过。" : "结论：有项目未通过，请把 自检.txt 发给作者。");
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("街霸六的雨 · 自检 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("系统：" + Environment.OSVersion.VersionString);
                var lib = new MusicLibrary();
                lib.Reload("music", false, "ordered");
                sb.AppendLine("音乐：" + lib.Count + " 个文件，文件夹 " + lib.Folder);
                sb.AppendLine();
                foreach (string line in result.Lines) sb.AppendLine(line);
                File.WriteAllText(AppPaths.SelfTestFile, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
            return result;
        }

        static void Check(SelfTestResult result, string name, int w, int h, bool low,
            bool mineLeft, bool fullYellow, bool tag, Settings cfg, string side, bool yellow,
            double fraction)
        {
            using (var bmp = MakeFrame(w, h, low, mineLeft, fullYellow, tag, 0))
            {
                var det = Detector.Analyze(bmp, cfg, new DetectorState(), Environment.TickCount);
                bool ok = det.HudFound && det.SideKnown && det.Side == side &&
                    det.Yellow == yellow && Math.Abs(det.ActiveFraction - fraction) < 0.06;
                if (side.Length == 0)
                    ok = det.HudFound && !det.SideKnown && !det.Yellow;
                if (fullYellow)
                    ok = det.HudFound && det.YellowTooWide && !det.Yellow;
                result.Pass &= ok;
                result.Lines.Add(string.Format("{0} {1}", ok ? "✓" : "✗", name));
                result.Lines.Add(string.Format(
                    "   读到：hud={0} side={1} known={2} yellow={3} frac={4:0.000} wide={5} white={6}/{7}",
                    det.HudFound, det.Side.Length == 0 ? "?" : det.Side, det.SideKnown,
                    det.Yellow, det.ActiveFraction, det.YellowTooWide,
                    det.WhiteLeft, det.WhiteRight));
                if (!ok && det.HudFound)
                {
                    try
                    {
                        Directory.CreateDirectory(AppPaths.CaptureDir);
                        string file = Path.Combine(AppPaths.CaptureDir,
                            "自检失败-" + Sanitize(name) + ".png");
                        bmp.Save(file);
                        result.Lines.Add("   截图：" + file);
                    }
                    catch { }
                }
            }
        }

        static void ManualTest(SelfTestResult result)
        {
            using (var bmp = MakeFrame(1600, 900, true, true, false, true, 1))
            {
                var cfg = new Settings();
                cfg.SideMode = "manual";
                int w = 1600, h = 900;
                double fullW = 0.5625 * h, gap = 0.089375 * h;
                int innerL = w / 2 - (int)gap;
                int y0 = (int)(0.10375 * h), y1 = (int)(0.13 * h);
                int x0 = innerL - (int)fullW;
                cfg.ManualBar = new double[]
                {
                    x0 / (double)w, y0 / (double)h, fullW / w, (y1 - y0) / (double)h
                };
                var det = Detector.Analyze(bmp, cfg, new DetectorState(), Environment.TickCount);
                bool ok = det.HudFound && det.Side == "manual" && det.Yellow &&
                    det.ActiveFraction > 0.12 && det.ActiveFraction < 0.25;
                result.Pass &= ok;
                result.Lines.Add(string.Format("{0} 手动框选：黄血 17%", ok ? "✓" : "✗"));
                result.Lines.Add(string.Format("   读到：hud={0} yellow={1} frac={2:0.000}",
                    det.HudFound, det.Yellow, det.ActiveFraction));
            }
        }

        static string Sanitize(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace('×', 'x');
        }

        // ------------------------------------------------------------------
        static Bitmap MakeFrame(int w, int h, bool low, bool mineLeft, bool fullYellow,
            bool tag, int seed)
        {
            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using (var bg = new LinearGradientBrush(new Rectangle(0, 0, w, h),
                    Color.FromArgb(18, 22, 33), Color.FromArgb(7, 9, 14), 90f))
                    g.FillRectangle(bg, 0, 0, w, h);

                var rnd = new Random(seed + 11);
                for (int i = 0; i < 18; i++)
                {
                    int cw = rnd.Next(w / 20, w / 4);
                    int ch = rnd.Next(h / 30, h / 8);
                    int cx = rnd.Next(0, w - cw);
                    int cy = rnd.Next(h / 8, h * 3 / 4);
                    using (var b = new SolidBrush(Color.FromArgb(24, 40, 48, 70)))
                        g.FillRectangle(b, cx, cy, cw, ch);
                }

                double fullW = 0.5625 * h;
                double gap = 0.089375 * h;
                int y0 = (int)(0.10375 * h);
                int y1 = (int)(0.13 * h);
                int bh = Math.Max(8, y1 - y0);
                int innerL = w / 2 - (int)gap;
                int innerR = w / 2 + (int)gap;
                int leftOuter = innerL - (int)fullW;
                int rightOuter = innerR + (int)fullW;

                // Yellow scenery above the bar, away from both inner edges; it
                // must not be mistaken for low health.
                using (var b = new SolidBrush(Color.FromArgb(210, 224, 184, 74)))
                    g.FillRectangle(b, (int)(w * 0.34), y0 - bh - 4, (int)(w * 0.05), bh);

                // Drive gauge (green) below the HP bars.
                using (var b = new SolidBrush(Color.FromArgb(120, 60, 210, 70)))
                {
                    g.FillRectangle(b, leftOuter, y0 + bh + (int)(h * 0.012),
                        (int)fullW, Math.Max(3, (int)(h * 0.012)));
                    g.FillRectangle(b, innerR, y0 + bh + (int)(h * 0.012),
                        (int)fullW, Math.Max(3, (int)(h * 0.012)));
                }

                Color magenta = Color.FromArgb(191, 22, 81);
                Color blue = Color.FromArgb(20, 71, 154);
                Color yellow = Color.FromArgb(252, 239, 101);

                DrawBar(g, leftOuter, y0, (int)fullW, bh, innerL, true,
                    low && mineLeft, fullYellow && mineLeft, magenta, yellow);
                DrawBar(g, innerR, y0, (int)fullW, bh, innerR, false,
                    low && !mineLeft, fullYellow && !mineLeft, blue, yellow);

                if (tag)
                {
                    float size = Math.Max(8f, h * 0.014f);
                    using (var font = new Font("Microsoft YaHei", size, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var brush = new SolidBrush(Color.White))
                    using (var fmt = new StringFormat())
                    {
                        fmt.Alignment = StringAlignment.Center;
                        fmt.LineAlignment = StringAlignment.Center;
                        int tx = mineLeft
                            ? leftOuter + (int)(fullW * 0.05)
                            : rightOuter - (int)(fullW * 0.10);
                        g.DrawString("我", font, brush,
                            new RectangleF(tx, y0, (int)(fullW * 0.06) + 6, bh), fmt);
                    }
                    // A tinted opponent badge, close to the one SF6 draws; the
                    // white test must ignore it.
                    using (var b = new SolidBrush(Color.FromArgb(190, 160, 180, 210)))
                    {
                        int ox = mineLeft ? rightOuter - (int)(fullW * 0.05) : leftOuter + (int)(fullW * 0.03);
                        g.FillEllipse(b, ox, y0 + bh / 3, bh / 3, bh / 3);
                    }
                }

                using (var f = new Font("Microsoft YaHei", Math.Max(8f, h * 0.012f), FontStyle.Regular,
                    GraphicsUnit.Pixel))
                using (var b = new SolidBrush(Color.FromArgb(150, 230, 235, 245)))
                    g.DrawString("SYNTHETIC TEST FRAME", f, b, 12, h - 24);
            }
            return bmp;
        }

        static void DrawBar(Graphics g, int x, int y, int w, int h, int inner, bool left,
            bool low, bool fullYellow, Color normal, Color yellow)
        {
            using (var b = new SolidBrush(Color.FromArgb(30, 34, 44)))
                g.FillRectangle(b, x, y, w, h);
            using (var pen = new Pen(Color.FromArgb(90, 104, 130), 1f))
                g.DrawRectangle(pen, x, y, w - 1, h - 1);

            int fillW = w;
            int fillX = x;
            if (low)
            {
                fillW = (int)(w * 0.17);
                fillX = left ? inner - fillW : inner;
            }
            using (var b = new SolidBrush(low || fullYellow ? yellow : normal))
                g.FillRectangle(b, fillX + 1, y + 1, Math.Max(1, fillW - 2), h - 2);
        }
    }
}
