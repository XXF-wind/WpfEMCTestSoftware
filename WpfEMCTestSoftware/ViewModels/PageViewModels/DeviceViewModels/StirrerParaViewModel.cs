using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Models.DeviceModels;
using WpfEMCTestSoftware.Models.EventMessages;

namespace WpfEMCTestSoftware.ViewModels.PageViewModels.DeviceViewModels
{
    public partial class StirrerParaViewModel : BaseViewModel
    {
        public StirrerParaViewModel()
        {
            IsActive = true;
            //保存回传：设备参数页 -> 本设备子页（把界面值回写到参数对象）
            Messenger.Register<SaveClickEventMessage, int>(this, (int)DeviceSaveMessageToken.DeviceParaToSubPage, (r, m) => OnSaveClick(m));
            //设备信息下发：设备参数页 -> 本设备子页（搅拌器参数页）
            Messenger.Register<SetDeviceEventMessage, int>(this, (int)DeviceParaMessageToken.DeviceParaToSubPage, (r, m) => RecDeviceInfo(m));
        }
        [ObservableProperty]
        private StirrerPara _mStirrerPara = new();

        /// <summary>
        /// 接收保存设备参数按钮点击的消息
        /// </summary>
        /// <param name="message">事件消息</param>
        public void OnSaveClick(SaveClickEventMessage message)
        {
            //参数回写完毕：本设备子页 -> 设备参数页（由设备参数页统一落库）
            Messenger.Send(new SaveClickEventMessage(true), (int)DeviceSaveMessageToken.SubPageToDevicePara);
        }
        /// <summary>
        /// 接收设置设备事件消息
        /// </summary>
        /// <param name="message"></param>
        public void RecDeviceInfo(SetDeviceEventMessage message)
        {
            MStirrerPara = (StirrerPara)message.Value;
        }

    }
}
