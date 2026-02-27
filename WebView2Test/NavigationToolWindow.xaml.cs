using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using WebView2Test.Helpers;
using WebView2Test.Settings;

namespace WebView2Test
{
    public partial class NavigationToolWindow : Window
    {
        private MainWindow _owner;
        private WebView2 _webView;
        private AppSettings _appSettings;

        private bool _isLoading;

        public NavigationToolWindow(MainWindow owner, WebView2 webView)
        {
            InitializeComponent();
            _owner = owner;
            this.Owner = owner;
            _webView = webView;
            _appSettings = ConfigHelper.GetSetting<AppSettings>();

            btnBack.Click += BtnBack_Click;
            btnForward.Click += BtnForward_Click;
            btnRefresh.Click += BtnRefresh_Click;
            btnGo.Click += BtnGo_Click;

            owner.LocationChanged += Owner_LocationOrSizeChanged;
            owner.SizeChanged += Owner_LocationOrSizeChanged;
            owner.StateChanged += Owner_StateChanged;
            owner.Closed += Owner_Closed;

            this.Loaded += NavigationToolWindow_Loaded;

            _webView.CoreWebView2.HistoryChanged += CoreWebView2_HistoryChanged;
            _webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            _webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            txtUrl.KeyDown += TxtUrl_KeyDown;
        }

        private void NavigationToolWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePosition();
            UpdateNavState();
        }

        private void CoreWebView2_HistoryChanged(object? sender, object e)
        {
            if (sender is CoreWebView2 webView)
            {
                txtUrl.Text = webView.Source;
                UpdateNavState();
            }
        }

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            txtUrl.Text = e.Uri;
            SetLoading(true);
            UpdateNavState();
        }

        private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (sender is CoreWebView2 webView)
            {
                SetLoading(false);
                Owner.Title = $"{_appSettings.Name} - {webView.DocumentTitle}";
                UpdateNavState();
            }
        }

        private void UpdateNavState()
        {
            if (_webView?.CoreWebView2 == null)
            {
                btnBack.IsEnabled = false;
                btnForward.IsEnabled = false;
                return;
            }

            btnBack.IsEnabled = _webView.CoreWebView2.CanGoBack;
            btnForward.IsEnabled = _webView.CoreWebView2.CanGoForward;
        }

        private void SetLoading(bool isLoading)
        {
            if (_isLoading == isLoading)
            {
                return;
            }

            _isLoading = isLoading;

            if (TryFindResource("RefreshSpinStoryboard") is not Storyboard storyboard)
            {
                return;
            }

            if (isLoading)
            {
                storyboard.Begin(this, true);
            }
            else
            {
                storyboard.Stop(this);
                RefreshRotate.Angle = 0;
            }
        }

        private void Owner_Closed(object? sender, EventArgs e)
        {
            this.Close();
        }

        private void Owner_StateChanged(object? sender, EventArgs e)
        {
            UpdatePosition();
        }

        private void Owner_LocationOrSizeChanged(object? sender, EventArgs e)
        {
            UpdatePosition();
        }

        private void BtnGo_Click(object? sender, RoutedEventArgs e)
        {
            NavigateTo(txtUrl.Text);
        }

        private void TxtUrl_KeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                NavigateTo(txtUrl.Text);
            }
        }

        private void BtnRefresh_Click(object? sender, RoutedEventArgs e)
        {
            if (_webView?.CoreWebView2 != null)
            {
                _webView.CoreWebView2.Reload();
            }
        }

        private void BtnForward_Click(object? sender, RoutedEventArgs e)
        {
            if (_webView?.CoreWebView2 != null && _webView.CoreWebView2.CanGoForward)
            {
                _webView.CoreWebView2.GoForward();
            }
        }

        private void BtnBack_Click(object? sender, RoutedEventArgs e)
        {
            if (_webView?.CoreWebView2 != null && _webView.CoreWebView2.CanGoBack)
            {
                _webView.CoreWebView2.GoBack();
            }
        }

        private void NavigateTo(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://cn.bing.com/search?q=" + url;
            }

            if (_webView?.CoreWebView2 != null)
            {
                try
                {
                    _webView.CoreWebView2.Navigate(url);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法导航到指定URL: {ex.Message}", "导航错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
        }

        private void UpdatePosition()
        {
            if (_owner == null) return;

            var restoreBounds = _owner.RestoreBounds;
            double left = restoreBounds.X;
            double top;

            if (_owner.WindowState == WindowState.Maximized)
            {
                top = restoreBounds.Y;
            }
            else
            {
                top = restoreBounds.Y + restoreBounds.Height;
            }

            this.Left = left;
            this.Top = top;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not TextBox)
            {
                DragMove();
            }
        }
    }
}
