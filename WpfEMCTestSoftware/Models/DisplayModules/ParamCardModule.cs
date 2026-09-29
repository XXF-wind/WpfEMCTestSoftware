using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using WpfEMCTestSoftware.RunTasks;

namespace WpfEMCTestSoftware.Models.DisplayModules
{
    /// <summary>
    /// 参数组：一张参数卡的内容（标题 + 键值对）
    /// 由 RunTask 提供，页面和卡片都不认识具体测试项
    /// </summary>
    public partial class ParamGroup : ObservableObject
    {
        public string Key { get; init; } = "";

        [ObservableProperty]
        private string _title = "";

        public ObservableCollection<ParamItem> Items { get; } = new();

        /// <summary>快捷构造：ParamGroup.Of("Card.Level", "电平检测", ("检测方式","Sensor"), ...)</summary>
        public static ParamGroup Of(string key, string title, params (string Name, string Value)[] items)
        {
            var g = new ParamGroup { Key = key, Title = title };
            foreach (var (name, value) in items)
                g.Items.Add(new ParamItem(name, value));
            return g;
        }

        /// <summary>
        /// 按名称写入/更新键值对（保留原有 ParamItem 实例，实时刷新时不闪、不重建）
        /// </summary>
        public void Set(params (string Name, string Value)[] items)
        {
            foreach (var (name, value) in items)
            {
                var exist = Items.FirstOrDefault(i => i.Name == name);
                if (exist == null)
                    Items.Add(new ParamItem(name, value));
                else
                    exist.Value = value;
            }
        }
    }

    /// <summary>
    /// 面板数据提供者：RunTask 实现它，向参数卡 / 结果表提供"要显示什么"
    /// 好处：新增测试项时只改 RunTask，页面与卡片代码零改动
    /// </summary>
    public interface IPanelDataProvider
    {
        /// <summary>本测试项要显示的参数组（卡片按 Key 取用）</summary>
        IEnumerable<ParamGroup> GetParamGroups();

        /// <summary>本测试项要显示的表格数据（结果表 / 校验比对表）</summary>
        IEnumerable<ResultTableData> GetResultTables();
    }

    /// <summary>
    /// 参数卡模块：只负责把 ParamGroup 画成键值对列表
    /// </summary>
    public partial class ParamCardModule : ObservableObject, ITestDisplayModule
    {
        public string ModuleKey { get; }

        [ObservableProperty]
        private string _title = "";

        /// <summary>卡片内容（与 RunTask 的 ParamGroup.Items 共享同一集合引用，实时刷新）</summary>
        [ObservableProperty]
        private ObservableCollection<ParamItem> _items = new();

        public ParamCardModule(string key, string title)
        {
            ModuleKey = key;
            Title = title;
        }

        public void Bind(BaseRunTask task)
        {
            //检查这个任务是不是IPanelDataProvider
            if (task is not IPanelDataProvider provider)
            {
                Items = new ObservableCollection<ParamItem> { new("状态", "任务未提供参数") };
                return;
            }
            //
            var group = provider.GetParamGroups().FirstOrDefault(g => g.Key == ModuleKey);
            if (group == null)
            {
                // 未配置：卡片仍然显示，方便一眼看出"这个测试还没接参数"，而不是空白页
                Items = new ObservableCollection<ParamItem> { new("状态", "未配置") };
                return;
            }

            if (!string.IsNullOrEmpty(group.Title))
                Title = group.Title;
            Items = group.Items;
        }

        public void Unbind() => Items = new ObservableCollection<ParamItem>();
    }
}
