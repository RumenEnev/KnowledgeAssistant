using KnowledgeAssistant.Domain.Documents;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents;

public record RetrievalConfigUpdatedEvent : MessageBase
{
    public RetrievalConfigUpdatedEvent(DocumentRetrievalConfig config)
    {
        Config = config;
    }
    public DocumentRetrievalConfig Config { get; }
}