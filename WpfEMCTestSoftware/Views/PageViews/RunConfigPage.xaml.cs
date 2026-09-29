using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Company.UI.Controls.UseControls;
using WpfEMCTestSoftware.ViewModels;

namespace WpfEMCTestSoftware.Views
{
    /// <summary>
    /// RunConfigPage.xaml 的交互逻辑
    /// </summary>
    public partial class RunConfigPage : Page
    {
        /// <summary>防重复挂钩（页面每次 Loaded 都会触发）</summary>
        private bool _logAutoScrollHooked;

        public RunConfigPage()
        {
            InitializeComponent();

            //左侧"运行日志"自动滚动：新日志进来自动滚到最后一行（操作员始终看到最新动作）
            //挂钩 ItemCollection（它会把 ItemsSource 的 CollectionChanged 转发出来），
            //BaseTasks 整体替换时集合实例变了也不用重新挂钩
            this.Loaded += (_, _) =>
            {
                if (_logAutoScrollHooked) return;
                _logAutoScrollHooked = true;
                if (RunLogList?.Items is INotifyCollectionChanged incc)
                {
                    incc.CollectionChanged += (_, _) =>
                    {
                        if (RunLogList.Items.Count > 0)
                            RunLogList.ScrollIntoView(RunLogList.Items[RunLogList.Items.Count - 1]);
                    };
                }

                //把"抓取所有曲线图位图"的能力注入给 ViewModel（图表控件在可视化树里，只有 View 能拿到）
                if (DataContext is RunConfigViewModel vm)
                    vm.CaptureChartImages = CaptureCharts;
            };
        }

        /// <summary>
        /// 遍历图表区可视化树，收集所有 UserChart 的已渲染 PNG（用于合并导出 / 报告嵌入）。
        /// 必须在 UI 线程调用（访问 ActualWidth / 渲染位图）。
        /// </summary>
        public List<ChartCapture> CaptureCharts()
        {
            var result = new List<ChartCapture>();
            if (ChartItemsControl == null)
                return result;
            var userCharts = new List<UserChart>();
            FindVisualChildren(ChartItemsControl, userCharts);
            foreach (var uc in userCharts)
            {
                var png = uc.ExportToPng();
                if (png != null && png.Length > 0)
                    result.Add(new ChartCapture { Title = uc.ChartTitle ?? "", Png = png });
            }
            return result;
        }

        private static void FindVisualChildren<T>(DependencyObject parent, List<T> result) where T : DependencyObject
        {
            if (parent == null) return;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t)
                    result.Add(t);
                FindVisualChildren(child, result);
            }
        }
    }
}
