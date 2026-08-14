using KnowledgeAssistant.Contracts.Tools;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ToolsManagement
{
    public record ToolCreatedEvent : MessageBase
    {
        public ToolCreatedEvent(ToolDto tool)
        {
            Tool = tool;
        }

        public ToolDto Tool { get; }
    }
}
