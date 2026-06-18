namespace WebView2Test.Settings
{
    public class AppSettings
    {
        public AppSettings()
        {
        }

        public string Name { get; set; } = "404";
        public string Theme { get; set; } = "Light";
        public int Height { get; set; }
        public int Width { get; set; }
        public int MinHeight { get; set; }
        public int MinWidth { get; set; }
        public int MaxHeight { get; set; }
        public int MaxWidth { get; set; }
    }
}
