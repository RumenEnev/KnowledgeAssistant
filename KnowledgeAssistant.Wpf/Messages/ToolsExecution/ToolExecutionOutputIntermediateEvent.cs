using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ToolsExecution;

public record class ToolExecutionOutputIntermediateEvent : MessageBase
{
    public ToolExecutionOutputIntermediateEvent(Guid toolId)
    {
        ToolId = toolId;
    }

    public Guid ToolId { get; }
}