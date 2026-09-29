using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfEMCTestSoftware.Models
{
    /// <summary>
    /// 参数摘要条目：RunConfigPage 右侧参数面板（名称/值 两列）
    /// </summary>
    public partial class ParamItem : ObservableObject
    {
        public ParamItem(string name, string value)
        {
            _name = name;
            _value = value;
        }

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _value;
    }
}
