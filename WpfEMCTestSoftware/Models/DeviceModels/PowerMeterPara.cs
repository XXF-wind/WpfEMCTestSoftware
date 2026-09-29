namespace WpfEMCTestSoftware.Models
{
    public class PowerMeterPara : DeviceInfo
    {
        private int _descentdelay = 0;
        private int _samplingtime = 0;
        /// <summary>
        /// 下降延时
        /// </summary>
        public int DescentDelay
        {
            get => _descentdelay;
            set { _descentdelay = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 采样时间
        /// </summary>
        public int SamplingTime
        {
            get => _samplingtime;
            set { _samplingtime = value; OnPropertyChanged(); }
        }
    }
}
