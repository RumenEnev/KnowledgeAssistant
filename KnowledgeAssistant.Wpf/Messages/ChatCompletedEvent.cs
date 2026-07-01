using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages
{
    public record ChatCompletedEvent : MessageBase
    {
        public ChatCompletedEvent(Guid? conversationId)
        {
            ConversationId = conversationId;
        }

        public Guid? ConversationId { get; }
    }
}