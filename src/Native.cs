using System;
using System.Runtime.InteropServices;

namespace NexusCore
{
    /// <summary>
    /// Thin P/Invoke layer. Everything the saver needs from user32/kernel32,
    /// kept in one place so the rest of the code stays plain WinForms.
    /// </summary>
    internal static class Native
    {
        // ---- window styles -------------------------------------------------
        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;

        public const int WS_CHILD = 0x40000000;
        public const int WS_POPUP = unchecked((int)0x80000000);
        public const int WS_CAPTION = 0x00C00000;
        public const int WS_THICKFRAME = 0x00040000;

        public const int WS_EX_TOPMOST = 0x00000008;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_APPWINDOW = 0x00040000;

        // ---- show cursor ---------------------------------------------------
        public const int SW_SHOW = 5;
        public const int SW_SHOWNA = 8;

        // SetWindowPos flags used by the preview path
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_NOACTIVATE = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X, Y;
        }

        // ---- DPI ------------------------------------------------------------
        // Must be called before the first window is created.
        public static void EnablePerMonitorDpi()
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 == (IntPtr)(-4)
                if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return;
            }
            catch (EntryPointNotFoundException) { }
            catch (DllNotFoundException) { }

            try
            {
                // PROCESS_PER_MONITOR_DPI_AWARE == 2
                if (SetProcessDpiAwareness(2)) return;
            }
            catch (EntryPointNotFoundException) { }

            try { SetProcessDPIAware(); }
            catch (EntryPointNotFoundException) { }
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwareness(int awareness);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        // ---- window plumbing -------------------------------------------------
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int index, int newLong);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y,
                                               int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT pt);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ShowCursor(bool show);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int cmdShow);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool MonitorFromPoint(POINT pt, uint flags, out IntPtr monitor);

        /// <summary>
        /// Strip the things a screensaver must not have: no caption, no
        /// thick frame, no taskbar presence, no Alt+Tab entry.
        /// </summary>
        public static void MakeInvisibleToShell(IntPtr handle)
        {
            int ex = GetWindowLong(handle, GWL_EXSTYLE);
            ex |= WS_EX_TOOLWINDOW;
            ex &= ~WS_EX_APPWINDOW;
            ex |= WS_EX_TOPMOST;
            SetWindowLong(handle, GWL_EXSTYLE, ex);
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLong64(IntPtr hWnd, int index);

        public static int GetWindowLong(IntPtr hWnd, int index)
        {
            if (IntPtr.Size == 8) return GetWindowLong64(hWnd, index).ToInt32();
            return GetWindowLong32(hWnd, index);
        }
    }
}