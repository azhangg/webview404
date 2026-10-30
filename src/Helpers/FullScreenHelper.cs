using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WebView2Test.Helpers
{
    /// <summary>
    /// 无边框全屏：铺满窗口所在显示器的整屏区域（含任务栏区域），退出时恢复原来的窗口状态与大小。
    /// </summary>
    public sealed class FullScreenHelper
    {
        private const int MONITOR_DEFAULTTONEAREST = 2;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private static readonly IntPtr HWND_TOP = IntPtr.Zero;

        private readonly Window _window;

        private WindowState _prevState;
        private WindowStyle _prevStyle;
        private ResizeMode _prevResizeMode;
        private Rect _prevBounds;

        public FullScreenHelper(Window window)
        {
            _window = window;
        }

        public bool IsFullScreen { get; private set; }

        public void Enter()
        {
            if (IsFullScreen)
            {
                return;
            }

            var handle = new WindowInteropHelper(_window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            _prevState = _window.WindowState;
            _prevStyle = _window.WindowStyle;
            _prevResizeMode = _window.ResizeMode;
            _prevBounds = CaptureBounds();

            // 先回到普通状态，避免最大化状态下改样式导致位置抖动
            _window.WindowState = WindowState.Normal;
            _window.WindowStyle = WindowStyle.None;
            _window.ResizeMode = ResizeMode.NoResize;

            if (TryGetMonitorBounds(handle, out var left, out var top, out var width, out var height))
            {
                // 用物理像素铺满整块屏幕，任务栏会被前台全屏窗口盖住（系统自动隐藏）
                SetWindowPos(handle, HWND_TOP, left, top, width, height, SWP_SHOWWINDOW | SWP_FRAMECHANGED);
            }
            else
            {
                _window.WindowState = WindowState.Maximized;
            }

            IsFullScreen = true;
        }

        public void Exit()
        {
            if (!IsFullScreen)
            {
                return;
            }

            IsFullScreen = false;
            var handle = new WindowInteropHelper(_window).Handle;

            _window.WindowStyle = _prevStyle;
            _window.ResizeMode = _prevResizeMode;
            _window.WindowState = WindowState.Normal;

            RestoreBounds();

            if (_prevState == WindowState.Maximized)
            {
                _window.WindowState = WindowState.Maximized;
            }

            if (handle != IntPtr.Zero)
            {
                // 让边框样式变化立即生效
                SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
            }

            _window.Activate();
        }

        private Rect CaptureBounds()
        {
            if (_window.WindowState == WindowState.Normal && _window.Width > 0 && _window.Height > 0)
            {
                return new Rect(_window.Left, _window.Top, _window.Width, _window.Height);
            }

            var restore = _window.RestoreBounds;
            return restore.IsEmpty ? SystemParameters.WorkArea : restore;
        }

        private void RestoreBounds()
        {
            var bounds = _prevBounds;
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            {
                bounds = SystemParameters.WorkArea;
            }

            _window.Left = bounds.Left;
            _window.Top = bounds.Top;
            _window.Width = bounds.Width;
            _window.Height = bounds.Height;
        }

        private static bool TryGetMonitorBounds(IntPtr handle, out int left, out int top, out int width, out int height)
        {
            left = top = width = height = 0;

            var monitor = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
            {
                return false;
            }

            var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info))
            {
                return false;
            }

            var rect = info.rcMonitor;
            left = rect.Left;
            top = rect.Top;
            width = rect.Right - rect.Left;
            height = rect.Bottom - rect.Top;

            return width > 0 && height > 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int cbSize;
            public NativeRect rcMonitor;
            public NativeRect rcWork;
            public uint dwFlags;
        }

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint flags);
    }
}
