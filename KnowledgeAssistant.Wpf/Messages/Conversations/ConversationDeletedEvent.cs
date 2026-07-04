using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record ConversationDeletedEvent :MessageBase
    {
        public ConversationDeletedEvent(Guid conversationId)
        {
            ConversationId = conversationId;
        }

        public Guid ConversationId { get; }
    }
}