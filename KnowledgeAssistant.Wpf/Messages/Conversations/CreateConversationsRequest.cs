using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record CreateConversationsRequest : MessageBase
    {
        public CreateConversationsRequest(string provider, string model)
        {
            Provider = provider;
            Model = model;
        }

        public string Provider { get; private set; }

        public string Model { get; private set; }
    }
}