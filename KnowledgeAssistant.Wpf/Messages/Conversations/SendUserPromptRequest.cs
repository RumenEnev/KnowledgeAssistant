using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record SendUserMessageRequest : MessageBase
    {
        public SendUserMessageRequest(string prompt, string provider, string model)
        {
            Prompt = prompt;
            Provider = provider;
            Model = model;
        }

        public string Prompt { get; }

        public string Provider { get; }

        public string Model { get; }
    }
}