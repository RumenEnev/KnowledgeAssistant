using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents
{
    public record DeleteDocumentRequest : MessageBase
    {
        public DeleteDocumentRequest(int documentId)
        {
            DocumentId = documentId;
        }

        public int DocumentId { get; }
    }
}
