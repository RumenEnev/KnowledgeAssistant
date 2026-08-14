using KnowledgeAssistant.Domain;

namespace KnowledgeAssistant.Application.Abstraction;

public interface IToolRepository
{
    Task<Guid> AddToolAsync(string name, string description, string parametersJsonSchema, bool isEnabled, CancellationToken cancellationToken);

    Task<ToolDefinitionEntity?> GetToolByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ToolDefinitionEntity>> GetToolsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ToolDefinitionEntity>> GetEnabledToolsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ToolDefinition>> GetEnabledToolDefinitionsAsync(CancellationToken cancellationToken);

    Task<bool> UpdateToolAsync(Guid id, string? name, string? description, string? parametersJsonSchema, bool? isEnabled, CancellationToken cancellationToken);

    Task<bool> DeleteToolAsync(Guid id, CancellationToken cancellationToken);
}