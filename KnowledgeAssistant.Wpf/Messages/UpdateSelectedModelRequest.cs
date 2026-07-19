using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages
{
    public record UpdateSelectedModelRequest : MessageBase
    {
        public UpdateSelectedModelRequest(string selectedModel)
        {
            SelectedModel = selectedModel;
        }

        public string SelectedModel { get; }
    }
}
