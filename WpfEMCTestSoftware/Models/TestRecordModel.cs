using System.Collections.ObjectModel;
namespace WpfEMCTestSoftware.Models
{
    public class TestRecordModel
    {
        /// <summary>
        /// 测试名称
        /// </summary>
        public string? TestName { get; set; }
        /// <summary>
        /// 报告编号
        /// </summary>
        public string? ReportID { get; set; }
        /// <summary>
        /// 频段
        /// </summary>
        public string? FrequencyBand { get; set; }
        /// <summary>
        /// 测试类型
        /// </summary>
        public string? TestType { get; set; }
        /// <summary>
        /// 测试日期
        /// </summary>
        public string? TestDate { get; set; }
        /// <summary>
        /// 图表1名称
        /// </summary>
        public string? ChartName1 { get; set; }
        /// <summary>
        /// 图表2名称
        /// </summary>
        public string? ChartName2 { get; set; }
        /// <summary>
        /// 图表3名称
        /// </summary>
        public string? ChartName3 { get; set; }
        /// <summary>
        /// 图表1数据集
        /// </summary>
        public ObservableCollection<double>? ChartYValues1 { get; set; } = [];
        /// <summary>
        /// 图表2数据集
        /// </summary>
        public ObservableCollection<double>? ChartYValues2 { get; set; } = [];
        /// <summary>
        /// 图表3数据集
        /// </summary>
        public ObservableCollection<double>? ChartYValues3 { get; set; } = [];

        /// <summary>
        /// 图表1X轴标签集
        /// </summary>
        public ObservableCollection<double>? ChartXLabel1 { get; set; } = [];
        /// <summary>
        /// 图表2X轴标签集
        /// </summary>
        public ObservableCollection<double>? ChartXLabel2 { get; set; } = [];
        /// <summary>
        /// 图表3X轴标签集
        /// </summary>
        public ObservableCollection<double>? ChartXLabel3 { get; set; } = [];
    }
}
