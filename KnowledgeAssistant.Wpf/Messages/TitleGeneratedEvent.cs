using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages
{
    public record TitleGeneratedEvent : MessageBase
    {
        public TitleGeneratedEvent(string title, Guid conversationId)
        {
            Title = title;
            ConversationId = conversationId;
        }

        public string Title { get; }

        public Guid ConversationId { get; }
    }
}