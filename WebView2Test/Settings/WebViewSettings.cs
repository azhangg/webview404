namespace WebView2Test.Settings
{
    public class WebViewSettings
    {
        public WebViewSettings()
        {
        }


        /// <summary>
        /// 开发模式：启用开发工具和调试功能，适用于开发阶段；生产模式则禁用这些功能以提升安全性和性能。
        /// </summary>
        public bool DevelopmentMode { get; set; }

        /// <summary>
        /// 域名配置，用于指定WebView加载的默认域名。
        /// </summary>
        public string Domain { get; set; } = "http://127.0.0.1";
    }
}
