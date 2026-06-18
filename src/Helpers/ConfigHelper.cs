using Microsoft.Extensions.Configuration;

namespace WebView2Test.Helpers
{
    public static class ConfigHelper
    {
        private static IConfiguration _configuration;

        static ConfigHelper()
        {
            _configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
        }

        /// <summary>
        /// 读取单个配置项
        /// </summary>
        /// <typeparam name="T">配置项类型</typeparam>
        /// <param name="key">配置键（支持分层，用:分隔）</param>
        /// <returns>配置值</returns>
        public static T? GetValue<T>(string key)
        {
            return _configuration.GetValue<T>(key);
        }

        /// <summary>
        /// 读取分层配置到实体类
        /// </summary>
        /// <typeparam name="T">实体类类型</typeparam>
        /// <returns>实体类对象</returns>
        /// <remarks>实体名称必须和配置节点名称一致</remarks>
        public static T GetSetting<T>() where T : new()
        {
            var section = new T();
            var sectionName = typeof(T).Name;
            _configuration.GetSection(sectionName).Bind(section);
            return section;
        }

        /// <summary>
        /// 获取配置根对象（自定义扩展用）
        /// </summary>
        public static IConfiguration GetConfigRoot()
        {
            return _configuration;
        }
    }
}
