using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record UpdateConversationTitleRequest : MessageBase
    {
        public UpdateConversationTitleRequest(Guid conversationId, string newTitle)
        {
            ConversationId = conversationId;
            NewTitle = newTitle;
        }
        public Guid ConversationId { get; init; }

        public string NewTitle { get; init; }
    }
}