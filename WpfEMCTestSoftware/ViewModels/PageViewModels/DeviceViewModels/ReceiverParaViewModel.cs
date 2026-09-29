using ClassLibrary;
using ClassLibrary.Generic;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.EventMessages;

namespace WpfEMCTestSoftware.ViewModels
{
    public partial class ReceiverParaViewModel : BaseViewModel
    {
        #region 变量/属性定义
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
        /// 测试方式选项组
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _testMethods = [.. Enum.GetNames(typeof(TestMethod))];

        /// <summary>
        /// 输入端口选项组
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _inputPorts = [.. Enum.GetNames(typeof(InputPort))];

        /// <summary>
        /// 刷新显示选项组
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _trackDisplays = [.. Enum.GetNames(typeof(TrackDisplay))];

        /// <summary>
        /// 耦合方式选项组
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _couplingMethods = [.. Enum.GetNames(typeof(CouplingMethod))];

        /// <summary>
        /// 底噪扩展选项组
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _bottomNoiseExpans = [.. Enum.GetNames(typeof(BottomNoiseExpan))];

        /// <summary>
        /// 前置预放选项组
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _PreAmplifiers = [.. Enum.GetNames(typeof(PreAmplifier))];

        /// <summary>
        /// 预选器选项组
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _preSelectors = [.. Enum.GetNames(typeof(PreSelector))];

        /// <summary>
        /// 步进类型选项组
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _stepTypes = [.. Enum.GetNames(typeof(StepType))];

        /// <summary>
        /// 检波器选项组
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _cymoscopes = [.. Enum.GetNames(typeof(Cymoscope))];

        /// <summary>
        /// 频率单位选项组
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _freqUnits = [.. Enum.GetNames(typeof(FreqUnit))];

        /// <summary>
        /// 接收机参数信息
        /// </summary>
        [ObservableProperty]
        private ReceiverPara _receiverPara = new();
        #endregion

        public ReceiverParaViewModel()
        {
            IsActive = true;
            //保存回传：设备参数页 -> 本设备子页（把界面值回写到参数对象）
            Messenger.Register<SaveClickEventMessage, int>(this, (int)DeviceSaveMessageToken.DeviceParaToSubPage, (r, m) => OnSaveClick(m));
            //设备信息下发：设备参数页 -> 本设备子页（接收机参数页）
            Messenger.Register<SetDeviceEventMessage, int>(this, (int)DeviceParaMessageToken.DeviceParaToSubPage, (r, m) => RecDeviceInfo(m));
        }

        /// <summary>
        /// 初始化接收机参数
        /// </summary>
        /// <param name="device"></param>
        private void Init(DeviceInfo device)
        {
            //注意：这里必须接住设备列表传下来的"真实对象"（和数据库中那条记录是同一个引用），
            //      页面上各项参数的 TwoWay 绑定才会写进这条记录，再由设备参数页统一保存入库。
            //      不能只赋值 Id 后 new 一个空对象，否则改了也存不进数据库。
            if (device is ReceiverPara para)
            {
                //页面的 4 个检波器下拉分别绑定 Cymoscopes[0]~[3]，历史数据为空时补足 4 位，
                //否则绑定取不到下标，选了也写不回数据库
                para.Cymoscopes ??= [];
                while (para.Cymoscopes.Count < 4)
                {
                    para.Cymoscopes.Add(Cymoscope.无);
                }
                ReceiverPara = para;
            }
        }

        /// <summary>
        /// 接收设置设备事件消息
        /// </summary>
        /// <param name="message"></param>
        public void RecDeviceInfo(SetDeviceEventMessage message)
        {
            // 初始化接收机参数.
            Init(message.Value);
        }

        /// <summary>
        /// 接收保存设备参数按钮点击的消息
        /// </summary>
        /// <param name="message">事件消息</param>
        public void OnSaveClick(SaveClickEventMessage message)
        {
            //参数回写完毕：本设备子页 -> 设备参数页（由设备参数页统一落库）
            Messenger.Send(new SaveClickEventMessage(true), (int)DeviceSaveMessageToken.SubPageToDevicePara);
        }
    }
}
