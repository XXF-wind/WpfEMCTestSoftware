using System.IO;
using System.Windows;

namespace WpfEMCTestSoftware.Core
{
    /// <summary>
    /// 主题管理器 — 支持浅色 / 深色 / 海洋蓝 三套主题切换
    /// 通过替换 App.ResourceDictionary 中的主题 ResourceDictionary 实现动态切换
    /// </summary>
    public static class ThemeManager
    {
        private const string ThemeConfigFile = "theme.config";
        private static readonly string ThemeConfigPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ThemeConfigFile);

        /// <summary>
        /// 当前主题名称
        /// </summary>
        public static string CurrentTheme { get; private set; } = "Light";

        /// <summary>
        /// 支持的主题列表（名称 / 显示名）
        /// </summary>
        public static readonly (string Key, string DisplayName)[] AvailableThemes =
        [
            ("Light", "浅色"),
            ("Dark", "深色"),
            ("Ocean", "海洋蓝"),
        ];

        /// <summary>
        /// 初始化主题（程序启动时调用）
        /// 优先读取持久化配置，否则默认 Light
        /// </summary>
        public static void Initialize()
        {
            string savedTheme = LoadSavedTheme();
            ApplyTheme(savedTheme);
        }

        /// <summary>
        /// 切换主题
        /// </summary>
        public static void SwitchTheme(string themeKey)
        {
            if (CurrentTheme == themeKey) return;
            ApplyTheme(themeKey);
            SaveTheme(themeKey);
        }

        /// <summary>
        /// 应用主题 — 替换 App 级资源字典
        /// </summary>
        private static void ApplyTheme(string themeKey)
        {
            var app = Application.Current;
            if (app == null) return;

            var dicts = app.Resources.MergedDictionaries;

            // 找到并移除旧的主题字典（key 以 "Theme_" 开头标记的）
            ResourceDictionary? oldTheme = null;
            foreach (var d in dicts)
            {
                if (d is ResourceDictionary rd && rd.Contains("__ThemeKey"))
                {
                    oldTheme = rd;
                    break;
                }
            }

            // 加载新主题
            var newTheme = new ResourceDictionary
            {
                Source = new Uri(
                    $"pack://application:,,,/WpfEMCTestSoftware;component/Resources/Themes/{themeKey}Theme.xaml",
                    UriKind.Absolute)
            };
            // 标记此字典为主题字典，方便后续定位替换
            newTheme["__ThemeKey"] = themeKey;

            if (oldTheme != null)
            {
                // 替换：在旧主题的位置插入新主题
                int index = dicts.IndexOf(oldTheme);
                dicts.RemoveAt(index);
                dicts.Insert(index, newTheme);
            }
            else
            {
                // 首次加载：插到末尾
                dicts.Add(newTheme);
            }

            // 同步 MaterialDesign 基主题
            SyncMaterialDesignBase(themeKey);

            CurrentTheme = themeKey;
        }

        /// <summary>
        /// 根据主题切换 MaterialDesign 的 Light/Dark 基主题
        /// </summary>
        private static void SyncMaterialDesignBase(string themeKey)
        {
            var app = Application.Current;
            if (app == null) return;

            var dicts = app.Resources.MergedDictionaries;

            // Light vs Dark 需要不同的 MaterialDesign 基础字典
            bool isDark = themeKey == "Dark" || themeKey == "Ocean";
            string newMdSource = isDark
                ? "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Dark.xaml"
                : "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml";

            // 找到并替换 MaterialDesign 基主题
            for (int i = 0; i < dicts.Count; i++)
            {
                var src = dicts[i].Source?.ToString() ?? "";
                if (src.Contains("MaterialDesignTheme.Light") ||
                    src.Contains("MaterialDesignTheme.Dark"))
                {
                    dicts.RemoveAt(i);
                    dicts.Insert(i, new ResourceDictionary
                    {
                        Source = new Uri(newMdSource, UriKind.Absolute)
                    });
                    break;
                }
            }
        }

        /// <summary>
        /// 获取下一个主题（循环切换用）
        /// </summary>
        public static string GetNextTheme()
        {
            for (int i = 0; i < AvailableThemes.Length; i++)
            {
                if (AvailableThemes[i].Key == CurrentTheme)
                {
                    int next = (i + 1) % AvailableThemes.Length;
                    return AvailableThemes[next].Key;
                }
            }
            return "Light";
        }

        /// <summary>
        /// 保存主题到本地配置文件
        /// </summary>
        private static void SaveTheme(string themeKey)
        {
            try
            {
                File.WriteAllText(ThemeConfigPath, themeKey);
            }
            catch
            {
                // 静默失败：不影响主流程
            }
        }

        /// <summary>
        /// 从本地配置读取上次使用的主题
        /// </summary>
        private static string LoadSavedTheme()
        {
            try
            {
                if (File.Exists(ThemeConfigPath))
                {
                    string saved = File.ReadAllText(ThemeConfigPath).Trim();
                    foreach (var t in AvailableThemes)
                    {
                        if (t.Key == saved) return saved;
                    }
                }
            }
            catch { }
            return "Dark";
        }
    }
}
