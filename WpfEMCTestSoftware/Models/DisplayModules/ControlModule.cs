using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;
using System.Windows.Media;

namespace WpfEMCTestSoftware.Models.DisplayModules
{
    /// <summary>
    /// 附加操作按钮模块：主按钮（开始/停止/暂停/保存）所有测试通用、写死在页面上；
    /// 这里的按钮按测试类型配置（EMI 有"接收机截图/曲线居中"，辐射类有"改变极化"，等等）。
    /// 点击后统一走 RunConfigViewModel.ModuleControlCommand，按 ModuleKey 分发。
    /// </summary>
    public partial class ControlModule : ObservableObject
    {
        private static readonly BrushConverter _brushConverter = new();

        public string ModuleKey { get; }

        [ObservableProperty]
        private string _label = "";

        public PackIconKind Icon { get; }

        public Brush Foreground { get; }

        public ControlModule(string key, string label, PackIconKind icon, string foreground)
        {
            ModuleKey = key;
            Label = label;
            Icon = icon;
            Foreground = string.IsNullOrEmpty(foreground)
                ? Brushes.Black
                : (Brush)(_brushConverter.ConvertFromString(foreground) ?? Brushes.Black);
        }
    }
}
