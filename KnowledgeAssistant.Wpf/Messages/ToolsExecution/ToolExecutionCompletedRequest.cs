using KnowledgeAssistant.Contracts.Dto;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ToolsExecution;

public record ToolExecutionCompletedRequest : MessageBase
{
    public ToolExecutionCompletedRequest(Guid toolId, ToolResult result)
    {
        ToolId = toolId;
        Result = result;
    }

    public Guid ToolId { get; }

    public ToolResult Result { get; }
}