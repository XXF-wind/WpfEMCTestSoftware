using System.Collections.ObjectModel;
namespace WpfEMCTestSoftware.Models
{
    public class AmpifierPara : DeviceInfo
    {
        private string _fcoupling = "0";//前向耦合,
        private string _rcoupling = "0";//反向耦合
        private string _maxInValue = "0";//功放最大输入值  
        private double _protectionpower; //保护功率值
        private ObservableCollection<FreqPowerPair> _ratedpowerinfreq = [new FreqPowerPair(), new FreqPowerPair(), new FreqPowerPair()];//频率区间对应的额定功率值
        public Dictionary<double, double> FCouplingData { get; set; } = new();
        public Dictionary<double, double> RCouplingData { get; set; } = new();
        public Dictionary<double, double> MaxinValueData { get; set; } = new();
        /// <summary>
        /// 前向耦合，数值代表定值，路径字符串代表文件
        /// </summary>
        public string FCoupling
        {
            get => _fcoupling;
            set => SetProperty(ref _fcoupling, value);
        }

        /// <summary>
        /// 反向耦合，数值代表定值，路径字符串代表文件
        /// </summary>
        public string RCoupling
        {
            get => _rcoupling;
            set => SetProperty(ref _rcoupling, value);
        }

        /// <summary>
        /// 功放最大输入值
        /// </summary>
        public string MaxInValue
        {
            get => _maxInValue;
            set => SetProperty(ref _maxInValue, value);
        }

        /// <summary>
        /// 驻波保护起始功率
        /// </summary>
        public double ProtectionPower
        {
            get => _protectionpower;
            set => SetProperty(ref _protectionpower, value);
        }

        /// <summary>
        /// 频率区间对应的额定功率值
        /// </summary>
        public ObservableCollection<FreqPowerPair> RatedPowerInFreq
        {
            get => _ratedpowerinfreq;
            set => SetProperty(ref _ratedpowerinfreq, value);
        }

        /// <summary>
        /// 频段对应功率
        /// </summary>
        public class FreqPowerPair
        {
            public int ID { get; set; } = 1;
            /// <summary>
            /// 起始频率
            /// </summary>
            public double StartFreq { get; set; }
            /// <summary>
            /// 终止频率
            /// </summary>
            public double EndFreq { get; set; }
            /// <summary>
            /// 额定功率值
            /// </summary>
            public double RatedPower { get; set; }
        }
    }
}
