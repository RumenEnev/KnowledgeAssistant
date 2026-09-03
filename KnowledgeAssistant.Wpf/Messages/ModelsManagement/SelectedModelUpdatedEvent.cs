using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ModelsManagement
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
