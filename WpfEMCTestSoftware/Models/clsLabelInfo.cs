using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfEMCTestSoftware.Models
{
    public partial class ClsLabelInfo : ObservableObject
    {
        [ObservableProperty]
        private string _labelTitle = string.Empty;

        [ObservableProperty]
        private string _labelValue = string.Empty;
    }
}
