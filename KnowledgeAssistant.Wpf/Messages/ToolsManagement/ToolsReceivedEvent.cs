using KnowledgeAssistant.Contracts.Tools;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ToolsManagement
{
    public record ToolsReceivedEvent : MessageBase
    {
        public ToolsReceivedEvent(IEnumerable<ToolDto> tools, string? Error = null)
        {
            Tools = tools;
            ErrorMessage = Error;
        }

        public IEnumerable<ToolDto> Tools { get; }

        public string? ErrorMessage { get; }
    }
}
