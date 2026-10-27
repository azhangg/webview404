using System.IO;
using System.Windows.Media.Imaging;

namespace WebView2Test.Helpers
{
    /// <summary>
    /// 图标转换辅助：把网站 favicon 流 / 内置 favicon.ico 转成可用于 Window.Icon 的位图。
    /// </summary>
    public static class FaviconHelper
    {
        private static readonly Uri DefaultIconUri = new("pack://application:,,,/Resources/favicon.ico", UriKind.Absolute);

        /// <summary>
        /// 读取内置 favicon.ico（取最大的一帧），用于页面没有 favicon 时兜底。
        /// </summary>
        public static BitmapSource? LoadDefault()
        {
            try
            {
                var decoder = BitmapDecoder.Create(DefaultIconUri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

                BitmapFrame? frame = null;
                foreach (var item in decoder.Frames)
                {
                    if (frame is null || item.PixelWidth > frame.PixelWidth)
                    {
                        frame = item;
                    }
                }

                if (frame is null)
                {
                    return null;
                }

                if (frame.CanFreeze)
                {
                    frame.Freeze();
                }

                return frame;
            }
            catch (Exception)
            {
                // 资源缺失或 ico 解码失败时不设置兜底图标，保持系统默认
                return null;
            }
        }

        /// <summary>
        /// 把 favicon 数据流转成冻结的位图，流关闭后依然可用。
        /// </summary>
        public static BitmapSource? FromStream(Stream? stream)
        {
            if (stream is null)
            {
                return null;
            }

            try
            {
                if (stream.CanSeek)
                {
                    stream.Position = 0;
                }

                // WebView2 返回的流不可靠（直接解码会得到 1x1 图），先完整复制到内存
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                buffer.Position = 0;

                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = buffer;
                image.EndInit();

                if (image.CanFreeze)
                {
                    image.Freeze();
                }

                return image;
            }
            catch (Exception)
            {
                // 部分站点只提供 SVG / 非 PNG 数据，解码失败时返回 null
                return null;
            }
        }
    }
}
