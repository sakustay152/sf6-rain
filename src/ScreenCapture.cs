using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Sf6Rain
{
    public sealed class CaptureResult : IDisposable
    {
        public Bitmap Image;
        public Rectangle Client;
        public bool Black;
        public string Error = "";

        public void Dispose()
        {
            if (Image != null) Image.Dispose();
            Image = null;
        }
    }

    /// <summary>
    /// GDI screen copy of the game's client area. This is the same pixels the
    /// player sees, at physical resolution, with no injection and no hooks.
    /// </summary>
    public static class ScreenCapture
    {
        public static CaptureResult Capture(IntPtr hwnd)
        {
            var result = new CaptureResult();
            if (hwnd == IntPtr.Zero || !WinApi.IsWindow(hwnd))
            {
                result.Error = "游戏窗口已关闭";
                return result;
            }
            if (WinApi.IsIconic(hwnd))
            {
                result.Error = "游戏窗口被最小化了";
                return result;
            }

            Rectangle rect = WinApi.ClientOnScreen(hwnd);
            if (rect.IsEmpty || rect.Width < 320 || rect.Height < 180)
            {
                result.Error = "画面尺寸异常（请用无边框窗口模式）";
                return result;
            }
            result.Client = rect;

            try
            {
                var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(rect.Location, Point.Empty, rect.Size,
                        CopyPixelOperation.SourceCopy);
                }
                result.Image = bmp;
                result.Black = MostlyBlack(bmp);

                if (result.Black)
                {
                    // Some DirectX windows return black from the screen copy even
                    // when they are visible; PrintWindow occasionally succeeds.
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        IntPtr hdc = g.GetHdc();
                        try { WinApi.PrintWindow(hwnd, hdc, 2); }
                        finally { g.ReleaseHdc(hdc); }
                    }
                    bool stillBlack = MostlyBlack(bmp);
                    if (!stillBlack)
                    {
                        result.Black = false;
                    }
                    else
                    {
                        result.Error = "画面全黑：请把游戏设为无边框窗口模式并保持在最前";
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                result.Error = "截图失败：" + ex.Message;
                if (result.Image != null) result.Image.Dispose();
                result.Image = null;
                return result;
            }
        }

        /// <summary>One-off grab for manual bar selection. Uses the same path.</summary>
        public static CaptureResult CaptureOnce(IntPtr hwnd)
        {
            return Capture(hwnd);
        }

        public static bool MostlyBlack(Bitmap bmp)
        {
            if (bmp == null) return true;
            int step = Math.Max(6, Math.Min(bmp.Width, bmp.Height) / 50);
            int total = 0, dark = 0;
            for (int y = 0; y < bmp.Height; y += step)
                for (int x = 0; x < bmp.Width; x += step)
                {
                    Color c = bmp.GetPixel(x, y);
                    total++;
                    if (c.R < 8 && c.G < 8 && c.B < 8) dark++;
                }
            return total > 0 && dark >= total * 0.985;
        }

        /// <summary>Downscale for the live preview. Caller owns the returned bitmap.</summary>
        public static Bitmap Preview(Bitmap source, int maxWidth)
        {
            if (source == null) return null;
            if (source.Width <= maxWidth) return (Bitmap)source.Clone();
            int w = maxWidth;
            int h = (int)Math.Round(source.Height * (maxWidth / (double)source.Width));
            if (h < 1) h = 1;
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(source, new Rectangle(0, 0, w, h));
            }
            return bmp;
        }
    }
}
