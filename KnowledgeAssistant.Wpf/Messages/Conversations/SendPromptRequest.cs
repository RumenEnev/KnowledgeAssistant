using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record SendUserMessageRequest : MessageBase
    {
        public SendUserMessageRequest(string prompt, string model, Guid? conversationId = null)
        {
            Prompt = prompt;
            Model = model;
            ConversationId = conversationId;
        }

        public string Prompt { get; }

        public string Model { get; }

        public Guid? ConversationId { get; }
    }
}