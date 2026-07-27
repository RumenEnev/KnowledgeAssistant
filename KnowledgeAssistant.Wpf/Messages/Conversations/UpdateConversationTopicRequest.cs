using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Conversations
{
    public record UpdateConversationTopicRequest : MessageBase
    {
        public UpdateConversationTopicRequest(Guid conversationId, int? topicId)
        {
            ConversationId = conversationId;
            TopicId = topicId;
        }

        public Guid ConversationId { get; init; }

        public int? TopicId { get; init; }
    }
}
