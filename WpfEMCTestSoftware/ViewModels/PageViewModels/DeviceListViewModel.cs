using ClassLibrary;
using ClassLibrary.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EMCDeviceLibrary;
using System.Collections.ObjectModel;
using System.Windows;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Generic;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.EventMessages;

namespace WpfEMCTestSoftware.ViewModels
{
    public partial class DeviceListViewModel : BaseViewModel
    {
        /// <summary>
        /// 设备类型树形结构数据
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<TreeItem>? _treeItems;

        // 当前选中的设备节点
        [ObservableProperty]
        private TreeItem _curTreeItem = new();

        /// <summary>
        /// 设备类型列表
        /// </summary>
        private List<string?> DeviceTypes { get; set; } = [];
        private List<string> PackIcons { get; set; } = [];

        /// <summary>
        /// 使用语言
        /// </summary>
        public SwitchLanguage Language
        {
            get => GenericParameter.Language;
            set
            {
                GenericParameter.Language = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 设备信息列表
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<DeviceInfo>? _deviceInfos = [];

        /// <summary>
        /// 选中设备信息
        /// </summary>
        [ObservableProperty]
        private DeviceInfo _currentdeviceInfo = new();

        /// <summary>
        /// 列刷新触发器（绑定到Behavior，true时触发列刷新）
        /// </summary>
        [ObservableProperty]
        private bool _refreshColumnsTrigger;

        // ============ 新增：设备类型缓存 ============



        public DeviceListViewModel(IMessenger messenger) : base(messenger)
        {
            Initlist();
            InitTreeItems();
            //绑定全局的设备到当前的设备列表
            GenericHelper.globalDevices = [.. DeviceSerAndDeser.GetDeviceInfos(ClsData.Instance.GainDeviceInfo())];
            DeviceInfos = GenericHelper.globalDevices;

        }
        /// <summary>
        /// 初始化缓存
        /// </summary>


        private void RegisterMessages()
        {
            //订阅设备类型请求

        }
        /// <summary>
        /// 初始化列表
        /// </summary>
        private void Initlist()
        {
            DeviceTypes =
                [
                GenericHelper.GetStringResource("DeviceTypeTitle_SignalSource"), //信号源 Signal source
                GenericHelper.GetStringResource("DeviceTypeTitle_Receiver"), //接收机  Receiver
                GenericHelper.GetStringResource("DeviceTypeTitle_FieldStrengthMeter"),//场强仪  Field strength Meter
                GenericHelper.GetStringResource("DeviceTypeTitle_Amplifier"),//功放  Amplifier
                GenericHelper.GetStringResource("DeviceTypeTitle_PowerMeter"),//功率计  Power meter
                GenericHelper.GetStringResource("DeviceTypeTitle_PhaseAttenuator"),//移相衰减器  Phase attenuator
                GenericHelper.GetStringResource("DeviceTypeTitle_MultiplierFreq"),//倍频器  Multiplier frequency
                GenericHelper.GetStringResource("DeviceTypeTitle_SwitchMatrix"),//开关矩阵  Switch matrix
                //GenericHelper.GetStringResource("DeviceTypeTitle_Oscilloscope"), // 示波器
                //GenericHelper.GetStringResource("DeviceTypeTitle_Spectrumanalyzer"), //频谱仪
                GenericHelper.GetStringResource("DeviceTypeTitle_Stirrer"), //搅拌器
                GenericHelper.GetStringResource("DeviceTypeTitle_Antenna"), //天线
                GenericHelper.GetStringResource("DeviceTypeTitle_AntennaTower"), //天线塔
                GenericHelper.GetStringResource("DeviceTypeTitle_NetworkAnalysis"),
                ];
            PackIcons =
                [
                "pack://application:,,,/WpfEMCTestSoftware;component/Images/信号源.png",
                "pack://application:,,,/WpfEMCTestSoftware;component/Images/接收机.png",
                "pack://application:,,,/WpfEMCTestSoftware;component/Images/场强仪.png",
                "pack://application:,,,/WpfEMCTestSoftware;component/Images/功放.png",
                "pack://application:,,,/WpfEMCTestSoftware;component/Images/功率计.png",
                "pack://application:,,,/WpfEMCTestSoftware;component/Images/衰减器.png",
                "pack://application:,,,/WpfEMCTestSoftware;component/Images/倍频器.png",
                "pack://application:,,,/WpfEMCTestSoftware;component/Images/开关矩阵.png",
                //"pack://application:,,,/WpfEMCTestSoftware;component/Images/示波器.png",
                //"pack://application:,,,/WpfEMCTestSoftware;component/Images/示波器.png",
                "pack://application:,,,/WpfEMCTestSoftware;component/Images/transorHorizontal.png",
                "pack://application:,,,/WpfEMCTestSoftware;component/Images/antennaEmitter.png",
                "pack://application:,,,/WpfEMCTestSoftware;component/Images/antenna.png",
                "pack://application:,,,/WpfEMCTestSoftware;component/Images/示波器.png",
                ];
        }

        /// <summary>
        /// 初始化树形结构数据
        /// </summary>
        private void InitTreeItems()
        {
            for (int i = 0; i < DeviceTypes.Count; i++)
            {
                TreeItems ??= [];
                ObservableCollection<TreeItem> items = [];
                string pathName = PackIcons[i];
                for (int id = i * 10000; id < (i * 10000) + 10000; id++)
                {
                    if (Enum.GetName(typeof(DeviceMode), id) != null)//检查枚举中是否存在对应id的枚举值名称。如果存在，则进入if语句内部。
                    {
                        items.Add(new TreeItem
                        {
                            Name = Enum.GetName(typeof(DeviceMode), id) ?? string.Empty,
                            Path = pathName
                        });
                    }
                }
                //创建一个TreeItem父节点，其Name为设备类型名称，Path为对应的图标路径，Children为之前构建的子节点集合（items）。然后将该父节点添加到TreeItems集合中。
                TreeItems.Add(new TreeItem
                {
                    Name = DeviceTypes[i],
                    Path = pathName,
                    Children = items
                });
            }
        }

        /// <summary>
        /// 查看详情命令执行
        /// </summary>
        /// <param name="student"></param>  
        [RelayCommand]
        private void ViewDetail()
        {
            EditDevice();
        }

        // 删除行命令执行
        [RelayCommand]
        private void DeleteRow()
        {
            DeleteDevice();
        }
        /// <summary>
        /// 复制行
        /// </summary>
        [RelayCommand]
        private void CopyRow()
        {
            // 检查当前是否有选中的设备
            if (CurrentdeviceInfo is null)
            {
                MessageBox.Show("请先选择一个设备进行复制", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {

                string newName = $"{CurrentdeviceInfo.Name}_{DeviceInfos?.Count + 1}";
                // 1. 创建新设备对象（深拷贝）
                DeviceInfo newDev = new DeviceInfo
                {
                    Id = (DeviceInfos?.Count ?? 0) + 1,
                    Name = newName,
                    Type = CurrentdeviceInfo.Type,
                    Model = CurrentdeviceInfo.Model,
                    Communicationmode = CurrentdeviceInfo.Communicationmode,
                    Address = CurrentdeviceInfo.Address,
                    IsActive = CurrentdeviceInfo.IsActive,
                    Time = CurrentdeviceInfo.Time
                };

                if (CurrentdeviceInfo.DevicePara != null)
                {
                    newDev.DevicePara = new DevicePara
                    {
                        IP = CurrentdeviceInfo.DevicePara.IP,
                        Port = CurrentdeviceInfo.DevicePara.Port,
                        ComPort = CurrentdeviceInfo.DevicePara.ComPort,
                        ComBaud = CurrentdeviceInfo.DevicePara.ComBaud,
                        CANChannel = CurrentdeviceInfo.DevicePara.CANChannel,
                        CANBaudRate = CurrentdeviceInfo.DevicePara.CANBaudRate
                    };
                }
                else
                {
                    newDev.DevicePara = null;
                }

                // 2. 添加到当前列表
                DeviceInfos?.Add(newDev);
                //3.保存到数据库
                ClsData.Instance.SaveDevice(newDev);
                MessageBox.Show($"设备复制成功：{newName}", "成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);

            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制设备失败：{ex.Message}", "错误",
                         MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        /// <summary>
        /// 重命名
        /// </summary>
        [RelayCommand]
        private void RenameRow()
        {
            // 检查当前是否有选中的设备
            if (CurrentdeviceInfo is null)
            {
                MessageBox.Show("请先选择一个设备进行重命名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {

                MMessageBox.Instance.Show(new DialogInfo("修改设备名称", [], "", "设备名称", CurrentdeviceInfo.Name), out DialogResult result);
                if (result.IsConfirmed != true) return;
                string newName = result.Data?.ToString() ?? string.Empty;
                // 用户点击取消或未输入
                if (string.IsNullOrEmpty(newName))
                {
                    return;
                }
                // 验证新名称（排除自己，否则不改名直接点确定也会误报"已存在"）
                if (isAlreadyExistName(newName))
                {
                    MessageBox.Show($"设备名称 {newName} 已存在，请换一个名称", "提示",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    return;

                }
                if (newName.Length > 50)
                {
                    MessageBox.Show("设备名称过长（数据库上限50字符），请缩短后重试", "错误",
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                ClsData.Instance.RenameDevice(CurrentdeviceInfo.Name, newName);
                CurrentdeviceInfo.Name = newName;

            }
            catch (Exception ex)
            {
                //字段太小而不能接受所要添加的数据的数量
                MessageBox.Show($"重命名失败：{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool isAlreadyExistName(string name)
        {
            for (int i = 0; i < DeviceInfos?.Count; ++i)
            {
                //排除自己：重命名保持原名时不算"已存在"
                if (!ReferenceEquals(DeviceInfos[i], CurrentdeviceInfo) && DeviceInfos[i].Name == name)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 设备操作命令执行
        /// 0=添加，1=编辑，2=删除，3=保存，4=退出。
        /// </summary>
        /// <param name="obj"></param>
        [RelayCommand]
        private void DeviceOper(object obj)
        {
            if (obj is string temp)
            {

                int operation = int.Parse(temp);
                switch (operation)
                {
                    case 0:
                        AddDevice();
                        break;
                    case 1:
                        EditDevice();
                        break;
                    case 2:
                        DeleteDevice();
                        break;
                    case 3:
                        SaveDevice();
                        break;
                    case 4:
                        ExitDeviceList();
                        break;
                }
            }

        }
        /// <summary>
        /// 添加设备，并且写入数据库
        /// </summary>
        private void AddDevice()
        {
            try
            {
                if (CurTreeItem.Children?.Count == 0)
                {
                    TreeItem Parent = GenericHelper.FindParentNode(TreeItems!, CurTreeItem)!;
                    if (Parent == null) return;
                    int index = DeviceTypes.IndexOf(Parent.Name);
                    DeviceInfo DEV = new DeviceInfo()
                    {
                        Id = (int)DeviceInfos?.Count + 1,
                        Name = $"{CurTreeItem.Name}_{DeviceInfos?.Count + 1}",
                        Model = (DeviceMode)Enum.Parse(typeof(DeviceMode), CurTreeItem.Name),
                        //不管英文还是中文显示，都在数据库中存中文的类型名称
                        Type = (DeviceType)index

                    };
                    DeviceInfos?.Add(DEV);
                    ClsData.Instance.SaveDevice(DEV);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加设备失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        /// <summary>
        /// 编辑，功能同步在datagrid节点双击
        /// </summary>
        private void EditDevice()
        {
            // 检查当前是否有选中的设备
            if (CurrentdeviceInfo is null)
            {
                MessageBox.Show("请先选择一个设备进行编辑", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Messenger.Send(new SetDeviceEventMessage(CurrentdeviceInfo), (int)DeviceParaMessageToken.DeviceListToMainWindow);
                Console.WriteLine($"开始编辑设备: {CurrentdeviceInfo.Name} (ID: {CurrentdeviceInfo.Id})");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"编辑设备失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 删除当前选中的deviceinfo,并且从列表中删除，也从数据库中删除
        /// </summary>
        private void DeleteDevice()
        {
            // 检查当前是否有选中的设备
            if (CurrentdeviceInfo is null)
            {
                MessageBox.Show("请先选择一个设备进行删除", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                // 确认删除对话框
                var result = MessageBox.Show(
                    $"确定要删除设备 '{CurrentdeviceInfo.Name}' 吗？\n" +
                    $"设备类型: {CurrentdeviceInfo.Type}\n" +
                    $"设备型号: {CurrentdeviceInfo.Model}\n\n" +
                    "此操作将从数据库和列表中永久删除，不可恢复！",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;
                // 1. 从数据库中删除
                ClsData.Instance.DelDevice(CurrentdeviceInfo.Name);
                // 2. 从内存列表中删除
                DeviceInfos?.Remove(CurrentdeviceInfo);
                // 4. 清除当前选中的设备
                CurrentdeviceInfo = null;

                // 5.设备ID重新编号
                if (DeviceInfos != null)
                {
                    for (int i = 0; i < DeviceInfos.Count; i++)
                    {
                        DeviceInfos[i].Id = i + 1;
                    }
                }

                // 显示成功消息
                MessageBox.Show("设备删除成功", "成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                Console.WriteLine("设备删除成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除设备失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);

                // 记录错误日志
                LogManager.WriteLog($"删除设备失败: {ex.Message}");
            }

        }
        /// <summary>
        /// 界面数据保存
        /// </summary>
        private void SaveDevice()
        {
            ClsData.Instance.ModifyDevicesActive(DeviceInfos);
        }
        /// <summary>
        /// 退出当前页面
        /// </summary>
        private void ExitDeviceList()
        {
            Messenger.Send(new ExitClickEventMessage(0xff), (int)PageExitMessageToken.SubPageToMainWindow);
        }

        public void updateDeviceList()
        {
            GenericHelper.globalDevices = _deviceInfos;
            GenericHelper.SortDevicesByType();
        }

        // 辅助方法：关闭窗口
        private void CloseWindow()
        {
            var window = Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.DataContext == this);
            window?.Close();
        }


        /// <summary>
        /// 设备树节点选中
        /// </summary>
        /// <param name="item"></param>
        public void SelectedTreeItem(TreeItem item)
        {
            CurTreeItem = item;
        }
        //跨窗口/跨页面通信
        [RelayCommand]
        private void DoubleDeviceData(object obj)
        {
            if (CurrentdeviceInfo is null) return;
            Messenger.Send(new SetDeviceEventMessage(CurrentdeviceInfo), (int)DeviceParaMessageToken.DeviceListToMainWindow);
        }
    }
}
