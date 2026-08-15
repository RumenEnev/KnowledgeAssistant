using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages
{
    public record AvailableModelsUpdatedEvent : MessageBase
    {
        public AvailableModelsUpdatedEvent(IEnumerable<AvailableModelInfo> models)
        {
            Models = models;
        }

        public IEnumerable<AvailableModelInfo> Models { get; }
    }
}