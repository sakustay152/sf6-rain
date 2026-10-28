using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace Sf6Rain
{
    public sealed class WindowInfo
    {
        public IntPtr Handle;
        public string Title = "";
        public string ClassName = "";
        public int Width;
        public int Height;

        public override string ToString()
        {
            string t = Title.Length > 52 ? Title.Substring(0, 52) + "…" : Title;
            return string.Format("{0}   ({1}×{2})", t, Width, Height);
        }
    }

    public static class WinApi
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc proc, IntPtr param);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder text, int maxCount);

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int cmd);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        static extern uint GetDpiForSystem();

        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        static extern int GetDeviceCaps(IntPtr hdc, int index);

        public static int SystemDpi()
        {
            try
            {
                uint dpi = GetDpiForSystem();
                if (dpi >= 96 && dpi <= 480) return (int)dpi;
            }
            catch { }
            try
            {
                IntPtr dc = GetDC(IntPtr.Zero);
                if (dc != IntPtr.Zero)
                {
                    int dpi = GetDeviceCaps(dc, 90);
                    ReleaseDC(IntPtr.Zero, dc);
                    if (dpi >= 96 && dpi <= 480) return dpi;
                }
            }
            catch { }
            return 96;
        }

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute, out int value, int size);

        public const int WM_HOTKEY = 0x0312;
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int DWMWA_CLOAKED = 14;
        public const int SW_RESTORE = 9;

        public const int HotkeyToggle = 0x5F61;
        public const int HotkeyStop = 0x5F62;
        public const uint VK_F8 = 0x77;
        public const uint VK_F9 = 0x78;

        /// <summary>Top-level windows a person would think of as "the game".</summary>
        public static List<WindowInfo> ListWindows(int minWidth, int minHeight)
        {
            var list = new List<WindowInfo>();
            uint own = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            EnumWindows(delegate(IntPtr h, IntPtr p)
            {
                if (!IsWindowVisible(h) || IsIconic(h)) return true;
                if (GetWindowTextLength(h) <= 0) return true;
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (pid == own) return true;
                if ((GetWindowLong(h, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) != 0) return true;

                int cloaked;
                if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0)
                    return true;

                RECT rc;
                if (!GetClientRect(h, out rc)) return true;
                int cw = rc.Right - rc.Left;
                int ch = rc.Bottom - rc.Top;
                if (cw < minWidth || ch < minHeight) return true;

                var sb = new StringBuilder(512);
                GetWindowText(h, sb, sb.Capacity);
                string title = sb.ToString().Trim();
                if (title.Length == 0) return true;

                var cls = new StringBuilder(256);
                GetClassName(h, cls, cls.Capacity);
                string className = cls.ToString();
                if (IsShellWindow(className)) return true;

                var info = new WindowInfo();
                info.Handle = h;
                info.Title = title;
                info.ClassName = className;
                info.Width = cw;
                info.Height = ch;
                list.Add(info);
                return true;
            }, IntPtr.Zero);

            list.Sort(delegate(WindowInfo a, WindowInfo b)
            {
                int ka = WindowRank(a);
                int kb = WindowRank(b);
                if (ka != kb) return ka - kb;
                return string.Compare(a.Title, b.Title, StringComparison.CurrentCulture);
            });
            return list;
        }

        static bool IsShellWindow(string className)
        {
            return className == "CabinetWClass" || className == "ExploreWClass"
                || className == "Progman" || className == "WorkerW"
                || className == "Shell_TrayWnd" || className == "Shell_SecondaryTrayWnd"
                || className == "TaskManagerWindow" || className == "MultitaskingViewFrame";
        }

        static int WindowRank(WindowInfo w)
        {
            string t = (w.Title == null ? "" : w.Title).ToLowerInvariant();
            string c = (w.ClassName == null ? "" : w.ClassName).ToLowerInvariant();
            if (t.IndexOf("street fighter", StringComparison.Ordinal) >= 0
                || t.IndexOf("街头霸王", StringComparison.Ordinal) >= 0) return 0;
            if (c.IndexOf("streetfighter", StringComparison.Ordinal) >= 0
                || c.IndexOf("reframework", StringComparison.Ordinal) >= 0) return 1;
            if (c.IndexOf("unity", StringComparison.Ordinal) >= 0) return 2;
            return 3;
        }

        public static bool IsGameLike(WindowInfo w)
        {
            return w != null && WindowRank(w) <= 2;
        }

        public static WindowInfo FindByTitle(string title, int minWidth, int minHeight)
        {
            if (string.IsNullOrEmpty(title)) return null;
            List<WindowInfo> windows = ListWindows(minWidth, minHeight);
            foreach (WindowInfo w in windows)
                if (string.Equals(w.Title, title, StringComparison.OrdinalIgnoreCase))
                    return w;
            foreach (WindowInfo w in windows)
                if (w.Title.IndexOf(title, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    return w;
            return null;
        }

        /// <summary>Client area in physical screen pixels. Zero if the window is gone.</summary>
        public static Rectangle ClientOnScreen(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return Rectangle.Empty;
            RECT rc;
            if (!GetClientRect(hwnd, out rc)) return Rectangle.Empty;
            int w = rc.Right - rc.Left;
            int h = rc.Bottom - rc.Top;
            if (w <= 0 || h <= 0) return Rectangle.Empty;
            var origin = new POINT();
            origin.X = 0;
            origin.Y = 0;
            if (!ClientToScreen(hwnd, ref origin)) return Rectangle.Empty;
            return new Rectangle(origin.X, origin.Y, w, h);
        }

        public static void Activate(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return;
            if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
        }
    }
}
