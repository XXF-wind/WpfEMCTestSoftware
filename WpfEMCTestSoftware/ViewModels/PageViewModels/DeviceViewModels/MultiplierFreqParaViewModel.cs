using ClassLibrary;
using ClassLibrary.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.EventMessages;

namespace WpfEMCTestSoftware.ViewModels
{
    public partial class MultiplierFreqParaViewModel : BaseViewModel
    {
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
        /// 倍频器参数信息
        /// </summary>
        [ObservableProperty]
        private MultiplierFreqPara _multiplierFreqPara = new();

        public MultiplierFreqParaViewModel()
        {
            IsActive = true;
            //保存回传：设备参数页 -> 本设备子页（把界面值回写到参数对象）
            Messenger.Register<SaveClickEventMessage, int>(this, (int)DeviceSaveMessageToken.DeviceParaToSubPage, (r, m) => OnSaveClick(m));
            //设备信息下发：设备参数页 -> 本设备子页（倍频器参数页）
            Messenger.Register<SetDeviceEventMessage, int>(this, (int)DeviceParaMessageToken.DeviceParaToSubPage, (r, m) => RecDeviceInfo(m));
        }

        private void Init(DeviceInfo device)
        {
            //注意：这里必须接住设备列表传下来的"真实对象"（和数据库中那条记录是同一个引用），
            //      页面 TwoWay 绑定的 MaxAtt / StepAtt 才会写进这条记录，再由设备参数页统一保存入库。
            //      不能只赋值 Id 后 new 一个空对象，否则改了也存不进数据库。
            if (device is MultiplierFreqPara para)
            {
                MultiplierFreqPara = para;
            }
        }

        /// <summary>
        /// 接收设置设备事件消息
        /// </summary>
        /// <param name="message"></param>
        public void RecDeviceInfo(SetDeviceEventMessage message)
        {
            // 初始化倍频器参数.
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
