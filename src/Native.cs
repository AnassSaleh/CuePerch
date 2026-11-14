using System;
using System.Runtime.InteropServices;

namespace FloatPrompter
{
    internal static class Native
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        public const int WM_MOUSEACTIVATE = 0x0021;
        public const int WM_NCHITTEST = 0x0084;
        public const int WM_ENTERSIZEMOVE = 0x0231;
        public const int WM_EXITSIZEMOVE = 0x0232;
        public const int WM_DISPLAYCHANGE = 0x007E;
        public const int WM_HOTKEY = 0x0312;
        public const int MA_NOACTIVATE = 3;

        public const int HTLEFT = 10, HTRIGHT = 11, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

        public const uint SWP_NOSIZE = 0x0001, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        public const uint WDA_NONE = 0x0, WDA_MONITOR = 0x1, WDA_EXCLUDEFROMCAPTURE = 0x11;

        public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowDisplayAffinity(IntPtr hWnd, out uint affinity);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT p);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int index, int value);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, uint flags);

        [DllImport("shcore.dll")]
        public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string cls, string title);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int cmd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        /// <summary>DPI scale (1.0 = 96 dpi) of the monitor containing the given physical point.</summary>
        public static double DpiScaleAt(int x, int y)
        {
            try
            {
                POINT p; p.X = x; p.Y = y;
                IntPtr mon = MonitorFromPoint(p, 2 /* MONITOR_DEFAULTTONEAREST */);
                uint dx, dy;
                if (GetDpiForMonitor(mon, 0 /* MDT_EFFECTIVE_DPI */, out dx, out dy) == 0 && dx > 0)
                    return dx / 96.0;
            }
            catch (Exception) { }
            return 1.0;
        }

        /// <summary>
        /// Hide a window from screen capture / sharing (Win10 2004+), with a fallback for older builds.
        /// Returns the affinity Windows actually reports afterwards, so callers can tell the user when hiding failed.
        /// </summary>
        public static uint ApplyCaptureExclusion(IntPtr hwnd, bool hide, string who)
        {
            if (hwnd == IntPtr.Zero) return WDA_NONE;
            int err1 = 0, err2 = 0;
            if (hide)
            {
                if (!SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE))
                {
                    err1 = Marshal.GetLastWin32Error();
                    if (!SetWindowDisplayAffinity(hwnd, WDA_MONITOR)) err2 = Marshal.GetLastWin32Error();
                }
            }
            else
            {
                SetWindowDisplayAffinity(hwnd, WDA_NONE);
            }

            uint actual;
            if (!GetWindowDisplayAffinity(hwnd, out actual)) actual = WDA_NONE;
            bool layered = (GetWindowLong(hwnd, GWL_EXSTYLE) & WS_EX_LAYERED) != 0;
            string key = who + ":" + hide + ":" + actual;
            string prev;
            lastLogged.TryGetValue(who, out prev);
            if (prev != key)
            {
                lastLogged[who] = key;
                App.Log(string.Format("Capture[{0}]: hide={1} affinity=0x{2:X} layered={3} err={4}/{5} windows={6}",
                    who, hide, actual, layered, err1, err2, WindowsBuild));
            }
            return actual;
        }

        private static readonly System.Collections.Generic.Dictionary<string, string> lastLogged =
            new System.Collections.Generic.Dictionary<string, string>();

        public const int WS_EX_LAYERED = 0x00080000;

        /// <summary>Windows build, e.g. "10.0.19045.4651", read from the registry (Environment.OSVersion can be shimmed).</summary>
        public static string WindowsBuild
        {
            get
            {
                try
                {
                    using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    {
                        if (k != null)
                        {
                            object build = k.GetValue("CurrentBuild"), ubr = k.GetValue("UBR"), name = k.GetValue("DisplayVersion");
                            return "10.0." + build + "." + ubr + (name != null ? " (" + name + ")" : "");
                        }
                    }
                }
                catch (Exception) { }
                return Environment.OSVersion.Version.ToString();
            }
        }
    }
}
