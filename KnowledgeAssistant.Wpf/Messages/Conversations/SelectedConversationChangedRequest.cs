using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record SelectedConversationChangedRequest : MessageBase
    {
        public SelectedConversationChangedRequest(Guid conversationId)
        {
            ConversationId = conversationId;
        }

        public Guid ConversationId { get; }
    }
}