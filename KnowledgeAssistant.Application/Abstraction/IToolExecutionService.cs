using KnowledgeAssistant.Domain;

namespace KnowledgeAssistant.Application.Abstraction;

public interface IToolExecutionService
{
    Task<string> ExecuteAsync(ToolDefinitionEntity tool, string argumentsJson, CancellationToken cancellationToken);
}