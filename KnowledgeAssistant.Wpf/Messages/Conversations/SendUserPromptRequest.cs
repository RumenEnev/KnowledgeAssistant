using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record SendUserMessageRequest : MessageBase
    {
        public SendUserMessageRequest(string prompt)
        {
            Prompt = prompt;
        }

        public string Prompt { get; }
    }
}