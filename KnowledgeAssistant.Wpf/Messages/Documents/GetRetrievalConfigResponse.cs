using KnowledgeAssistant.Domain.Documents;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents;

public record GetRetrievalConfigResponse : MessageBase
{
    public GetRetrievalConfigResponse(DocumentRetrievalConfig? config)
    {
        Config = config;
    }

    public DocumentRetrievalConfig? Config { get; }
}