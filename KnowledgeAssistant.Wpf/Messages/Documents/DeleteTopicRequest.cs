using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents
{
    public record DeleteTopicRequest : MessageBase
    {
        public DeleteTopicRequest(int topicId)
        {
            TopicId = topicId;
        }

        public int TopicId { get; init; }
    }
}
