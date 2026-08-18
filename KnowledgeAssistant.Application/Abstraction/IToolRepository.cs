using KnowledgeAssistant.Contracts.Enums;
using KnowledgeAssistant.Domain;

namespace KnowledgeAssistant.Application.Abstraction;

public interface IToolRepository
{
    Task<Guid> AddToolAsync(string name, string description, string parametersJsonSchema, bool isEnabled, CancellationToken cancellationToken);

    Task<ToolDefinitionEntity?> GetToolByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ToolDefinitionEntity>> GetToolsAsync(MessageSource? source, CancellationToken cancellationToken);

    Task<IReadOnlyList<ToolDefinitionEntity>> GetEnabledToolsAsync(MessageSource? source, CancellationToken cancellationToken);

    Task<IReadOnlyList<ToolDefinition>> GetEnabledToolDefinitionsAsync(MessageSource? source, CancellationToken cancellationToken);

    Task<bool> UpdateToolAsync(Guid id, string? name, string? description, string? parametersJsonSchema, bool? isEnabled, CancellationToken cancellationToken);

    Task<bool> DeleteToolAsync(Guid id, CancellationToken cancellationToken);
}