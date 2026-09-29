using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace WpfEMCTestSoftware.Models.HardwareModels
{
    /// <summary>
    /// 【EMS硬件子频段配置模型】
    /// 每个频段 = 一个独立的 EMSSubBandHardConfigItem
    /// 包含：频段频率、硬件配置、衰减、开关命令等
    /// </summary>
    public partial class RVC_EMSHardModel : ObservableObject
    {
        [ObservableProperty]
        private int? _k1CMD;
        [ObservableProperty]
        private int? _k2CMD;
        [ObservableProperty]
        private int? _k3CMD;
        [ObservableProperty]
        private int? _k4CMD;
        [ObservableProperty]
        private string? _lossDesToAMP;
        [ObservableProperty]
        private string? _lossDesToFW;
        [ObservableProperty]
        private string? _lossDesToRW;
        [ObservableProperty]
        private Dictionary<double, double> _lossDesToAMPData = new();
        [ObservableProperty]
        private Dictionary<double, double> _lossDesToFWData = new();
        [ObservableProperty]
        private Dictionary<double, double> _lossDesToRWData = new();
        [ObservableProperty]
        private ObservableCollection<DeviceInfo> _listDeviceInfos = [.. Enumerable.Range(1, 20).Select(i => new DeviceInfo { Id = i, Name = "" })];
        [ObservableProperty]
        private string? _mtestMode;
    }

}
