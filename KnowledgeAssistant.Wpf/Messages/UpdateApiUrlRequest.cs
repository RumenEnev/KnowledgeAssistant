using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages
{
    public record UpdateApiUrlRequest : MessageBase
    {
        public UpdateApiUrlRequest(string url)
        {
            Url = url;
        }

        public string Url { get; }
    }
}