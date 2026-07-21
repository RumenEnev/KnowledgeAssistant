using KnowledgeAssistant.Domain.Documents;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents
{
    public record DocumentsUpdatedEvent : MessageBase
    {
        public DocumentsUpdatedEvent(IEnumerable<Document> documents)
        {
            Documents = documents;
        }

        public IEnumerable<Document> Documents { get; }
    }
}
