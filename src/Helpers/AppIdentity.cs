using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace WebView2Test.Helpers
{
    /// <summary>
    /// 为当前进程设置「每版唯一」的 AppUserModelID。
    /// <para>
    /// 未声明 AppID 时，Windows 会用 exe 的完整路径派生一个隐式 AppID，任务栏按这个身份记住按钮图标。
    /// 原地覆盖同一个 exe（路径不变）会继续命中旧记录，表现为标题栏图标已更新、任务栏却还是旧的；
    /// 把产物拷到一个新目录就不会有问题，因为路径变了、AppID 也就变了。
    /// </para>
    /// <para>
    /// 这里显式指定一个「实例 + 本次构建」都不同的 AppID，等价于每次发布自动换一个新目录，
    /// 同时保证同一台机器上的多个实例（opencode / 医生端 / 客户端 / 浏览器）不会合并成一个任务栏按钮。
    /// </para>
    /// </summary>
    internal static class AppIdentity
    {
        private const string Prefix = "WebView404";

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SetCurrentProcessExplicitAppUserModelID(
            [MarshalAs(UnmanagedType.LPWStr)] string appId);

        /// <summary>
        /// 设置进程 AppID。必须在创建任何窗口之前调用，否则任务栏已经用旧身份建好按钮了。
        /// </summary>
        public static void Apply()
        {
            try
            {
                SetCurrentProcessExplicitAppUserModelID(Build());
            }
            catch (Exception)
            {
                // 设置失败时退回系统默认的隐式 AppID，不影响程序启动
            }
        }

        /// <summary>
        /// 生成形如 WebView404.{实例}.{构建} 的 AppID。
        /// 实例：exe 所在目录路径的哈希（同一目录每次运行保持一致）。
        /// 构建：程序集版本 + exe 写入时间的哈希（每次发布都会变）。
        /// </summary>
        private static string Build()
        {
            var (exePath, baseDir) = ResolveLocations();

            var instance = ShortHash(baseDir.ToLowerInvariant());
            var build = ShortHash($"{InformationalVersion()}|{SafeWriteTicks(exePath)}");

            return $"{Prefix}.{instance}.{build}";
        }

        /// <summary>
        /// 取真实 exe 路径与它所在的部署目录。
        /// 单文件发布时 <see cref="Environment.ProcessPath"/> 就是 exe；
        /// 用 dotnet 托管启动时它指向 dotnet.exe，此时退回程序集所在目录。
        /// </summary>
        private static (string ExePath, string BaseDir) ResolveLocations()
        {
            var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            var processPath = Environment.ProcessPath;

            if (string.IsNullOrEmpty(processPath))
            {
                return (string.Empty, baseDir);
            }

            var name = Path.GetFileNameWithoutExtension(processPath);
            if (string.Equals(name, "dotnet", StringComparison.OrdinalIgnoreCase))
            {
                return (string.Empty, baseDir);
            }

            return (processPath, Path.GetDirectoryName(processPath) ?? baseDir);
        }

        private static string InformationalVersion()
        {
            return Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "0.0.0";
        }

        private static long SafeWriteTicks(string exePath)
        {
            if (string.IsNullOrEmpty(exePath))
            {
                return 0;
            }

            try
            {
                return File.GetLastWriteTimeUtc(exePath).Ticks;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static string ShortHash(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes, 0, 4).ToLowerInvariant();
        }
    }
}
