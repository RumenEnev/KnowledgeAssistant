using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ToolsManagement
{
    public record ToolUpdatedEvent : MessageBase
    {
        public ToolUpdatedEvent(Guid id)
        {
            Id = id;
        }

        public Guid Id { get; }
    }
}
