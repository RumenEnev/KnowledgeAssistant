using KnowledgeAssistant.Domain.Documents;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents
{
    public record TopicsUpdatedEvent : MessageBase
    {
        public TopicsUpdatedEvent(IEnumerable<Topic> topics)
        {
            Topics = topics;
        }

        public IEnumerable<Topic> Topics { get; }
    }
}
