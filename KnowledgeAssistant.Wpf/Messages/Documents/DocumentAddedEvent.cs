using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents;

public record DocumentAddedEvent : MessageBase
{
    public DocumentAddedEvent(int documentId, int chunksCount)
    {
        DocumentId = documentId;
        ChunksCount = chunksCount;
    }

    public int DocumentId { get; }

    public int ChunksCount { get; }
}