using Dapper;
using KnowledgeAssistant.Application.Abstraction;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace KnowledgeAssistant.Application.Services
{
    public class ConfigurationRepository : IConfigurationRepository
    {
        // Fixed id for the single, global application configuration row.
        private static readonly Guid GlobalConfigurationId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        // Used when the configuration row doesn't exist yet (fresh install, before any save).
        private const int DefaultChunkTargetSizeChars = 1000;
        private const int DefaultChunkOverlapChars = 150;

        private readonly string _connectionString;
        private readonly IModelRepository _modelRepository;

        public ConfigurationRepository(IConfiguration configuration, IModelRepository modelRepository)
        {
            _connectionString = configuration.GetConnectionString("KnowledgeAssistant")
                ?? throw new InvalidOperationException("Connection string is missing.");
            _modelRepository = modelRepository;
        }

        public async Task UpsertSelectedModelAsync(string selectedModel, CancellationToken cancellationToken)
        {
            var modelId = await _modelRepository.GetOrCreateModelIdAsync(selectedModel, cancellationToken);

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var updateQuery = "UPDATE ai_interactions.configuration SET selected_model_id = @ModelId WHERE id = @Id";
            var rowsAffected = await connection.ExecuteAsync(updateQuery, new
            {
                Id = GlobalConfigurationId,
                ModelId = modelId
            });

            if (rowsAffected == 0)
            {
                var insertQuery = "INSERT INTO ai_interactions.configuration (id, selected_model_id) VALUES (@Id, @ModelId)";
                await connection.ExecuteAsync(insertQuery, new
                {
                    Id = GlobalConfigurationId,
                    ModelId = modelId
                });
            }
        }

        public async Task<string?> GetSelectedModelAsync(CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var query = "SELECT m.name FROM ai_interactions.configuration c " +
                        "JOIN ai_interactions.models m ON m.\"Id\" = c.selected_model_id " +
                        "WHERE c.id = @Id";

            return await connection.QuerySingleOrDefaultAsync<string?>(query, new { Id = GlobalConfigurationId });
        }

        public async Task<(int ChunkTargetSizeChars, int ChunkOverlapChars)> GetChunkingSettingsAsync(CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var query = "SELECT chunk_target_size_chars AS ChunkTargetSizeChars, chunk_overlap_chars AS ChunkOverlapChars " +
                        "FROM ai_interactions.configuration WHERE id = @Id";

            var row = await connection.QuerySingleOrDefaultAsync<ChunkingSettingsRow>(query, new { Id = GlobalConfigurationId });

            return row is null
                ? (DefaultChunkTargetSizeChars, DefaultChunkOverlapChars)
                : (row.ChunkTargetSizeChars, row.ChunkOverlapChars);
        }

        public async Task UpsertChunkingSettingsAsync(int chunkTargetSizeChars, int chunkOverlapChars, CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var updateQuery = "UPDATE ai_interactions.configuration " +
                               "SET chunk_target_size_chars = @ChunkTargetSizeChars, chunk_overlap_chars = @ChunkOverlapChars " +
                               "WHERE id = @Id";
            var rowsAffected = await connection.ExecuteAsync(updateQuery, new
            {
                Id = GlobalConfigurationId,
                ChunkTargetSizeChars = chunkTargetSizeChars,
                ChunkOverlapChars = chunkOverlapChars
            });

            if (rowsAffected == 0)
            {
                var insertQuery = "INSERT INTO ai_interactions.configuration (id, chunk_target_size_chars, chunk_overlap_chars) " +
                                   "VALUES (@Id, @ChunkTargetSizeChars, @ChunkOverlapChars)";
                await connection.ExecuteAsync(insertQuery, new
                {
                    Id = GlobalConfigurationId,
                    ChunkTargetSizeChars = chunkTargetSizeChars,
                    ChunkOverlapChars = chunkOverlapChars
                });
            }
        }

        private sealed class ChunkingSettingsRow
        {
            public int ChunkTargetSizeChars { get; set; }

            public int ChunkOverlapChars { get; set; }
        }
    }
}
