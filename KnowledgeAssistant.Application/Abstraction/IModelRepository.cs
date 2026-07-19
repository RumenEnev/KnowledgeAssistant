namespace KnowledgeAssistant.Application.Abstraction
{
    public interface IModelRepository
    {
        /// <summary>
        /// Resolves the Id of a model by name, auto-registering it in ai_interactions.models
        /// (provider = "ollama") if it isn't already known.
        /// </summary>
        Task<Guid> GetOrCreateModelIdAsync(string modelName, CancellationToken cancellationToken);

        Task<string?> GetModelNameAsync(Guid modelId, CancellationToken cancellationToken);
    }
}
