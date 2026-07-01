using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages
{
    public record AvailableModelsUpdatedEvent : MessageBase
    {
        public AvailableModelsUpdatedEvent(IEnumerable<string> models)
        {
            Models = models;
        }

        public IEnumerable<string> Models { get; }
    }
}