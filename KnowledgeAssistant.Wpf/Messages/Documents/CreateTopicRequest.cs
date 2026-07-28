using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents
{
    public record CreateTopicRequest : MessageBase
    {
        public CreateTopicRequest(string name)
        {
            Name = name;
        }

        public string Name { get; init; }
    }
}
