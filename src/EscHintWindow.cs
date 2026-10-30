using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WebView2Test.Helpers;

namespace WebView2Test
{
    /// <summary>
    /// 长按 ESC 退出全屏的提示窗。
    /// WebView2 基于 HwndHost，WPF 元素无法盖在它上面，因此提示用独立无边框窗口显示，
    /// 并设置为鼠标穿透，不影响网页交互。
    /// </summary>
    public sealed class EscHintWindow : Window
    {
        private const double ProgressBarWidth = 220;
        private const double BottomMargin = 64;

        private readonly Border _progress;

        public EscHintWindow()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            SizeToContent = SizeToContent.WidthAndHeight;
            Focusable = false;

            _progress = new Border
            {
                Width = 0,
                Height = 4,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromRgb(147, 197, 253)),
                CornerRadius = new CornerRadius(2)
            };

            var track = new Border
            {
                Width = ProgressBarWidth,
                Height = 4,
                Margin = new Thickness(0, 10, 0, 0),
                Background = new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255)),
                CornerRadius = new CornerRadius(2),
                Child = _progress
            };

            var text = new TextBlock
            {
                Text = "长按 ESC 3 秒退出全屏",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(249, 250, 251)),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var panel = new StackPanel();
            panel.Children.Add(text);
            panel.Children.Add(track);

            Content = new Border
            {
                Padding = new Thickness(24, 14, 24, 16),
                Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x1F, 0x29, 0x37)),
                CornerRadius = new CornerRadius(14),
                Child = panel
            };

            SourceInitialized += (_, _) => WindowStyleHelper.MakeClickThrough(this);
        }

        /// <summary>更新进度，0 到 1。</summary>
        public void SetProgress(double progress)
        {
            _progress.Width = ProgressBarWidth * Math.Clamp(progress, 0, 1);
        }

        /// <summary>显示在主窗口底部居中位置。</summary>
        public void ShowFor(Window owner)
        {
            if (!IsVisible)
            {
                Owner = owner;
                Show();
            }

            UpdateLayout();

            var width = ActualWidth > 0 ? ActualWidth : ProgressBarWidth + 48;
            var height = ActualHeight > 0 ? ActualHeight : 74;

            Left = owner.Left + ((owner.ActualWidth - width) / 2);
            Top = owner.Top + owner.ActualHeight - height - BottomMargin;
        }
    }
}
