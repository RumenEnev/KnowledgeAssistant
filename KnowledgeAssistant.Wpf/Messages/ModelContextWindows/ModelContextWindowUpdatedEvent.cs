using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ModelContextWindows
{
    public record ModelContextWindowUpdatedEvent : MessageBase
    {
        public ModelContextWindowUpdatedEvent(Guid modelId, string modelName, int? contextWindowTokens)
        {
            ModelId = modelId;
            ModelName = modelName;
            ContextWindowTokens = contextWindowTokens;
        }

        public Guid ModelId { get; }

        public string ModelName { get; }

        public int? ContextWindowTokens { get; }
    }
}
