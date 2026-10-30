using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WebView2Test.Helpers
{
    /// <summary>
    /// 窗口扩展样式辅助。
    /// </summary>
    public static class WindowStyleHelper
    {
        private const int GWL_EXSTYLE = -20;

        /// <summary>工具窗口：不出现在任务栏和 Alt+Tab 列表。</summary>
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        /// <summary>不激活：鼠标点击不会抢走前台窗口的激活状态。</summary>
        private const int WS_EX_NOACTIVATE = 0x08000000;

        /// <summary>鼠标穿透：点击落到下面的窗口上。</summary>
        private const int WS_EX_TRANSPARENT = 0x00000020;

        /// <summary>
        /// 让窗口不抢焦点、不进 Alt+Tab，用于悬浮球这类"点击但不该成为活动窗口"的窗口。
        /// </summary>
        public static void MakeNonActivatable(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var style = GetExtendedStyle(handle);
            SetExtendedStyle(handle, style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        }

        /// <summary>
        /// 让窗口完全鼠标穿透：不抢焦点、不进 Alt+Tab、点击事件落到下层窗口，用于纯提示类浮层。
        /// </summary>
        public static void MakeClickThrough(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var style = GetExtendedStyle(handle);
            SetExtendedStyle(handle, style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT);
        }

        private static int GetExtendedStyle(IntPtr handle)
        {
            return IntPtr.Size == 8
                ? (int)GetWindowLongPtr64(handle, GWL_EXSTYLE)
                : GetWindowLong32(handle, GWL_EXSTYLE);
        }

        private static void SetExtendedStyle(IntPtr handle, int style)
        {
            if (IntPtr.Size == 8)
            {
                SetWindowLongPtr64(handle, GWL_EXSTYLE, new IntPtr(style));
            }
            else
            {
                SetWindowLong32(handle, GWL_EXSTYLE, style);
            }
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    }
}
