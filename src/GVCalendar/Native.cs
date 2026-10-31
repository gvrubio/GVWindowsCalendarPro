using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace GVCalendar
{
    internal static class Native
    {
        public const int WM_DPICHANGED = 0x02E0;
        public const int WM_ACTIVATE = 0x0006;
        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);

        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_TOPMOST = 0x00000008;
        public const int CS_DROPSHADOW = 0x00020000;

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWA_BORDER_COLOR = 34;
        public const int DWMWCP_ROUND = 2;

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int RegisterWindowMessage(string message);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        public static bool IsWindows11 => Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000;

        public static int GetDpiForPoint(Point p)
        {
            try
            {
                IntPtr mon = MonitorFromPoint(new POINT { X = p.X, Y = p.Y }, 2 /* MONITOR_DEFAULTTONEAREST */);
                if (GetDpiForMonitor(mon, 0 /* MDT_EFFECTIVE_DPI */, out uint dx, out _) == 0 && dx > 0)
                    return (int)dx;
            }
            catch { /* Windows antiguo o Wine */ }
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
                return (int)g.DpiX;
        }

        public static void TrySetAttribute(IntPtr hwnd, int attr, int value)
        {
            try { DwmSetWindowAttribute(hwnd, attr, ref value, sizeof(int)); }
            catch { /* DWM no disponible */ }
        }
    }
}
