using CommunityToolkit.Mvvm.ComponentModel;
using Company.UI.Controls.UseControls;
using System.Collections.ObjectModel;
using WpfEMCTestSoftware.RunTasks;

namespace WpfEMCTestSoftware.Models.DisplayModules
{
    /// <summary>
    /// 图表类显示模块基类：包装一条/一组测试曲线
    /// Lines 直接引用 BaseRunTask 中的曲线集合（赋引用，不拷贝），保证实时刷新
    /// </summary>
    public abstract partial class ChartModuleBase : ObservableObject, ITestDisplayModule
    {
        public abstract string ModuleKey { get; }

        [ObservableProperty]
        private string _title = "";

        [ObservableProperty]
        private string _yAxisTitle = "";

        /// <summary>X轴是否对数显示</summary>
        [ObservableProperty]
        private bool _isLogX;

        /// <summary>曲线集合（Bind 时指向任务里的集合引用）</summary>
        [ObservableProperty]
        private ObservableCollection<ChartLineModel>? _lines;

        /// <summary>Y 轴下限（NaN=自动按数据自适应）。
        /// 经 DataTemplate 绑到 UserChart.YMinLimit：固定量程来自 ModuleCatalog 描述
        /// （如 信号源 -20~20 dBm、功率 0~100 W）；跟限制线联动的图由 ChannelChartModule 运行时改写</summary>
        [ObservableProperty]
        private double _yMinLimit = double.NaN;

        /// <summary>Y 轴上限（NaN=自动），语义同 YMinLimit</summary>
        [ObservableProperty]
        private double _yMaxLimit = double.NaN;

        public virtual void Bind(BaseRunTask task)
        {
            Lines = ProvideLines(task);
        }

        public virtual void Unbind()
        {
            Lines = null;
        }

        /// <summary>子类指定本模块显示任务中的哪条曲线集合</summary>
        protected abstract ObservableCollection<ChartLineModel>? ProvideLines(BaseRunTask task);
    }
}
