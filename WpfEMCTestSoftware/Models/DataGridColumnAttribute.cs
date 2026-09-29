using System.Windows;

namespace WpfEMCTestSoftware.Models
{
    // 增强型列配置特性（支持更多属性）
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DataGridColumnSettingAttribute : Attribute
    {
        /// <summary>
        /// 中文标题
        /// </summary>
        public string ChineseTitle { get; }

        /// <summary>
        /// 是否显示（支持动态变更）
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// 列宽度（支持固定值、Auto、* 格式，如 "100"、"Auto"、"*"）
        /// </summary>
        public string Width { get; set; } = "Auto";

        /// <summary>
        /// 水平对齐方式
        /// </summary>
        public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;

        public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Center;
        /// <summary>
        /// 是否允许排序
        /// </summary>
        public bool CanSort { get; set; } = true;

        public DataGridColumnSettingAttribute(string chineseTitle)
        {
            ChineseTitle = chineseTitle;
        }
    }
}
