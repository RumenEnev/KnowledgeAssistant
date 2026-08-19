using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents;

public record CreateTopicRequest : MessageBase
{
    public CreateTopicRequest(string name, int? parentId = null)
    {
        Name = name;
        ParentId = parentId;
    }

    public string Name { get; init; }

    public int? ParentId { get; init; }
}