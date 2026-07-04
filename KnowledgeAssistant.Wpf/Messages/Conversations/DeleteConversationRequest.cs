using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record DeleteConversationRequest : MessageBase
    {
        public DeleteConversationRequest(Guid conversationId)
        {
            ConversationId = conversationId;
        }

        public Guid ConversationId { get; }
    }
}