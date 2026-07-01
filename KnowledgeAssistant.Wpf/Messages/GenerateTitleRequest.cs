using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages
{
    public record GenerateTitleRequest : MessageBase
    {
        public GenerateTitleRequest(string userPrompt, string model, Guid conversationId)
        {
            UserPrompt = userPrompt;
            Model = model;
            ConversationId = conversationId;
        }

        public string UserPrompt { get; }

        public string Model { get; }

        public Guid ConversationId { get; }
    }
}