namespace WpfEMCTestSoftware.Models.DeviceModels
{
    public class OscilloscopePara : DeviceInfo
    {
        private bool _isrf = false;// 是否打开射频输出
        private double _setfreq = 0.0;// 设置频率
        private double _gainfreq = 0.0;// 获取频率
        private double _forwardpower = 0.0;// 前向功率
        private double _reversepower = 0.0;// 反向功率
        private int _errorstatus = 0;// 告警状态

        /// <summary>
        /// 是否打开射频输出
        /// </summary>
        public bool IsRF
        {
            get => _isrf;
            set => SetProperty(ref _isrf, value);
        }
        /// <summary>
        /// 设置频率
        /// </summary>
        public double SetFreq
        {
            get => _setfreq;
            set => SetProperty(ref _setfreq, value);
        }
        /// <summary>
        /// 获取频率
        /// </summary>
        public double Gainfreq
        {
            get => _gainfreq;
            set => SetProperty(ref _gainfreq, value);
        }
        /// <summary>
        /// 前向功率
        /// </summary>
        public double ForwardPower
        {
            get => _forwardpower;
            set => SetProperty(ref _forwardpower, value);
        }
        /// <summary>
        /// 反向功率
        /// </summary>
        public double ReversePower
        {
            get => _reversepower;
            set => SetProperty(ref _reversepower, value);
        }
        /// <summary>
        /// 告警状态
        /// </summary>
        public int ErrorStatus
        {
            get => _errorstatus;
            set => SetProperty(ref _errorstatus, value);
        }
    }
}
