namespace KnowledgeAssistant.Application.Abstraction
{
    public record ModelFlags(bool InternalUseOnly, bool CanCallTools);

    public interface IModelRepository
    {
        Task<Guid> GetOrCreateModelIdAsync(string modelName, CancellationToken cancellationToken);

        Task<string?> GetModelNameAsync(Guid modelId, CancellationToken cancellationToken);

        Task<ModelFlags> GetModelFlagsAsync(Guid modelId, CancellationToken cancellationToken);

        Task UpdateModelFlagsAsync(Guid modelId, bool internalUseOnly, bool canCallTools, CancellationToken cancellationToken);
    }
}