using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EMCDeviceLibrary;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.EventMessages;

namespace WpfEMCTestSoftware.ViewModels
{
    public partial class AmpifierTestViewModel : BaseViewModel
    {
        #region 变量/属性定义
        private SockConnect? Sock = null;

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
        /// 是否开启射频
        /// </summary>
        [ObservableProperty]
        private bool _isRF = false;

        /// <summary>
        /// 射频按钮文本
        /// </summary>
        [ObservableProperty]
        private string _rfbtnText = "RF ON";

        /// <summary>
        /// 频率值
        /// </summary>
        [ObservableProperty]
        private double _freqValue;

        /// <summary>
        /// 设置频率值
        /// </summary>
        [ObservableProperty]
        private double _setFreq;

        /// <summary>
        /// 正向功率
        /// </summary>
        [ObservableProperty]
        private double _forwardPower;

        /// <summary>
        /// 反向功率
        /// </summary>
        [ObservableProperty]
        private double _reversePower;

        /// <summary>
        /// 告警状态
        /// </summary>
        [ObservableProperty]
        private int _warnStatus = 0;

        private DeviceInfo DeviceInfo = new();
        #endregion

        public AmpifierTestViewModel()
        {
            IsActive = true;
            //设备信息下发：设备参数页 -> 功放测试子页
            Messenger.Register<SetDeviceEventMessage, int>(this, (int)DeviceParaMessageToken.DeviceParaToSubPage, (r, msg) => OnGainPara(msg));
            Initializer();
        }

        private void Initializer()
        {
            ConnectbtnText = GenericHelper.GetStringResource("ConnectTitle");
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
        /// RF开关切换
        /// </summary>
        /// <param name="obj"></param>
        [RelayCommand]
        private void RFSwitch(object obj)
        {
            if (IsRF)
            {
                // 关闭射频
                Sock?.SendData("RF OFF");
                IsRF = false;
                RfbtnText = "RF ON";
            }
            else
            {
                // 打开射频
                Sock?.SendData("RF ON");
                IsRF = true;
                RfbtnText = "RF OFF";
            }
        }
    }
}
