using CommunityToolkit.Mvvm.Messaging.Messages;

namespace WpfEMCTestSoftware.Models.EventMessages
{
    /// <summary>
    /// 通用点击事件消息
    /// </summary>
    public class UniversalClickEventMessage : ValueChangedMessage<string>
    {
        public UniversalClickEventMessage(string type) : base(type) { }
    }
}
