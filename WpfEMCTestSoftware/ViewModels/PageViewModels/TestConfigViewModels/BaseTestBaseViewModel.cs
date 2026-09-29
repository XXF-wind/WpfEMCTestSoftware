using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Enums;
using static WpfEMCTestSoftware.GenericHelper;

namespace WpfEMCTestSoftware.ViewModels.PageViewModels.TestConfigViewModels
{
    public partial class BaseTestBaseViewModel : BaseViewModel
    {
        //combox绑定的下拉数据源
        [ObservableProperty]
        private List<object> _standardList = Enum.GetValues(typeof(EMCStandard))
           .Cast<EMCStandard>()
           .Select(e => new
           {
               ShowText = e.GetDescription(),
               RealValue = e
           }).ToList<object>();
        //
        [ObservableProperty]
        private List<object> _reverbTestList = Enum.GetValues(typeof(ReverbTestMode))
            .Cast<ReverbTestMode>()
            .Select(e => new
            {
                ShowText = e.GetDescription(),
                RealValue = e
            }).ToList<object>();
        [ObservableProperty]
        private List<object> _testStageTypelist = Enum.GetValues(typeof(TestStageType))
            .Cast<TestStageType>()
            .Select(e => new
            {
                ShowText = e.GetDescription(),
                RealValue = e
            }).ToList<object>();

        [ObservableProperty]
        private List<object> _adjustPriority = Enum.GetValues(typeof(AdjustPriority)).Cast<AdjustPriority>()
           .Select(e => new
           {
               ShowText = e.GetDescription(),
               RealValue = e
           }).ToList<object>();
        [ObservableProperty]
        private List<object> _freUnitList = Enum.GetValues(typeof(FrequencyUnit)).Cast<FrequencyUnit>()
            .Select(e => new
            {
                ShowText = e.GetDescription(),
                RealValue = e
            }
            ).ToList<object>();
        [ObservableProperty]
        private List<object> _stepModesList = Enum.GetValues(typeof(STEPMODE)).Cast<STEPMODE>()
            .Select(
            e => new
            {
                ShowText = e.GetDescription(),
                RealValue = e
            }
            ).ToList<object>();
        [ObservableProperty]
        private List<object> _modTypeList = Enum.GetValues(typeof(ModulationType)).Cast<ModulationType>()
            .Select(
            e => new
            {
                ShowText = e.GetDescription(),
                RealValue = e
            }
            ).ToList<object>();

        [ObservableProperty]
        public ObservableCollection<ModelItem> _pointSelectList = new ObservableCollection<ModelItem>()
        {
            new ModelItem { ShowText = "5个点位", RealValue = 5 },
            new ModelItem { ShowText = "8个点位", RealValue = 8 },
            new ModelItem { ShowText = "9个点位", RealValue = 9 }
        };

        public class ModelItem
        {
            public string ShowText { get; set; }
            public int RealValue { get; set; }
        }
        // 全部设备集合统一放基类，子类直接继承使用
        [ObservableProperty]
        private ObservableCollection<string> _signalSourceNames = ["No Device"];
        [ObservableProperty]
        private ObservableCollection<string> _amplifierNames = ["No Device"];
        [ObservableProperty]
        private ObservableCollection<string> _powerMeterames = ["No Device"];
        [ObservableProperty]
        private ObservableCollection<string> _switchMatrixNames = ["No Device"];
        [ObservableProperty]
        private ObservableCollection<string> _fieldStrengthNames = ["No Device"];
        [ObservableProperty]
        private ObservableCollection<string> _networkAnalysisNames = ["No Device"];
        [ObservableProperty]
        private ObservableCollection<string> _receiverNames = ["No Device"];

        [ObservableProperty]
        private ObservableCollection<string> _switch1 = ["1-1", "1-2", "1-3", "1-4"];
        [ObservableProperty]
        private ObservableCollection<string> _switch2 = ["2-1", "2-2", "2-3", "2-4"];
        [ObservableProperty]
        private ObservableCollection<string> _switch3 = ["3-1", "3-2", "3-3", "3-4"];
        [ObservableProperty]
        private ObservableCollection<string> _switch4 = ["4-1", "4-2", "4-3", "4-4"];
        protected void LoadSortDeviceNames()
        {
            // 先清空原有设备（保留"No Device"首项）
            SignalSourceNames.Clear();
            SignalSourceNames.Add("No Device");
            foreach (var device in DeviceCacheStore.SignalSources)
                SignalSourceNames.Add(device.Name);

            AmplifierNames.Clear();
            AmplifierNames.Add("No Device");
            foreach (var device in DeviceCacheStore.Amplifiers)
                AmplifierNames.Add(device.Name);

            PowerMeterames.Clear();
            PowerMeterames.Add("No Device");
            foreach (var device in DeviceCacheStore.PowerMeters)
                PowerMeterames.Add(device.Name);

            SwitchMatrixNames.Clear();
            SwitchMatrixNames.Add("No Device");
            foreach (var device in DeviceCacheStore.SwitchMatrixs)
                SwitchMatrixNames.Add(device.Name);

            FieldStrengthNames.Clear();
            FieldStrengthNames.Add("No Device");
            foreach (var device in DeviceCacheStore.FieldStrengthMeters)
                FieldStrengthNames.Add(device.Name);

            NetworkAnalysisNames.Clear();
            NetworkAnalysisNames.Add("No Device");
            foreach (var device in DeviceCacheStore.NetworkAnalysis)
                NetworkAnalysisNames.Add($"{device.Name}");

            ReceiverNames.Clear();
            ReceiverNames.Add("No Device");
            foreach (var device in DeviceCacheStore.Receivers)
                ReceiverNames.Add($"{device.Name}");
        }
    }
}
