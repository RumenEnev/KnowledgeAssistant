using KnowledgeAssistant.Infrastructure.Dto;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ModelsManagement
{
    public record AvailableModelsUpdatedEvent : MessageBase
    {
        public AvailableModelsUpdatedEvent(string provider, IEnumerable<AvailableModelInfo> models)
        {
            Provider = provider;
            Models = models.ToArray();
        }

        public string Provider { get; }

        public IReadOnlyCollection<AvailableModelInfo> Models { get; }
    }
}