using KnowledgeAssistant.Domain.Documents;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents;

public record SaveRetrievalConfigRequest : MessageBase
{
    public SaveRetrievalConfigRequest(DocumentRetrievalConfig config)
    {
        Config = config;
    }
    public DocumentRetrievalConfig Config { get; }
}