using CommunityToolkit.Mvvm.Messaging.Messages;

namespace WpfEMCTestSoftware.Models.EventMessages
{
    /// <summary>
    /// 保存设备参数信息消息
    /// </summary>
    public class SaveDeviceEventMessage : ValueChangedMessage<DeviceInfo>
    {
        public SaveDeviceEventMessage(DeviceInfo info) : base(info) { }
    }
}
