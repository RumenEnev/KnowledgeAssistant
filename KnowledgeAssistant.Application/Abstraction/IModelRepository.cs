using KnowledgeAssistant.Contracts.Dto.Model;

namespace KnowledgeAssistant.Application.Abstraction;

public interface IModelRepository
{
    Task<Guid> GetOrCreateModelIdAsync(string modelName, CancellationToken cancellationToken);

    Task<string?> GetModelNameAsync(Guid modelId, CancellationToken cancellationToken);

    Task<ModelFlagsDto> GetModelFlagsAsync(Guid modelId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> GetEmbeddingModelsAsync(CancellationToken cancellationToken);

    Task UpdateModelFlagsAsync(Guid modelId, bool internalUseOnly, bool canCallTools, bool isFavorite, CancellationToken cancellationToken);
}