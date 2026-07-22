using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ModelContextWindows
{
    public record UpdateModelContextWindowRequest : MessageBase
    {
        public UpdateModelContextWindowRequest(Guid modelId, string modelName, int? contextWindowTokens)
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
