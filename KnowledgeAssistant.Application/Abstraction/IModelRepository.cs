namespace KnowledgeAssistant.Application.Abstraction
{
    public interface IModelRepository
    {
        Task<Guid> GetOrCreateModelIdAsync(string modelName, CancellationToken cancellationToken);

        Task<string?> GetModelNameAsync(Guid modelId, CancellationToken cancellationToken);

        Task<int?> GetContextWindowTokensAsync(Guid modelId, CancellationToken cancellationToken);

        Task UpdateContextWindowTokensAsync(Guid modelId, int? contextWindowTokens, CancellationToken cancellationToken);
    }
}
