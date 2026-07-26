using KnowledgeAssistant.Contracts.Enums;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents
{
    public record AddDocumentRequest : MessageBase
    {
        public AddDocumentRequest(string title, string text, DocumentType documentType, IEnumerable<string> topics)
        {
            Title = title;
            Text = text;
            DocumentType = documentType;
            Topics = topics;
        }

        public string Title { get; }

        public string Text { get; }
        public DocumentType DocumentType { get; }

        public IEnumerable<string> Topics { get; }
    }
}
