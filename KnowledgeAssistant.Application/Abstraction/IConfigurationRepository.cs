namespace KnowledgeAssistant.Application.Abstraction
{
    public interface IConfigurationRepository
    {
        Task UpsertSelectedModelAsync(string selectedModel, CancellationToken cancellationToken);

        Task<string?> GetSelectedModelAsync(CancellationToken cancellationToken);

        Task<(int ChunkTargetSizeChars, int ChunkOverlapChars)> GetChunkingSettingsAsync(CancellationToken cancellationToken);

        Task UpsertChunkingSettingsAsync(int chunkTargetSizeChars, int chunkOverlapChars, CancellationToken cancellationToken);
    }
}
