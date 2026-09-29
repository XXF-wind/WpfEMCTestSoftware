using CommunityToolkit.Mvvm.Input;
using Company.UI.Controls;
using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models.EventMessages;

namespace WpfEMCTestSoftware.ViewModels
{
    /// <summary>
    /// 辅助工具卡片项（图标 + 名称 + 说明 + 标签 + 目标页面索引）
    /// </summary>
    public class SupportToolItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public PackIconKind Icon { get; set; }
        public int PageIndex { get; set; }
    }

    /// <summary>
    /// 辅助工具页 ViewModel：维护测试工具列表，处理卡片点击跳转
    /// </summary>
    public partial class SupportToolViewModel : BaseViewModel
    {
        /// <summary>工具卡片集合</summary>
        public ObservableCollection<SupportToolItem> Tools { get; } = [];

        public SupportToolViewModel()
        {
            Tools.Add(new SupportToolItem
            {
                Title = "开关相位测试",
                Description = "验证开关矩阵各通道的相位一致性，快速定位相位偏差异常的通道，确保链路相位稳定。",
                Tag = "基础校验",
                Icon = PackIconKind.Refresh,
                PageIndex = 2001,
            });
            Tools.Add(new SupportToolItem
            {
                Title = "移相调幅衰减测试",
                Description = "对移相、调幅与衰减通道进行综合标定，输出链路性能数据与标定报告。",
                Tag = "信号校准",
                Icon = PackIconKind.SquareOutline,
                PageIndex = 2002,
            });
            Tools.Add(new SupportToolItem
            {
                Title = "通道连通性测试",
                Description = "诊断矩阵通道的通断与隔离度，自动生成连通性检测报告。",
                Tag = "链路诊断",
                Icon = PackIconKind.SourceBranch,
                PageIndex = 2003,
            });
            Tools.Add(new SupportToolItem
            {
                Title = "设备自检",
                Description = "一键自检系统已连接设备的工作状态，确保测试环境准备就绪。",
                Tag = "系统维护",
                Icon = PackIconKind.ShieldCheck,
                PageIndex = 2004,
            });
        }

        /// <summary>点击工具卡片 → 跳转到对应测试页面</summary>
        [RelayCommand]
        private void Navigate(SupportToolItem? item)
        {
            if (item == null)
                return;
            Messenger.Send(new UniversalIntMessage(item.PageIndex), (int)IntMessageToken.SwitchPage);
        }

        /// <summary>添加测试项（预留）</summary>
        [RelayCommand]
        private void AddTool()
        {
            FlyoutNotication.ShowNotication("自定义测试项功能正在开发中，敬请期待", "辅助工具");
        }
    }
}
