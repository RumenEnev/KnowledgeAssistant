using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ToolsManagement
{
    public record ToolDeletedEvent : MessageBase
    {
        public ToolDeletedEvent(Guid id)
        {
            Id = id;
        }

        public Guid Id { get; }
    }
}
