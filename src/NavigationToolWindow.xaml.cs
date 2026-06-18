using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
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
        private bool _isToolVisibleInFullScreen = false;
        private bool _isInFullScreenMode;
        private bool _isDraggingFloatingBall;
        private bool _suppressFloatingBallClick;
        private Window? _floatingBallWindow;
        private Button? _floatingBallButton;
        private Border? _floatingBallIndicator;
        private Point _floatingBallDragStartCursor;
        private Point _floatingBallStartPosition;

        private const double FloatingBallSize = 58;
        private const double FloatingBallMargin = 20;
        private const double NavigationSpacing = 12;

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
            this.Closed += NavigationToolWindow_Closed;

            _webView.CoreWebView2.HistoryChanged += CoreWebView2_HistoryChanged;
            _webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            _webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            _webView.CoreWebView2.ContainsFullScreenElementChanged += CoreWebView2_ContainsFullScreenElementChanged;
            txtUrl.KeyDown += TxtUrl_KeyDown;
        }

        private void NavigationToolWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePosition();
            UpdateNavState();
            UpdateFullScreenUiState();
        }

        private void NavigationToolWindow_Closed(object? sender, EventArgs e)
        {
            CleanupFloatingBallWindow();

            if (_webView?.CoreWebView2 != null)
            {
                _webView.CoreWebView2.ContainsFullScreenElementChanged -= CoreWebView2_ContainsFullScreenElementChanged;
            }
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
                Owner.Title = $"{webView.DocumentTitle}";
                UpdateNavState();
            }
        }

        private void CoreWebView2_ContainsFullScreenElementChanged(object? sender, object e)
        {
            Dispatcher.Invoke(UpdateFullScreenUiState);
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
            CleanupFloatingBallWindow();
            this.Close();
        }

        private void Owner_StateChanged(object? sender, EventArgs e)
        {
            UpdatePosition();
            UpdateFullScreenUiState();
        }

        private void Owner_LocationOrSizeChanged(object? sender, EventArgs e)
        {
            UpdatePosition();
            UpdateFloatingBallPosition();
            UpdateFullScreenUiState();
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
                url = "http://" + url;
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

            if (_isInFullScreenMode)
            {
                PositionNavigationAroundFloatingBall();
                return;
            }

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
            Topmost = false;

            UpdateFloatingBallPosition();
        }

        private bool IsInFullScreenMode()
        {
            return (_webView?.CoreWebView2?.ContainsFullScreenElement ?? false)
                || _owner.WindowState == WindowState.Maximized
                || IsOwnerCoveringScreen();
        }

        private bool IsOwnerCoveringScreen()
        {
            const double tolerance = 2;

            return _owner.Left <= SystemParameters.VirtualScreenLeft + tolerance
                && _owner.Top <= SystemParameters.VirtualScreenTop + tolerance
                && _owner.ActualWidth >= SystemParameters.VirtualScreenWidth - tolerance
                && _owner.ActualHeight >= SystemParameters.VirtualScreenHeight - tolerance;
        }

        private void UpdateFullScreenUiState()
        {
            _isInFullScreenMode = IsInFullScreenMode();

            if (_isInFullScreenMode)
            {
                ShowFloatingBall();

                if (_isToolVisibleInFullScreen)
                {
                    if (!IsVisible)
                    {
                        Show();
                    }

                    UpdatePosition();
                    Activate();
                }
                else if (IsVisible)
                {
                    Hide();
                }

                UpdateFloatingBallButtonLabel();
                return;
            }

            _isToolVisibleInFullScreen = false;
            HideFloatingBall();

            if (!IsVisible)
            {
                Show();
            }

            UpdatePosition();
        }

        private void EnsureFloatingBallWindow()
        {
            if (_floatingBallWindow != null)
            {
                return;
            }

            var button = new Button
            {
                Width = FloatingBallSize,
                Height = FloatingBallSize,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "显示导航栏",
                Focusable = false,
                Template = BuildFloatingBallButtonTemplate()
            };
            button.PreviewMouseLeftButtonDown += FloatingBallButton_PreviewMouseLeftButtonDown;
            button.PreviewMouseMove += FloatingBallButton_PreviewMouseMove;
            button.PreviewMouseLeftButtonUp += FloatingBallButton_PreviewMouseLeftButtonUp;

            var indicator = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 6,
                    ShadowDepth = 0,
                    Opacity = 0.25,
                    Color = Color.FromRgb(255, 255, 255)
                }
            };

            var highlight = new Ellipse
            {
                Width = 18,
                Height = 10,
                Margin = new Thickness(0, 10, 0, 0),
                Fill = new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                RenderTransform = new RotateTransform(-18)
            };

            var content = new Grid
            {
                Width = FloatingBallSize,
                Height = FloatingBallSize
            };
            content.Children.Add(highlight);
            content.Children.Add(indicator);
            button.Content = content;

            var border = new Border
            {
                Width = FloatingBallSize,
                Height = FloatingBallSize,
                CornerRadius = new CornerRadius(FloatingBallSize / 2),
                Background = Brushes.Transparent,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 22,
                    ShadowDepth = 8,
                    Opacity = 0.28,
                    Color = Color.FromRgb(15, 23, 42)
                },
                Child = button
            };

            var floatingBallWindow = new Window
            {
                Width = FloatingBallSize,
                Height = FloatingBallSize,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowActivated = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Focusable = false,
                Content = border
            };

            _floatingBallButton = button;
            _floatingBallIndicator = indicator;
            _floatingBallWindow = floatingBallWindow;
            UpdateFloatingBallButtonLabel();
            UpdateFloatingBallPosition();
        }

        private void ShowFloatingBall()
        {
            EnsureFloatingBallWindow();

            if (_floatingBallWindow != null && !_floatingBallWindow.IsVisible)
            {
                _floatingBallWindow.Show();
            }

            UpdateFloatingBallPosition();
        }

        private void HideFloatingBall()
        {
            if (_floatingBallWindow?.IsVisible == true)
            {
                _floatingBallWindow.Hide();
            }
        }

        private void CleanupFloatingBallWindow()
        {
            if (_floatingBallButton != null)
            {
                _floatingBallButton.PreviewMouseLeftButtonDown -= FloatingBallButton_PreviewMouseLeftButtonDown;
                _floatingBallButton.PreviewMouseMove -= FloatingBallButton_PreviewMouseMove;
                _floatingBallButton.PreviewMouseLeftButtonUp -= FloatingBallButton_PreviewMouseLeftButtonUp;
                _floatingBallButton = null;
            }

            _floatingBallIndicator = null;

            if (_floatingBallWindow != null)
            {
                _floatingBallWindow.Close();
                _floatingBallWindow = null;
            }
        }

        private void UpdateFloatingBallPosition()
        {
            if (_floatingBallWindow == null)
            {
                return;
            }

            if (_floatingBallWindow.IsVisible)
            {
                return;
            }

            var workingArea = SystemParameters.WorkArea;

            _floatingBallWindow.Left = workingArea.Right - FloatingBallSize - FloatingBallMargin;
            _floatingBallWindow.Top = workingArea.Bottom - FloatingBallSize - FloatingBallMargin;
        }

        private ControlTemplate BuildFloatingBallButtonTemplate()
        {
            var grid = new FrameworkElementFactory(typeof(Grid));

            var outerSphere = new FrameworkElementFactory(typeof(Ellipse));
            outerSphere.SetValue(Shape.FillProperty, new RadialGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(147, 197, 253), 0),
                    new GradientStop(Color.FromRgb(59, 130, 246), 0.45),
                    new GradientStop(Color.FromRgb(29, 78, 216), 0.8),
                    new GradientStop(Color.FromRgb(30, 41, 59), 1)
                })
            {
                Center = new Point(0.32, 0.28),
                GradientOrigin = new Point(0.28, 0.24),
                RadiusX = 0.85,
                RadiusY = 0.85
            });
            outerSphere.SetValue(Shape.StrokeProperty, new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)));
            outerSphere.SetValue(Shape.StrokeThicknessProperty, 1.0);

            var innerGlow = new FrameworkElementFactory(typeof(Ellipse));
            innerGlow.SetValue(FrameworkElement.WidthProperty, FloatingBallSize - 14);
            innerGlow.SetValue(FrameworkElement.HeightProperty, FloatingBallSize - 14);
            innerGlow.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            innerGlow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            innerGlow.SetValue(UIElement.OpacityProperty, 0.4);
            innerGlow.SetValue(Shape.FillProperty, new RadialGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(140, 255, 255, 255), 0),
                    new GradientStop(Color.FromArgb(40, 255, 255, 255), 0.55),
                    new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)
                })
            {
                Center = new Point(0.35, 0.3),
                GradientOrigin = new Point(0.3, 0.25),
                RadiusX = 0.8,
                RadiusY = 0.8
            });

            grid.AppendChild(outerSphere);
            grid.AppendChild(innerGlow);

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            grid.AppendChild(presenter);

            return new ControlTemplate(typeof(Button))
            {
                VisualTree = grid
            };
        }

        private void ToggleFloatingBallNavigation()
        {
            _isToolVisibleInFullScreen = !_isToolVisibleInFullScreen;

            if (_isToolVisibleInFullScreen)
            {
                if (!IsVisible)
                {
                    Show();
                }

                UpdatePosition();
                Activate();
            }
            else if (IsVisible)
            {
                Hide();
            }

            UpdateFloatingBallButtonLabel();
        }

        private void UpdateFloatingBallButtonLabel()
        {
            if (_floatingBallButton == null)
            {
                return;
            }

            _floatingBallButton.ToolTip = _isToolVisibleInFullScreen ? "隐藏导航栏" : "显示导航栏";

            if (_floatingBallIndicator != null)
            {
                _floatingBallIndicator.Width = _isToolVisibleInFullScreen ? 18 : 14;
                _floatingBallIndicator.Height = _isToolVisibleInFullScreen ? 18 : 14;
                _floatingBallIndicator.CornerRadius = new CornerRadius(_isToolVisibleInFullScreen ? 9 : 7);
                _floatingBallIndicator.Background = _isToolVisibleInFullScreen
                    ? new SolidColorBrush(Color.FromArgb(245, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
            }
        }

        private void FloatingBallButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_floatingBallWindow == null)
            {
                return;
            }

            _isDraggingFloatingBall = true;
            _suppressFloatingBallClick = false;
            _floatingBallDragStartCursor = _floatingBallWindow.PointToScreen(e.GetPosition(_floatingBallWindow));
            _floatingBallStartPosition = new Point(_floatingBallWindow.Left, _floatingBallWindow.Top);
            _floatingBallButton?.CaptureMouse();
        }

        private void FloatingBallButton_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingFloatingBall || _floatingBallWindow == null || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var cursor = _floatingBallWindow.PointToScreen(e.GetPosition(_floatingBallWindow));
            var offsetX = cursor.X - _floatingBallDragStartCursor.X;
            var offsetY = cursor.Y - _floatingBallDragStartCursor.Y;

            if (!_suppressFloatingBallClick && (Math.Abs(offsetX) > 4 || Math.Abs(offsetY) > 4))
            {
                _suppressFloatingBallClick = true;
            }

            if (!_suppressFloatingBallClick)
            {
                return;
            }

            var workingArea = SystemParameters.WorkArea;
            var newLeft = _floatingBallStartPosition.X + offsetX;
            var newTop = _floatingBallStartPosition.Y + offsetY;

            _floatingBallWindow.Left = Math.Max(workingArea.Left, Math.Min(workingArea.Right - FloatingBallSize, newLeft));
            _floatingBallWindow.Top = Math.Max(workingArea.Top, Math.Min(workingArea.Bottom - FloatingBallSize, newTop));

            if (_isToolVisibleInFullScreen && IsVisible)
            {
                PositionNavigationAroundFloatingBall();
            }
        }

        private void FloatingBallButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var shouldToggle = !_suppressFloatingBallClick;
            _isDraggingFloatingBall = false;
            _floatingBallButton?.ReleaseMouseCapture();
            e.Handled = true;

            if (shouldToggle)
            {
                ToggleFloatingBallNavigation();
            }

            _suppressFloatingBallClick = false;
        }

        private void PositionNavigationAroundFloatingBall()
        {
            if (_floatingBallWindow == null)
            {
                return;
            }

            var workingArea = SystemParameters.WorkArea;
            var navWidth = ActualWidth > 0 ? ActualWidth : Width;
            var navHeight = ActualHeight > 0 ? ActualHeight : Height;
            var ballCenterX = _floatingBallWindow.Left + (FloatingBallSize / 2);
            var showOnLeft = ballCenterX >= workingArea.Left + (workingArea.Width / 2);
            var targetLeft = showOnLeft
                ? _floatingBallWindow.Left - navWidth - NavigationSpacing
                : _floatingBallWindow.Left + FloatingBallSize + NavigationSpacing;
            var targetTop = _floatingBallWindow.Top + ((FloatingBallSize - navHeight) / 2);

            Left = Math.Max(workingArea.Left, Math.Min(workingArea.Right - navWidth, targetLeft));
            Top = Math.Max(workingArea.Top, Math.Min(workingArea.Bottom - navHeight, targetTop));
            Topmost = true;
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
