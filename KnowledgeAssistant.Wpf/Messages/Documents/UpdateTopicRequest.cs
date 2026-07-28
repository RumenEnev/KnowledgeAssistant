using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents
{
    public record UpdateTopicRequest : MessageBase
    {
        public UpdateTopicRequest(int topicId, string name)
        {
            TopicId = topicId;
            Name = name;
        }

        public int TopicId { get; init; }

        public string Name { get; init; }
    }
}
