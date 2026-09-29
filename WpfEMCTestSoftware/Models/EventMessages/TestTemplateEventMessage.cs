using CommunityToolkit.Mvvm.Messaging.Messages;
using static WpfEMCTestSoftware.Models.globalDefinition;

namespace WpfEMCTestSoftware.Models.EventMessages
{
    public class TestTemplateEventMessage : ValueChangedMessage<TestTemplateConfigFlagEnum>
    {
        public TestTemplateEventMessage(TestTemplateConfigFlagEnum state) : base(state) { }
    }
}
