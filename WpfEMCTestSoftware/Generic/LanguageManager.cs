using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Globalization;
using System.Windows;

namespace WpfEMCTestSoftware
{
    public partial class LanguageManager : ObservableObject
    {
        // 单例
        private static readonly Lazy<LanguageManager> _instance =
            new Lazy<LanguageManager>(() => new LanguageManager());
        public static LanguageManager Instance => _instance.Value;

        [ObservableProperty]
        private CultureInfo _currentCulture;

        private LanguageManager()
        {
            CurrentCulture = CultureInfo.CurrentUICulture;
        }

        /// <summary>
        /// 切换语言：移除旧语言资源，添加新语言资源（扁平化覆盖）
        /// </summary>
        [RelayCommand]
        public void SwitchLanguage(string cultureName)
        {
            try
            {
                var rootDict = Application.Current.Resources as ResourceDictionary;
                if (rootDict == null) return;

                // 1. 移除所有语言资源文件（通过路径特征匹配）
                var langDicts = rootDict.MergedDictionaries
                    .Where(d =>
                    {
                        var src = d.Source?.ToString() ?? "";
                        return src.Contains("zh-CN.xaml") || src.Contains("en-US.xaml");
                    })
                    .ToList();
                foreach (var dict in langDicts)
                {
                    rootDict.MergedDictionaries.Remove(dict);
                }

                // 2. 添加新语言资源（直接合并到全局，设计器/运行时都能识别）
                var newLangDict = new ResourceDictionary
                {
                    Source = new Uri($"/WpfEMCTestSoftware;component/Resources/{cultureName}.xaml",
                        UriKind.RelativeOrAbsolute)
                };
                rootDict.MergedDictionaries.Add(newLangDict);

                // 3. 更新线程文化
                CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
                CultureInfo.CurrentCulture = new CultureInfo(cultureName);

                Debug.WriteLine($"切换到{cultureName}语言成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"语言切换失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 快捷命令
        [RelayCommand]
        public void SwitchToChinese() => SwitchLanguage("zh-CN");

        [RelayCommand]
        public void SwitchToEnglish() => SwitchLanguage("en-US");
    }
}
