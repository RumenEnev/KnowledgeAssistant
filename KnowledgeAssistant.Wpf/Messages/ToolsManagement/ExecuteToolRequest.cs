using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ToolsManagement;

public record ExecuteToolRequest : MessageBase
{
    public ExecuteToolRequest(Guid toolId, string path, string argumentsJson)
    {
        ToolId = toolId;
        Path = path;
        ArgumentsJson = argumentsJson;
    }

    public Guid ToolId { get; }

    public string Path { get; }

    public string ArgumentsJson { get; }    
}