using ClassLibrary;
using ClassLibrary.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EMCDeviceLibrary;
using Peak.Can.Basic.BackwardCompatibility;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.EventMessages;
using WpfEMCTestSoftware.Views;
using WpfEMCTestSoftware.Views.PageViews.DeviceViews;

namespace WpfEMCTestSoftware.ViewModels
{
    public partial class DeviceParaViewModel : BaseViewModel
    {
        #region 属性/变量定义

        /// <summary>
        /// 语言
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

        [ObservableProperty]
        private DeviceInfo _deviceInfo = new();


        /// <summary>
        /// COM端口列表
        /// </summary>
        public ObservableCollection<string> ComPorts { get; } = [.. System.IO.Ports.SerialPort.GetPortNames()];
        /// <summary>
        /// 波特率列表（用字符串，与 ComPort/ComBaud 的 string 属性直接匹配，避免 int↔string 绑定回显失败）
        /// </summary>
        public ObservableCollection<string> BaudRates { get; } = ["9600", "19200", "38400", "57600", "115200", "230400", "460800", "921600"];
        /// <summary>
        /// CAN总线连接波特率列表（直接用枚举值，与 SelectedValue 绑定的枚举属性类型一致，保证回显）
        /// </summary>
        public ObservableCollection<TPCANBaudrate> CANBauds { get; } = new((TPCANBaudrate[])Enum.GetValues(typeof(TPCANBaudrate)));
        /// <summary>
        /// 通讯方式列表
        /// </summary>
        public ObservableCollection<string> CommunicationModes { get; } = [.. Enum.GetNames(typeof(CommunicationMode))];

        [ObservableProperty]
        private ObservableCollection<bool> _modeEnableds = [true, false, false, false, false];

        [ObservableProperty]
        private Page? _paraPage;

        [ObservableProperty]
        private Page? _testPage;
        #endregion

        public DeviceParaViewModel()
        {
            IsActive = true;
            //设备信息转发：主窗体 -> 设备参数页（本页）
            Messenger.Register<SetDeviceEventMessage, int>(this, (int)DeviceParaMessageToken.MainWindowToDevicePara, (r, m) => ViewDeviceDetails(m));
            //保存回传：设备子页 -> 设备参数页（子页参数已回写，由本页落库）
            Messenger.Register<SaveClickEventMessage, int>(this, (int)DeviceSaveMessageToken.SubPageToDevicePara, (r, m) => SaveDeviceInfo(m));
        }

        /// <summary>
        /// 展示设备信息详情
        /// </summary>
        /// <param name="m"></param>
        private void ViewDeviceDetails(SetDeviceEventMessage m)
        {
            DeviceInfo = m.Value;
            int count = (int)DeviceInfo.Model / 10000;
            InitPage(count);
            //回显通讯方式：根据已保存的枚举点亮对应RadioButton并切换参数面板
            SyncModeEnableds((int)DeviceInfo.Communicationmode);
            //设备信息继续下发：设备参数页 -> 具体设备子页（功放 / 接收机 / 信号源 ……）
            Messenger.Send(m, (int)DeviceParaMessageToken.DeviceParaToSubPage);
        }

        /// <summary>
        /// 重建通讯方式选中状态（整体替换集合触发PropertyChanged，RadioButton选中态与参数面板Visibility才会刷新）
        /// </summary>
        /// <param name="index">通讯方式枚举对应的索引</param>
        private void SyncModeEnableds(int index)
        {
            ModeEnableds = [index == 0, index == 1, index == 2, index == 3, index == 4];
        }

        /// <summary>
        /// 点击通讯方式RadioButton：统一在这里切换面板并回写到设备对象（保存时才能落库）
        /// </summary>
        /// <param name="index">RadioButton的CommandParameter（0=LAN 1=Serial 2=CAN 3=VISA 4=None）</param>
        [RelayCommand]
        private void SelectCommunicationMode(string index)
        {
            int i = int.Parse(index);
            SyncModeEnableds(i);
            if (DeviceInfo != null)
            {
                DeviceInfo.Communicationmode = (CommunicationMode)i;
            }
        }

        /// <summary>
        /// 初始化子页面
        /// </summary>
        private void InitPage(int id)
        {
            ParaPage = id switch
            {
                0 => new SignalSouceParaPage(),
                1 => new ReceiverParaPage(),
                2 => new FieldStrengthMeterParaPage(),
                3 => new AmpifierParaPage(),
                4 => new PowerMeterParaPage(),
                5 => new PhaseAttParaPage(),
                6 => new MultiplierFreqParaPage(),
                7 => new SwitchMatrixParaPage(),
                8 => new OscilloscopeParaPage(),    //示波器
                9 => new SpectrumanalyzerParaPage(),//频谱仪
                10 => new StirrerParaPage(),        //搅拌器
                11 => new AntennaPage(),            //天线
                12 => new AntennaTowerPage(),       //天线塔
                _ => null,
            };

            TestPage = id switch
            {
                0 => new SignalSourceTestPage(),
                3 => new AmpifierTestPage(),
                8 => new AmpifierTestPage(),
                _ => null,
            };
        }

        /// <summary>
        /// 接收保存设备参数结果的消息
        /// </summary>
        /// <param name="Result">返回保存结果</param>
        public void SaveDeviceInfo(SaveClickEventMessage Result)
        {
            if (Result.Value == true)
            {
                ClsData.Instance.SaveDevice(DeviceInfo);
            }
            Messenger.Send(new ExitClickEventMessage(1), (int)PageExitMessageToken.SubPageToMainWindow);
        }

        /// <summary>
        /// 确认/保存点击命令
        /// </summary>
        [RelayCommand]
        private void OnConfirm()
        {
            if (ParaPage == null)
            {
                ClsData.Instance.SaveDevice(DeviceInfo);
                Messenger.Send(new ExitClickEventMessage(1), (int)PageExitMessageToken.SubPageToMainWindow);
            }
            //通知设备子页把界面上的值回写到参数对象
            Messenger.Send(new SaveClickEventMessage(true), (int)DeviceSaveMessageToken.DeviceParaToSubPage);

        }

        /// <summary>
        /// 取消点击命令
        /// </summary>
        [RelayCommand]
        private void OnCencel()
        {
            Messenger.Send(new ExitClickEventMessage(1), (int)PageExitMessageToken.SubPageToMainWindow);// 
        }
    }
}
