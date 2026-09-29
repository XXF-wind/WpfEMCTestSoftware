namespace WpfEMCTestSoftware.Models.DeviceModels
{
    public class StirrerPara : DeviceInfo
    {
        private int _stepAngeleSet = 0;
        private int _minAngle = 0;
        private int _maxAngle = 0;
        private int _rotateSpeed = 0;

        public int StepAngeleSet
        {
            get { return _stepAngeleSet; }
            set
            {
                _stepAngeleSet = value;
                OnPropertyChanged();
            }
        }
        public int MinAngle
        {
            get => _minAngle;
            set
            {
                _minAngle = value;
                OnPropertyChanged();
            }
        }
        public int MaxAngle
        {
            get => _maxAngle;
            set
            {
                _maxAngle = value;
                OnPropertyChanged();
            }
        }

        public int RotateSpeed
        {
            get => _rotateSpeed;
            set
            {
                _rotateSpeed = value;
                OnPropertyChanged();
            }
        }
    }
}
