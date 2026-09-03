using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ModelsManagement
{
    public record GetAvailableModelsRequest : MessageBase
    {
        public GetAvailableModelsRequest(string provider)
        {
            Provider = provider;
        }

        public string Provider { get; }
    }
}