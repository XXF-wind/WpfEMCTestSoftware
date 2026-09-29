using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EMCDeviceLibrary;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.EventMessages;

namespace WpfEMCTestSoftware.ViewModels
{
    public partial class SignalSourceTestViewModel : BaseViewModel
    {
        #region 变量/属性定义
        /// <summary>
        /// 是否连接
        /// </summary>
        [ObservableProperty]
        private bool _isConnected = false;

        /// <summary>
        /// 当前连接按钮文本
        /// </summary>
        [ObservableProperty]
        private string _connectbtnText = string.Empty;

        /// <summary>
        /// 是否打开信号输出
        /// </summary>
        [ObservableProperty]
        private bool _isOutput = false;

        /// <summary>
        /// 信号输出按钮文本
        /// </summary>
        [ObservableProperty]
        private string _outputbtnText = "Output ON";

        /// <summary>
        /// 设置频率值
        /// </summary>
        [ObservableProperty]
        private double _setFreq;

        /// <summary>
        /// 设置信号
        /// </summary>
        [ObservableProperty]
        private string _setSingal = string.Empty;

        /// <summary>
        /// 脉冲调制按钮状态
        /// </summary>
        [ObservableProperty]
        private bool _isPulseModulation = false;

        /// <summary>
        /// 脉冲调制按钮显示内容
        /// </summary>
        [ObservableProperty]
        private string _pulseModulation = string.Empty;

        /// <summary>
        /// 调制使能按钮状态
        /// </summary>
        [ObservableProperty]
        private bool _isModulationEnable = false;

        /// <summary>
        /// 调制使能按钮显示内容
        /// </summary>
        [ObservableProperty]
        private string _modulationEnable = string.Empty;

        private DeviceInfo DeviceInfo = new();
        private SockConnect? Sock = null;
        #endregion

        public SignalSourceTestViewModel()
        {
            IsActive = true;
            //设备信息下发：设备参数页 -> 信号源测试子页
            Messenger.Register<SetDeviceEventMessage, int>(this, (int)DeviceParaMessageToken.DeviceParaToSubPage, (r, msg) => OnGainPara(msg));
            Initializer();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        private void Initializer()
        {
            ConnectbtnText = GenericHelper.GetStringResource("ConnectTitle");
            PulseModulation = GenericHelper.GetStringResource("PulseModulationTitle") + GenericHelper.GetStringResource("OpenTitle");
            ModulationEnable = GenericHelper.GetStringResource("ModulationEnableTitle") + GenericHelper.GetStringResource("OpenTitle");
            OutputbtnText = GenericHelper.GetStringResource("SingalOutputTitle") + GenericHelper.GetStringResource("OpenTitle");
        }

        /// <summary>
        /// 接收设备参数的消息
        /// </summary>
        /// <param name="message">事件消息</param>
        public void OnGainPara(SetDeviceEventMessage message)
        {
            DeviceInfo = message.Value;
        }

        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="obj"></param>
        [RelayCommand]
        private void ReadData(object obj)
        {
            if (IsConnected)
            {

            }
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="obj"></param>
        [RelayCommand]
        private void SendData(object obj)
        {
            if (IsConnected)
            {

            }
        }

        /// <summary>
        /// 设备连接
        /// </summary>
        /// <param name="obj"></param>
        [RelayCommand]
        private void ConnectDevice(object obj)
        {
            Sock = ClsFactory.GainSock(DeviceInfo.Model);
            Sock?.InitConnection(DeviceInfo.DevicePara);
            if (Sock != null && Sock.Initialed)
            {
                IsConnected = true;
                ConnectbtnText = GenericHelper.GetStringResource("DisconnectTitle");
            }
            else
            {
                IsConnected = false;
                ConnectbtnText = GenericHelper.GetStringResource("ConnectTitle");
            }
        }

        /// <summary>
        /// 输出信号点击
        /// </summary>
        /// <param name="obj"></param>
        [RelayCommand]
        private void OutputClick(object obj)
        {
            if (IsConnected)
            {
                IsOutput = !IsOutput;
                if (IsOutput)
                {
                    OutputbtnText = GenericHelper.GetStringResource("SingalOutputTitle") + GenericHelper.GetStringResource("CloseTitle");
                }
                else
                {
                    OutputbtnText = GenericHelper.GetStringResource("SingalOutputTitle") + GenericHelper.GetStringResource("OpenTitle");
                }
            }
        }

        /// <summary>
        /// 脉冲调制开关点击
        /// </summary>
        /// <param name="obj"></param>
        [RelayCommand]
        private void PWMSwitchClick(object obj)
        {
            if (IsConnected)
            {
                IsPulseModulation = !IsPulseModulation;
                if (IsPulseModulation)
                {
                    PulseModulation = GenericHelper.GetStringResource("PulseModulationTitle") + GenericHelper.GetStringResource("CloseTitle");
                }
                else
                {
                    PulseModulation = GenericHelper.GetStringResource("PulseModulationTitle") + GenericHelper.GetStringResource("OpenTitle");
                }
            }

        }

        /// <summary>
        /// 调制使能点击
        /// </summary>
        /// <param name="obj"></param>
        [RelayCommand]
        private void ModulationEnableClick(object obj)
        {
            if (IsConnected)
            {
                IsModulationEnable = !IsModulationEnable;
                if (IsModulationEnable)
                {
                    ModulationEnable = GenericHelper.GetStringResource("ModulationEnableTitle") + GenericHelper.GetStringResource("CloseTitle");
                }
                else
                {
                    ModulationEnable = GenericHelper.GetStringResource("ModulationEnableTitle") + GenericHelper.GetStringResource("OpenTitle");
                }
            }
        }
    }
}
