using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ModelContextWindows
{
    public record GetModelContextWindowsRequest : MessageBase
    {
        public GetModelContextWindowsRequest(string provider)
        {
            Provider = provider;
        }

        public string Provider { get; set; }
    }
}