using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ToolsManagement
{
    public record DeleteToolRequest : MessageBase
    {
        public DeleteToolRequest(Guid id)
        {
            Id = id;
        }

        public Guid Id { get; set; }
    }
}
