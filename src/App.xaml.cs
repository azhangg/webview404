using System.Windows;
using WebView2Test.Helpers;

namespace WebView2Test
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 必须在创建任何窗口之前设置 AppID，否则任务栏会先用旧身份建好按钮
            AppIdentity.Apply();

            base.OnStartup(e);

            new MainWindow().Show();
        }
    }

}
