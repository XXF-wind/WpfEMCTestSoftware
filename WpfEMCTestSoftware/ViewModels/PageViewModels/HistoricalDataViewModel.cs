using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Models;
namespace WpfEMCTestSoftware.ViewModels
{
    public partial class HistoricalDataViewModel : BaseViewModel
    {
        #region 频段
        /// <summary>
        /// 最小频率
        /// </summary>
        [ObservableProperty]
        private double _minFrequence = 0;

        /// <summary>
        /// 最大频率
        /// </summary>
        [ObservableProperty]
        private double _maxFrequence = 100;
        #endregion
        /// <summary>
        /// 测试记录队列
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _testRecords = [];

        /// <summary>
        /// 测试记录
        /// </summary>
        [ObservableProperty]
        private TestRecordModel _testRecordModel = new();

        /// <summary>
        /// 曲线标签
        /// </summary>
        [ObservableProperty]
        private string _valueLabel = string.Empty;
        /// <summary>
        /// 显示信息(不可修改)
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ClsLabelInfo> _displayInfo;

        [ObservableProperty]
        private Func<double, string> _selFormatter = value => value.ToString("F0");

        public HistoricalDataViewModel()
        {

        }
    }
}
