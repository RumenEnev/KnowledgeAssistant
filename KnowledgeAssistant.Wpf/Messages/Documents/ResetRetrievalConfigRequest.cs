using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents;

public record ResetRetrievalConfigRequest : MessageBase
{
    public ResetRetrievalConfigRequest(int documentId)
    {
        DocumentId = documentId;
    }

    public int DocumentId { get; }
}
