using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace WebView2Test.Helpers
{
    /// <summary>
    /// 通过 Win32 WM_SETICON 更新窗口图标。只设置 Window.Icon 时标题栏会变、任务栏按钮不会，
    /// 需要把大/小图标推给窗口句柄并触发一次边框刷新。
    /// </summary>
    public static class WindowIconHelper
    {
        private const int WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;

        private const int SmallIconSize = 16;
        private const int BigIconSize = 32;

        /// <summary>保留的图标句柄上限（每次更新产生大/小两个）。</summary>
        private const int MaxRetainedIcons = 8;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint flags);

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        /// <summary>
        /// 把位图应用到指定窗口。图标句柄会追加到 <paramref name="handles"/>，由调用方在窗口关闭时统一释放
        /// （立即销毁会让任务栏按钮回落到 exe 图标）。
        /// </summary>
        public static void Apply(Window window, BitmapSource? image, IList<IntPtr> handles)
        {
            if (image is null)
            {
                return;
            }

            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var newBig = CreateHIcon(image, BigIconSize);
            if (newBig != IntPtr.Zero)
            {
                SendMessage(handle, WM_SETICON, ICON_BIG, newBig);
                handles.Add(newBig);
            }

            var newSmall = CreateHIcon(image, SmallIconSize);
            if (newSmall != IntPtr.Zero)
            {
                SendMessage(handle, WM_SETICON, ICON_SMALL, newSmall);
                handles.Add(newSmall);
            }

            // 任务栏按钮只在创建时读取图标，用 SWP_FRAMECHANGED 触发一次重绘
            SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

            Trim(handles);
        }

        /// <summary>
        /// 释放所有句柄，窗口关闭时调用。
        /// </summary>
        public static void ReleaseAll(IList<IntPtr> handles)
        {
            foreach (var icon in handles)
            {
                if (icon != IntPtr.Zero)
                {
                    DestroyIcon(icon);
                }
            }

            handles.Clear();
        }

        /// <summary>
        /// 任务栏可能延迟读取图标，保留最近几代句柄，超出的销毁，避免长时间运行累积 GDI 对象。
        /// </summary>
        private static void Trim(IList<IntPtr> handles)
        {
            while (handles.Count > MaxRetainedIcons)
            {
                if (handles[0] != IntPtr.Zero)
                {
                    DestroyIcon(handles[0]);
                }

                handles.RemoveAt(0);
            }
        }

        private static IntPtr CreateHIcon(BitmapSource source, int size)
        {
            try
            {
                using var sourceImage = ToDrawingImage(source);
                if (sourceImage is null)
                {
                    return IntPtr.Zero;
                }

                using var canvas = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(canvas))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.Clear(Color.Transparent);
                    graphics.DrawImage(sourceImage, 0, 0, size, size);
                }

                return canvas.GetHicon();
            }
            catch (Exception)
            {
                return IntPtr.Zero;
            }
        }

        private static Image? ToDrawingImage(BitmapSource source)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            stream.Position = 0;

            // GDI+ 需要流保持可读，这里复制一份脱离流
            using var decoded = Image.FromStream(stream);
            return new Bitmap(decoded);
        }
    }
}
