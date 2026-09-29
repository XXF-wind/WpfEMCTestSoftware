using CommunityToolkit.Mvvm.ComponentModel;
using WpfEMCTestSoftware.Models.HardwareModels;
using WpfEMCTestSoftware.ViewModels.PageViewModels.TestConfigViewModels;


namespace WpfEMCTestSoftware.ViewModels.PageViewModels.HardwareViewModels
{

    public partial class RVC_HardwareViewModel : BaseTestBaseViewModel
    {
        [ObservableProperty]
        private RVC_EMSHardModel _curRVCHardModel = new RVC_EMSHardModel();
        public RVC_HardwareViewModel()
        {
            LoadSortDeviceNames();
        }




    }
}
