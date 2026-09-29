using CommunityToolkit.Mvvm.Messaging.Messages;

namespace WpfEMCTestSoftware.Models.EventMessages
{
    public class ExitClickEventMessage : ValueChangedMessage<int>
    {
        public ExitClickEventMessage(int state) : base(state) { }
    }
}
