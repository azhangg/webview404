
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebView2Test.HostObjects
{
    public class FileBlobInfo
    {
        public string? Base64Data { get; set; }
        public string? MimeType { get; set; }
        public string? FileName { get; set; }
        public long FileSize { get; set; }
    }

    public class FileApi
    {
        public FileApi()
        {
        }

        /// <summary>
        /// 通过file协议/本地路径读取文件，返回Base64格式（供JS转Blob）
        /// </summary>
        /// <param name="filePath">支持：file:///C:/test.png 或 C:\test.png</param>
        /// <returns>包含Base64和MIME的文件信息</returns>
        public async Task<string?> GetFileAsBase64Async(string filePath)
        {
            try
            {
                // 处理file://协议路径，转为本地绝对路径
                string localPath = filePath;
                if (localPath.StartsWith("file://"))
                {
                    // 移除file://前缀，处理跨平台路径分隔符
                    localPath = localPath.Replace("file:///", "").Replace('/', '\\');
                    // 解码URL编码的路径（如空格转为%20的情况）
                    localPath = Uri.UnescapeDataString(localPath);
                }

                // 校验文件是否存在
                if (!File.Exists(localPath))
                {
                    throw new FileNotFoundException("文件不存在", localPath);
                }

                // 读取文件二进制流
                byte[] fileBytes = await File.ReadAllBytesAsync(localPath);
                // 转换为Base64字符串
                string base64 = Convert.ToBase64String(fileBytes);
                // 获取文件MIME类型
                string mimeType = GetMimeType(localPath);

                // 返回文件信息
                return JsonSerializer.Serialize(new FileBlobInfo
                {
                    Base64Data = base64,
                    MimeType = mimeType,
                    FileName = Path.GetFileName(localPath),
                    FileSize = fileBytes.Length
                });
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 获取文件MIME类型（简化版，可扩展更多类型）
        /// </summary>
        private string GetMimeType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return extension switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".pdf" => "application/pdf",
                ".txt" => "text/plain",
                ".json" => "application/json",
                _ => "application/octet-stream", // 未知类型
            };
        }
    }
}
