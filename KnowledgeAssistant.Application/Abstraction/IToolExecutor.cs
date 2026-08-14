using KnowledgeAssistant.Domain;

namespace KnowledgeAssistant.Application.Abstraction
{
    public interface IToolExecutor
    {
        Task<string> ExecuteAsync(ToolDefinitionEntity tool, string argumentsJson, CancellationToken cancellationToken);
    }
}
