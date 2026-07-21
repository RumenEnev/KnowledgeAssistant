using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents
{
    public record DocumentUpdatedEvent : MessageBase
    {
        public DocumentUpdatedEvent(int documentId)
        {
            DocumentId = documentId;
        }

        public int DocumentId { get; }
    }
}
