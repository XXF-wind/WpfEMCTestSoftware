using CommunityToolkit.Mvvm.Messaging.Messages;

namespace WpfEMCTestSoftware.Models.EventMessages
{
    /// <summary>
    /// 传递数据消息
    /// </summary>
    public class TransmitDataEventMessage : ValueChangedMessage<string>
    {
        public TransmitDataEventMessage(string data) : base(data) { }
    }
}
