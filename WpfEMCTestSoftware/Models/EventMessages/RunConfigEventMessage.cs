using CommunityToolkit.Mvvm.Messaging.Messages;

namespace WpfEMCTestSoftware.Models.EventMessages
{
    public class RunConfigEventMessage : ValueChangedMessage<string?>
    {
        public RunConfigEventMessage(string? state) : base(state) { }
    }
}
