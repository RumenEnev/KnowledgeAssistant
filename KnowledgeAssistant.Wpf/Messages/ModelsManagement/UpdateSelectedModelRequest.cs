using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ModelsManagement
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
