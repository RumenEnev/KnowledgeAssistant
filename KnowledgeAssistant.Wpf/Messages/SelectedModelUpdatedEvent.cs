using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages
{
    public record SelectedModelUpdatedEvent : MessageBase
    {
        public SelectedModelUpdatedEvent(string? selectedModel)
        {
            SelectedModel = selectedModel;
        }

        public string? SelectedModel { get; }
    }
}
