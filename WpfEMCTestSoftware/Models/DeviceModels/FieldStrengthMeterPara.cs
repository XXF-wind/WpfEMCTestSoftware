namespace WpfEMCTestSoftware.Models
{
    public class FieldStrengthMeterPara : DeviceInfo
    {
        private string _calibrationfactor = string.Empty; // 校准因子
        public Dictionary<double, double> calibrationfactorData { get; set; } = new Dictionary<double, double>();
        private int _sampletime = 0;
        /// <summary>
        /// 校准因子
        /// </summary>
        public string CalibrationFactor
        {
            get => _calibrationfactor;
            set => SetProperty(ref _calibrationfactor, value);
        }
        /// <summary>
        /// 采样时间
        /// </summary>
        public int SampleTime
        {
            get => _sampletime;
            set => SetProperty(ref _sampletime, value);
        }
    }
}
