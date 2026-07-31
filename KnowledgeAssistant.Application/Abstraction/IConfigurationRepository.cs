using KnowledgeAssistant.Domain;

namespace KnowledgeAssistant.Application.Abstraction
{
    public interface IConfigurationRepository
    {
        Task UpsertSelectedModelAsync(string selectedModel, CancellationToken cancellationToken);

        Task<string?> GetSelectedModelAsync(CancellationToken cancellationToken);

        Task<(int ChunkTargetSizeChars, int ChunkOverlapChars)> GetChunkingSettingsAsync(CancellationToken cancellationToken);

        Task UpsertChunkingSettingsAsync(int chunkTargetSizeChars, int chunkOverlapChars, CancellationToken cancellationToken);

        Task<Guid> AddRepositoryAsync(string name, string rootPath, string? description, CancellationToken cancellationToken);

        Task<SourceRepository?> GetRepositoryByIdAsync(Guid id, CancellationToken cancellationToken);

        Task<SourceRepository?> GetRepositoryByNameAsync(string name, CancellationToken cancellationToken);

        Task<IReadOnlyList<SourceRepository>> GetRepositoriesAsync(CancellationToken cancellationToken);

        Task<bool> UpdateRepositoryAsync(Guid id, string? name, string? rootPath, string? description, CancellationToken cancellationToken);

        Task<bool> DeleteRepositoryAsync(Guid id, CancellationToken cancellationToken);
    }
}