using CommunityToolkit.Mvvm.Messaging.Messages;

namespace WpfEMCTestSoftware.Models.EventMessages
{
    public class SaveClickEventMessage : ValueChangedMessage<bool>
    {
        public SaveClickEventMessage(bool state) : base(state) { }
    }
}
