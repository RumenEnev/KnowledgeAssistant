using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents;

public record UpdateTopicRequest : MessageBase
{
    public UpdateTopicRequest(int topicId, string name, int? parentId)
    {
        TopicId = topicId;
        Name = name;
        ParentId = parentId;
    }

    public int TopicId { get; init; }

    public string Name { get; init; }

    public int? ParentId { get; init; }
}