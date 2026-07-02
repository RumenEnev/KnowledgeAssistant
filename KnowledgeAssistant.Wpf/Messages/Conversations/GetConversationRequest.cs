using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record GetConversationRequest : MessageBase
    {
        public GetConversationRequest(Guid conversationId)
        {
            ConversationId = conversationId;
        }

        public Guid ConversationId { get; }
    }
}