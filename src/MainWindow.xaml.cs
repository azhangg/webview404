using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using WebView2Test.Helpers;
using WebView2Test.HostObjects;
using WebView2Test.Settings;

namespace WebView2Test
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private AppSettings _appSettings;
        private WebViewSettings _webViewSettings;

        private NavigationToolWindow? _navTool;

        /// <summary>内置 favicon，页面没有图标时回落使用。</summary>
        private readonly BitmapSource? _defaultIcon;

        /// <summary>请求序号，用于丢弃先发后至的过期图标。</summary>
        private int _iconRequest;

        /// <summary>本次导航是否已经同步过图标，避免重复刷新。</summary>
        private bool _iconAppliedForNavigation;

        /// <summary>任务栏 / 标题栏图标句柄，窗口关闭时统一释放。</summary>
        private readonly List<IntPtr> _iconHandles = new();

        public MainWindow()
        {
            _appSettings = ConfigHelper.GetSetting<AppSettings>();
            _webViewSettings = ConfigHelper.GetSetting<WebViewSettings>();

            InitializeComponent();

            _defaultIcon = FaviconHelper.LoadDefault();
            if (_defaultIcon is not null)
            {
                Icon = _defaultIcon;
            }

            Closed += MainWindow_Closed;
            InitApp();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            WindowIconHelper.ReleaseAll(_iconHandles);
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InitWebViewAsync();

            if (_webViewSettings.DevelopmentMode)
            {
                _navTool = new NavigationToolWindow(this, webView);
                _navTool.Show();
            }
        }

        private void InitApp()
        {
            if (_appSettings.Width > 0)
            {
                this.Width = _appSettings.Width;
            }

            if (_appSettings.Height > 0)
            {
                this.Height = _appSettings.Height;
            }

            if (_appSettings.MinWidth > 0)
            {
                this.MinWidth = _appSettings.MinWidth;
            }

            if (_appSettings.MinHeight > 0)
            {
                this.MinHeight = _appSettings.MinHeight;
            }

            if (_appSettings.MaxWidth > 0)
            {
                this.MaxWidth = _appSettings.MaxWidth;
            }
            if (_appSettings.MaxHeight > 0)
            {
                this.MaxHeight = _appSettings.MaxHeight;
            }

            this.Title = _appSettings.Name;
        }

        private async Task InitWebViewAsync()
        {
            await webView.EnsureCoreWebView2Async(null);
            if (!_webViewSettings.DevelopmentMode)
            {
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            }

            webView.CoreWebView2.AddHostObjectToScript("FileApi", new FileApi());

            // 站点图标变化时同步到窗口 / 任务栏
            webView.CoreWebView2.FaviconChanged += CoreWebView2_FaviconChanged;
            // 兜底：新页面没有 favicon 或未触发变化事件时，导航完成后同步（页面无图标则回落默认图标）
            webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;

            webView.CoreWebView2.Navigate(_webViewSettings.Domain);
        }

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // 每次导航重新开始，允许本次导航再同步一次图标
            _iconAppliedForNavigation = false;
        }

        private async void CoreWebView2_FaviconChanged(object? sender, object e)
        {
            await UpdateWindowIconAsync();
        }

        private async void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (_iconAppliedForNavigation)
            {
                return;
            }

            await UpdateWindowIconAsync();
        }

        /// <summary>
        /// 读取当前页面 favicon 并应用到窗口图标（同时影响标题栏、任务栏、Alt+Tab）。
        /// </summary>
        private async Task UpdateWindowIconAsync()
        {
            var core = webView.CoreWebView2;
            if (core is null)
            {
                return;
            }

            var request = ++_iconRequest;
            Stream? stream = null;

            try
            {
                stream = await core.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png);

                // 期间又发生了新的导航 / 图标变化，丢弃这次结果
                if (request != _iconRequest)
                {
                    return;
                }

                ApplyIcon(FaviconHelper.FromStream(stream) ?? _defaultIcon);
                _iconAppliedForNavigation = true;
            }
            catch (Exception)
            {
                // WebView 已关闭或取不到图标，保持当前图标
            }
            finally
            {
                stream?.Dispose();
            }
        }

        /// <summary>
        /// 应用窗口图标：Window.Icon 负责标题栏，WM_SETICON 负责任务栏与 Alt+Tab。
        /// </summary>
        private void ApplyIcon(BitmapSource? image)
        {
            if (image is null)
            {
                return;
            }

            Icon = image;
            WindowIconHelper.Apply(this, image, _iconHandles);
        }
    }
}
