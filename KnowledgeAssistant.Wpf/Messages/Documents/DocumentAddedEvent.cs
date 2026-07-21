using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents
{
    public record DocumentAddedEvent : MessageBase
    {
        public DocumentAddedEvent(int documentId)
        {
            DocumentId = documentId;
        }

        public int DocumentId { get; }
    }
}
