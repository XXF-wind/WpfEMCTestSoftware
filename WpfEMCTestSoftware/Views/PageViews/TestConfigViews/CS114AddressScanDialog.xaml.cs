using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EMCDeviceLibrary;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfEMCTestSoftware.Models;

namespace WpfEMCTestSoftware.Views.PageViews.TestConfigViews
{
    /// <summary>
    /// CS114 仪器通讯地址弹窗：
    /// 自动扫描 —— 从设备库自动获取在线仪器型号与地址；手动输入 —— 直接填写通讯地址。
    /// 选中/填写后点击【应用】，再点【确定】完成设置。
    /// </summary>
    public partial class CS114AddressScanDialog : Window
    {
        public CS114AddressScanVM VM { get; } = new();

        public CS114AddressScanDialog(DeviceInfo device)
        {
            InitializeComponent();
            VM.Initialize(device);
            DataContext = VM;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void ModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            // 自动扫描/手动输入提示切换（扫描结果始终可见，便于对照）
            if (VM == null) return;
            VM.ScanHint = (sender is RadioButton rb && rb.Content?.ToString() == "手动输入")
                ? "手动输入：在下方填写通讯地址后点击【应用】"
                : "自动获取：点击【扫描】获取在线仪器 → 选中通讯地址 → 点击【应用】";
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            VM.Confirm();
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }

    /// <summary>扫描结果行</summary>
    public class CS114ScanItem
    {
        public string Name { get; set; } = "";
        public string Model { get; set; } = "";
        public string Comm { get; set; } = "";
        public string Address { get; set; } = "";
    }

    /// <summary>
    /// 地址弹窗 ViewModel（弹窗内部使用）
    /// </summary>
    public partial class CS114AddressScanVM : ObservableObject
    {
        [ObservableProperty]
        private string _slotRoleName = "";

        [ObservableProperty]
        private string _scanHint = "自动获取：点击【扫描】获取在线仪器 → 选中通讯地址 → 点击【应用】";

        [ObservableProperty]
        private string _manualAddress = "";

        [ObservableProperty]
        private string _appliedInfo = "";

        [ObservableProperty]
        private ObservableCollection<CS114ScanItem> _scanResults = new();

        [ObservableProperty]
        private CS114ScanItem? _curScanItem;

        private DeviceInfo? _device;

        public void Initialize(DeviceInfo device)
        {
            _device = device;
            SlotRoleName = string.IsNullOrWhiteSpace(device.Name) ? $"设备{device.Id}" : device.Name;
            ManualAddress = device.Address ?? "";
        }

        /// <summary>确定：若无已应用内容且有选中项，直接应用选中项</summary>
        public void Confirm()
        {
            if (_device == null) return;
            if (CurScanItem != null)
                ApplyItem(CurScanItem);
        }

        [RelayCommand]
        private void OnScan()
        {
            ScanResults.Clear();
            CurScanItem = null;
            var found = GenericHelper.globalDevices?
                .Where(d => d != null)
                .Select(d => new CS114ScanItem
                {
                    Name = d.Name,
                    Model = d.Model != null ? d.Model.ToString() : "",
                    Comm = d.Communicationmode.ToString(),
                    Address = string.IsNullOrEmpty(d.Address) ? "--" : d.Address,
                })
                .ToList() ?? new List<CS114ScanItem>();

            foreach (var item in found)
                ScanResults.Add(item);

            if (ScanResults.Count == 0)
                MessageBox.Show("未扫描到在线仪器，请检查设备连接，或切换到【手动输入】填写通讯地址。", "提示");
        }

        [RelayCommand]
        private void OnApply()
        {
            if (CurScanItem == null)
            {
                MessageBox.Show("请先在列表中选中一条通讯地址", "提示");
                return;
            }
            ApplyItem(CurScanItem);
        }

        [RelayCommand]
        private void OnApplyManual()
        {
            if (_device == null) return;
            if (string.IsNullOrWhiteSpace(ManualAddress))
            {
                MessageBox.Show("请填写通讯地址", "提示");
                return;
            }
            _device.Address = ManualAddress.Trim();
            if (_device.Name == "No Device" || string.IsNullOrEmpty(_device.Name))
                _device.Name = "手动设备";
            AppliedInfo = $"已应用：{SlotRoleName} @ {_device.Address}";
        }

        private void ApplyItem(CS114ScanItem item)
        {
            if (_device == null) return;
            _device.Name = string.IsNullOrEmpty(item.Name) ? "No Device" : item.Name;
            if (Enum.TryParse<DeviceMode>(item.Model, out var model))
                _device.Model = model;
            if (Enum.TryParse<CommunicationMode>(item.Comm, out var comm))
                _device.Communicationmode = comm;
            _device.Address = item.Address == "--" ? "" : item.Address;
            AppliedInfo = $"已应用：{SlotRoleName} @ {_device.Address}";
        }
    }
}
