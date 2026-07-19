namespace KnowledgeAssistant.Application.Abstraction
{
    public interface IConfigurationRepository
    {
        Task UpsertSelectedModelAsync(string selectedModel, CancellationToken cancellationToken);

        Task<string?> GetSelectedModelAsync(CancellationToken cancellationToken);
    }
}
