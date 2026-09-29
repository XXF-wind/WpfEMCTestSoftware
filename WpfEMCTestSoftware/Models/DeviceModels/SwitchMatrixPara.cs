using System.Collections.ObjectModel;

namespace WpfEMCTestSoftware.Models
{
    public class SwitchMatrixPara : DeviceInfo
    {
        private ObservableCollection<int> _switchState = [];
        /// <summary>
        /// 开关状态
        /// </summary>
        public ObservableCollection<int> SwitchState
        {
            get => _switchState;
            set => SetProperty(ref _switchState, value);
        }
    }
}
