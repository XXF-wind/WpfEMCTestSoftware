using CommunityToolkit.Mvvm.Messaging.Messages;

namespace WpfEMCTestSoftware.Models.EventMessages
{
    /// <summary>
    /// 设置设备信息消息
    /// </summary>
    public class SetDeviceEventMessage : ValueChangedMessage<DeviceInfo>
    {
        public SetDeviceEventMessage(DeviceInfo info) : base(info) { }
    }
}
