using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using WpfEMCTestSoftware.Core;
using WpfEMCTestSoftware.Models.DeviceModels;
using WpfEMCTestSoftware.Models.EventMessages;

namespace WpfEMCTestSoftware.ViewModels
{
    /// <summary>
    /// 移相衰减器参数视图模型
    /// 保存链路与功放 AmpifierParaViewModel 保持一致：
    /// 页面(确认) -> 本页 OnSaveClick(界面值回写参数对象) -> 设备参数页 SaveDeviceInfo(统一落库)
    /// </summary>
    public partial class PhaseAttParaViewModel : BaseViewModel
    {
        #region 变量/属性定义
        /// <summary>
        /// 移相衰减器参数信息（与数据库中该设备的记录是同一个引用，页面直接双向绑定它）
        /// </summary>
        [ObservableProperty]
        private PhaseAttPara _phaseAttPara = new();
        #endregion

        public PhaseAttParaViewModel()
        {
            IsActive = true;
            //保存回传：设备参数页 -> 本设备子页（把界面值回写到参数对象）
            Messenger.Register<SaveClickEventMessage, int>(this, (int)DeviceSaveMessageToken.DeviceParaToSubPage, (r, m) => OnSaveClick(m));
            //设备信息下发：设备参数页 -> 本设备子页（移相衰减器参数页）
            Messenger.Register<SetDeviceEventMessage, int>(this, (int)DeviceParaMessageToken.DeviceParaToSubPage, (r, m) => RecDeviceInfo(m));
        }

        /// <summary>
        /// 接收设置设备事件消息
        /// </summary>
        /// <param name="message"></param>
        public void RecDeviceInfo(SetDeviceEventMessage message)
        {
            //注意：这里必须接住设备列表传下来的"真实对象"（和数据库中那条记录是同一个引用），
            //      页面上改的值才会写进这条记录，再由设备参数页统一保存入库。
            //      不能保留 new 出来的空对象，否则改了也存不进数据库。
            if (message.Value is PhaseAttPara para)
            {
                PhaseAttPara = para;
            }
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
