using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents
{
    public record AddDocumentRequest : MessageBase
    {
        public AddDocumentRequest(string title, string text, IEnumerable<string> topics)
        {
            Title = title;
            Text = text;
            Topics = topics;
        }

        public string Title { get; }

        public string Text { get; }

        public IEnumerable<string> Topics { get; }
    }
}
