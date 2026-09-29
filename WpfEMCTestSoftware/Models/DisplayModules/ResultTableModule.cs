using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using WpfEMCTestSoftware.RunTasks;

namespace WpfEMCTestSoftware.Models.DisplayModules
{
    /// <summary>
    /// 结果表的一行：单元格文本列表 + 判定高亮
    /// 列数由表模块的 Columns 决定，行与列完全数据驱动
    /// </summary>
    public partial class ResultRow : ObservableObject
    {
        /// <summary>行号（从 1 开始，用于左侧序号列）</summary>
        public int No { get; set; }

        /// <summary>单元格内容（与 Columns 一一对应）</summary>
        public ObservableCollection<string> Cells { get; } = new();

        /// <summary>超标（行整体标红）</summary>
        [ObservableProperty]
        private bool _isOverLimit;

        /// <summary>临界（余量 &lt; 3dB，行整体标黄）</summary>
        [ObservableProperty]
        private bool _isCritical;

        public ResultRow(params string[] cells)
        {
            foreach (var c in cells)
                Cells.Add(c);
        }
    }

    /// <summary>
    /// 表格数据：RunTask 提供，表模块只负责渲染
    /// </summary>
    public class ResultTableData
    {
        public string Key { get; init; } = "";
        public string Title { get; init; } = "";
        public string[] Columns { get; init; } = [];

        /// <summary>行集合（RunTask 持续往里塞数据，界面自动刷新）</summary>
        public ObservableCollection<ResultRow> Rows { get; } = new();

        public static ResultTableData Of(string key, string title, string[] columns)
            => new() { Key = key, Title = title, Columns = columns };
    }

    /// <summary>
    /// 表格显示模块：结果判定表 / 系统校验比对表都用它
    /// 列头来自 ModuleCatalog，行来自 RunTask 的 ResultTableData（共享集合引用）
    /// </summary>
    public partial class ResultTableModule : ObservableObject, ITestDisplayModule
    {
        public string ModuleKey { get; }

        [ObservableProperty]
        private string _title = "";

        /// <summary>列头</summary>
        public ObservableCollection<string> Columns { get; } = new();

        /// <summary>数据行</summary>
        public ObservableCollection<ResultRow> Rows { get; } = new();

        /// <summary>单元格宽度（各表统一，保证表头与数据对齐）</summary>
        public double CellWidth { get; init; } = 92;

        public ResultTableModule(string key, string title, string[] columns)
        {
            ModuleKey = key;
            Title = title;
            foreach (var c in columns)
                Columns.Add(c);
        }

        public void Bind(BaseRunTask task)
        {
            if (task is not IPanelDataProvider provider)
                return;

            var data = provider.GetResultTables().FirstOrDefault(t => t.Key == ModuleKey);
            if (data == null)
                return;

            if (!string.IsNullOrEmpty(data.Title))
                Title = data.Title;

            // 列头以任务提供的为准（任务可以按测试项追加列，比如 EMI 追加"检波器"）
            if (data.Columns.Length > 0)
            {
                Columns.Clear();
                foreach (var c in data.Columns)
                    Columns.Add(c);
            }

            // ★ 关键：Rows 是共享引用，任务往里面加数据，界面自动刷新
            Rows.Clear();
            foreach (var r in data.Rows)
                Rows.Add(r);
        }

        public void Unbind() => Rows.Clear();
    }
}
