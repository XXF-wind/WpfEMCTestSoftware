using CommunityToolkit.Mvvm.Messaging.Messages;

namespace WpfEMCTestSoftware.Models.EventMessages
{
    public class DialogResultEventMessage : ValueChangedMessage<DialogResult>
    {
        public DialogResultEventMessage(DialogResult state) : base(state) { }
    }
}
