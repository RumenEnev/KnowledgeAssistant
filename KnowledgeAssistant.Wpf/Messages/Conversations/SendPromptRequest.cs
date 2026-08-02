using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record SendPromptRequest : MessageBase
    {
        public SendPromptRequest(string prompt, string model, string role, Guid? conversationId)
        {
            Prompt = prompt;
            Model = model;
            Role = role;
            ConversationId = conversationId;
        }

        public string Prompt { get; }

        public string Model { get; }

        public string Role { get; }

        public Guid? ConversationId { get; }
    }
}