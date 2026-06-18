using System.Windows;
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

        public MainWindow()
        {
            _appSettings = ConfigHelper.GetSetting<AppSettings>();
            _webViewSettings = ConfigHelper.GetSetting<WebViewSettings>();

            InitializeComponent();
            InitApp();
            Loaded += MainWindow_Loaded;
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
            webView.CoreWebView2.Navigate(_webViewSettings.Domain);
        }
    }
}
