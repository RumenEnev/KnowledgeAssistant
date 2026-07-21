using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents
{
    public record DocumentDeletedEvent : MessageBase
    {
        public DocumentDeletedEvent(int documentId)
        {
            DocumentId = documentId;
        }

        public int DocumentId { get; }
    }
}
