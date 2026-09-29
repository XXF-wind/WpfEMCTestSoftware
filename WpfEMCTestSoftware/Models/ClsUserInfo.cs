using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfEMCTestSoftware.Models
{
    public partial class ClsUserInfo : ObservableObject
    {
        [ObservableProperty]
        private int _validityPeriod = 365;
        [ObservableProperty]
        public string _creationTime = string.Empty;
        [ObservableProperty]
        private string _userId = string.Empty;
        [ObservableProperty]
        private string _userName = string.Empty;
        [ObservableProperty]
        public string _passWord = string.Empty;
        [ObservableProperty]
        public bool _isActive = false;
        [ObservableProperty]
        public int _level = -1;

        public string ExpirationDate
        {
            get
            {
                if (ValidityPeriod == 0)
                {
                    return "∞";
                }
                if (DateTime.TryParse(CreationTime, out DateTime result))
                {
                    return result.AddDays(ValidityPeriod).ToString("yyyy-MM-dd");
                }
                else
                {
                    return "";
                }
            }
        }
    }
}
