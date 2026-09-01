using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents;

public record GetRetrievalConfigRequest : MessageBase
{
    public GetRetrievalConfigRequest(int documentId)
    {
        DocumentId = documentId;
    }

    public int DocumentId { get; }
}
