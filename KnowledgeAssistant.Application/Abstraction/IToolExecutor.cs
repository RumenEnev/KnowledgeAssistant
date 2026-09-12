using KnowledgeAssistant.Contracts.Enums;
using KnowledgeAssistant.Domain.Entities;

namespace KnowledgeAssistant.Application.Abstraction
{
    public interface IToolExecutor
    {
        Task<string> ExecuteAsync(ToolDefinitionEntity tool, string argumentsJson, CancellationToken cancellationToken);
    }
}
