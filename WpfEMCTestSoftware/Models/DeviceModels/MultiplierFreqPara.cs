namespace WpfEMCTestSoftware.Models
{
    public class MultiplierFreqPara : DeviceInfo
    {
        private double _maxatt = 0.0;  // 最大衰减值
        private double _stepatt = 0.0; // 衰减步进值

        /// <summary>
        /// 最大衰减值
        /// </summary>
        public double MaxAtt
        {
            get => _maxatt;
            set => SetProperty(ref _maxatt, value);
        }

        /// <summary>
        /// 衰减步进值
        /// </summary>
        public double StepAtt
        {
            get => _stepatt;
            set => SetProperty(ref _stepatt, value);
        }
    }
}
