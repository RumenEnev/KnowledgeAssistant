using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record RefreshConversationRequest : MessageBase
    {
        public RefreshConversationRequest(Guid conversationId)
        {
            ConversationId = conversationId;
        }

        public Guid ConversationId { get; }
    }
}
