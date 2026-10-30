using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
        /// <summary>长按 ESC 退出全屏所需的时长（秒）。</summary>
        private const double EscHoldSeconds = 3;

        private AppSettings _appSettings;
        private WebViewSettings _webViewSettings;

        private NavigationToolWindow? _navTool;

        private readonly FullScreenHelper _fullScreen;
        private readonly DispatcherTimer _escTimer;
        private readonly Stopwatch _escHoldWatch = new();

        private KeyboardInputHook? _keyboardHook;
        private EscHintWindow? _escHintWindow;
        private bool _isEscHeld;

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

            _fullScreen = new FullScreenHelper(this);
            _escTimer = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(40)
            };
            _escTimer.Tick += EscTimer_Tick;

            _defaultIcon = FaviconHelper.LoadDefault();
            if (_defaultIcon is not null)
            {
                Icon = _defaultIcon;
            }

            Closed += MainWindow_Closed;
            Deactivated += MainWindow_Deactivated;
            InitApp();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _escTimer.Stop();
            _escHintWindow?.Close();
            _escHintWindow = null;
            _keyboardHook?.Dispose();
            _keyboardHook = null;
            WindowIconHelper.ReleaseAll(_iconHandles);
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InstallKeyboardHook();

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

        // ===== F11 全屏 / 长按 ESC 3 秒退出 =====

        /// <summary>
        /// 安装低级键盘钩子。WebView2 获得焦点后 WPF 收不到键盘事件，钩子负责捕获 F11 / ESC；
        /// 安装失败时退化为窗口自身的键盘事件。
        /// </summary>
        private void InstallKeyboardHook()
        {
            var hook = new KeyboardInputHook
            {
                KeyDownFilter = HandleHotkeyDown,
                KeyUpFilter = HandleHotkeyUp
            };

            if (hook.TryInstall())
            {
                _keyboardHook = hook;
                return;
            }

            hook.Dispose();

            PreviewKeyDown += MainWindow_PreviewKeyDown;
            PreviewKeyUp += MainWindow_PreviewKeyUp;
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (HandleHotkeyDown(e.Key))
            {
                e.Handled = true;
            }
        }

        private void MainWindow_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (HandleHotkeyUp(e.Key))
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// 快捷键按下：F11 切换全屏；全屏中 ESC 开始 3 秒长按计时。
        /// 返回 true 表示吞掉按键，不再传给网页。
        /// </summary>
        private bool HandleHotkeyDown(Key key)
        {
            switch (key)
            {
                case Key.F11:
                    ToggleFullScreen();
                    return true;

                case Key.Escape when _fullScreen.IsFullScreen:
                    BeginEscHold();
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 快捷键抬起：长按 ESC 中途松手则取消退出全屏。
        /// </summary>
        private bool HandleHotkeyUp(Key key)
        {
            if (key != Key.Escape || !_isEscHeld)
            {
                return false;
            }

            EndEscHold();
            return true;
        }

        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
            // 窗口失焦时中断长按，避免切走后还在后台倒计时
            if (_isEscHeld)
            {
                EndEscHold();
            }
        }

        private void ToggleFullScreen()
        {
            if (_fullScreen.IsFullScreen)
            {
                ExitFullScreen();
            }
            else
            {
                EnterFullScreen();
            }
        }

        private void EnterFullScreen()
        {
            if (_fullScreen.IsFullScreen)
            {
                return;
            }

            EndEscHold();
            _fullScreen.Enter();
        }

        private void ExitFullScreen()
        {
            EndEscHold();

            if (_fullScreen.IsFullScreen)
            {
                _fullScreen.Exit();
            }
        }

        private void BeginEscHold()
        {
            // 键盘自动重复会多次触发 KeyDown，只认第一次
            if (_isEscHeld)
            {
                return;
            }

            _isEscHeld = true;
            _escHoldWatch.Restart();

            _escHintWindow ??= new EscHintWindow();
            _escHintWindow.SetProgress(0);
            _escHintWindow.ShowFor(this);

            _escTimer.Start();
        }

        private void EndEscHold()
        {
            _escTimer.Stop();
            _escHoldWatch.Reset();
            _isEscHeld = false;
            _escHintWindow?.Hide();
        }

        private void EscTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isEscHeld)
            {
                EndEscHold();
                return;
            }

            var progress = _escHoldWatch.Elapsed.TotalSeconds / EscHoldSeconds;
            _escHintWindow?.SetProgress(progress);

            if (progress >= 1)
            {
                ExitFullScreen();
            }
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
