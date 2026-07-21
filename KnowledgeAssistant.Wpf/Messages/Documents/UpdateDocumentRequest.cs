using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents
{
    public record UpdateDocumentRequest : MessageBase
    {
        public UpdateDocumentRequest(int documentId, string title, string text, IEnumerable<string> topics)
        {
            DocumentId = documentId;
            Title = title;
            Text = text;
            Topics = topics;
        }

        public int DocumentId { get; }

        public string Title { get; }

        public string Text { get; }

        public IEnumerable<string> Topics { get; }
    }
}
