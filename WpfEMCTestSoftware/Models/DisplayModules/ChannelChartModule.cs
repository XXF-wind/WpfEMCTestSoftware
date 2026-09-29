using Company.UI.Controls.UseControls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using WpfEMCTestSoftware.RunTasks;

namespace WpfEMCTestSoftware.Models.DisplayModules
{
    /// <summary>
    /// 通用曲线图表模块：**不针对任何具体测试项**
    /// 数据来源 = 运行时从 BaseRunTask.CurveChannels[ModuleKey] 取集合
    /// 作用：新增一条监测曲线时，只要 RunTask 注册通道 + 目录里加一条描述 + 配置表加一个 Key，
    ///       不需要新建模块类，也不需要新增 XAML 模板
    /// Y 轴范围两级来源（优先级从高到低）：
    ///   1) ModuleCatalog 描述里配了 YMin/YMax（如 信号源 -20~20、功率 0~100）→ 全程固定；
    ///   2) 没配（NaN）但集合里有 IsYReferenceLine 标记的参考线（如注入电平图的 limit 红虚线）
    ///      → 按参考线 Y 值动态推算：上限 = 参考值 + 余量、下限 = 0，参考线整组替换时自动重算；
    ///   3) 都没有 → NaN = UserChart 自动模式（按数据 min/max 自适应）。
    /// </summary>
    public class ChannelChartModule : ChartModuleBase
    {
        private readonly string _moduleKey;

        /// <summary>目录描述里的固定 Y 范围（NaN=自动），Unbind 时用它复位</summary>
        private readonly double _fixedYMin;
        private readonly double _fixedYMax;

        /// <summary>参考线推算：Y 上限 = 参考值 + 该余量（dB），保证 limit 线始终完整可见</summary>
        private const double ReferenceHeadroom = 10.0;
        /// <summary>参考线推算：Y 下限固定 0（dBμA 物理量无负值，也避免轴自动下探到负区）</summary>
        private const double ReferenceFloor = 0.0;

        /// <summary>当前监听的参考线模型（Bind 时挂、Unbind 时摘，防止事件泄漏）</summary>
        private ChartLineModel? _watchedReference;

        public ChannelChartModule(string moduleKey, string title, string yAxisTitle,
            bool isLogX = false, double yMin = double.NaN, double yMax = double.NaN)
        {
            _moduleKey = moduleKey;
            Title = title;
            YAxisTitle = yAxisTitle;
            IsLogX = isLogX;
            _fixedYMin = yMin;
            _fixedYMax = yMax;
            YMinLimit = yMin;
            YMaxLimit = yMax;
        }

        public override string ModuleKey => _moduleKey;

        protected override ObservableCollection<ChartLineModel>? ProvideLines(BaseRunTask task)
        {
            if (task?.CurveChannels.TryGetValue(_moduleKey, out var lines) == true)
                return lines;
            return null;   //通道未注册：图表显示为空，不影响其他模块
        }

        public override void Bind(BaseRunTask task)
        {
            base.Bind(task);
            WatchReferenceLine();
        }

        public override void Unbind()
        {
            //先摘事件（要旧 Lines 引用，必须在 Lines=null 之前），再复位范围，最后清集合
            UnwatchReferenceLine();
            YMinLimit = _fixedYMin;   //复位为目录固定值，避免残留上次测试推算出的范围
            YMaxLimit = _fixedYMax;
            base.Unbind();
        }

        // -------------------- 参考线 → Y 轴范围联动 --------------------

        private void WatchReferenceLine()
        {
            UnwatchReferenceLine();
            var reference = Lines?.FirstOrDefault(l => l.IsYReferenceLine);
            if (reference == null) return;   //无参考线：保持目录固定范围 / 自动模式
            _watchedReference = reference;
            reference.PropertyChanged += OnReferencePropertyChanged;
            ApplyRangeFromReference();       //Bind 时参考线已有数据则先推算一次
        }

        private void UnwatchReferenceLine()
        {
            if (_watchedReference == null) return;
            _watchedReference.PropertyChanged -= OnReferencePropertyChanged;
            _watchedReference = null;
        }

        private void OnReferencePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            //参考线的更新方式是整组替换 XData/YData（setter 发 PropertyChanged）；
            //只认 YData 一次，XData/Title 等变化不重算，减少无谓重绘
            if (e.PropertyName != nameof(ChartLineModel.YData)) return;
            ApplyRangeFromReference();
        }

        /// <summary>按参考线当前 Y 值推算 Y 轴范围；无有效值时保持 NaN（自动模式，不乱设轴）。
        /// ★ 取最大值而非第一个值：limit 参考线是"全频段限值折线"（各频段等级不同，随频率爬升），
        ///   取第一个（最低频段）值会让高频段折线画出可视区外。</summary>
        private void ApplyRangeFromReference()
        {
            double? referenceValue = null;
            var yData = _watchedReference?.YData;
            if (yData != null)
            {
                foreach (var v in yData)
                {
                    if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                    referenceValue = referenceValue == null ? v : Math.Max(referenceValue.Value, v);
                }
            }
            if (referenceValue == null) return;
            YMaxLimit = referenceValue.Value + ReferenceHeadroom;
            YMinLimit = ReferenceFloor;
        }
    }
}
