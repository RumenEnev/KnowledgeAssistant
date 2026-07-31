using Dapper;
using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Domain;
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

        public async Task<Guid> AddRepositoryAsync(string name, string rootPath, string? description, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Repository name is required.", nameof(name));

            if (!Directory.Exists(rootPath))
                throw new DirectoryNotFoundException($"Root path does not exist: {rootPath}");

            var existing = await GetRepositoryByNameAsync(name, cancellationToken);
            if (existing != null)
                throw new InvalidOperationException($"A repository named '{name}' is already registered.");

            var id = Guid.NewGuid();
            var now = DateTime.UtcNow;

            const string sql = """
                                INSERT INTO repositories (id, name, root_path, description, created_at, updated_at)
                                VALUES (@Id, @Name, @RootPath, @Description, @CreatedAt, @UpdatedAt);
                                """;

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var command = new CommandDefinition(sql, new
            {
                Id = id,
                Name = name,
                RootPath = rootPath,
                Description = description,
                CreatedAt = now,
                UpdatedAt = now
            }, cancellationToken: cancellationToken);

            await connection.ExecuteAsync(command);
            return id;
        }

        public async Task<SourceRepository?> GetRepositoryByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            const string sql = """
                                SELECT id AS "Id", name AS "Name", root_path AS "RootPath", description AS "Description",
                                       created_at AS "CreatedAt", updated_at AS "UpdatedAt"
                                FROM repositories WHERE id = @Id
                                """;

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
            return await connection.QueryFirstOrDefaultAsync<SourceRepository>(command);
        }

        public async Task<SourceRepository?> GetRepositoryByNameAsync(string name, CancellationToken cancellationToken)
        {
            const string sql = """
                                SELECT id AS "Id", name AS "Name", root_path AS "RootPath", description AS "Description",
                                       created_at AS "CreatedAt", updated_at AS "UpdatedAt"
                                FROM repositories WHERE name = @Name
                                """;

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var command = new CommandDefinition(sql, new { Name = name }, cancellationToken: cancellationToken);
            return await connection.QueryFirstOrDefaultAsync<SourceRepository>(command);
        }

        public async Task<IReadOnlyList<SourceRepository>> GetRepositoriesAsync(CancellationToken cancellationToken)
        {
            const string sql = """
                                SELECT id AS "Id", name AS "Name", root_path AS "RootPath", description AS "Description",
                                       created_at AS "CreatedAt", updated_at AS "UpdatedAt"
                                FROM repositories ORDER BY name
                                """;

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
            var results = await connection.QueryAsync<SourceRepository>(command);
            return results.ToList();
        }

        public async Task<bool> UpdateRepositoryAsync(Guid id, string? name, string? rootPath, string? description, CancellationToken cancellationToken)
        {
            var existing = await GetRepositoryByIdAsync(id, cancellationToken);
            if (existing == null) return false;

            if (rootPath != null && !Directory.Exists(rootPath))
                throw new DirectoryNotFoundException($"Root path does not exist: {rootPath}");

            const string sql = """
                                UPDATE repositories
                                SET name = @NewName, root_path = @NewRootPath, description = @NewDescription, updated_at = @UpdatedAt
                                WHERE id = @Id;
                                """;

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var command = new CommandDefinition(sql, new
            {
                NewName = name ?? existing.Name,
                NewRootPath = rootPath ?? existing.RootPath,
                NewDescription = description ?? existing.Description,
                UpdatedAt = DateTime.UtcNow,
                Id = id
            }, cancellationToken: cancellationToken);

            var rows = await connection.ExecuteAsync(command);
            return rows > 0;
        }

        public async Task<bool> DeleteRepositoryAsync(Guid id, CancellationToken cancellationToken)
        {
            const string sql = "DELETE FROM repositories WHERE id = @Id";

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
            var rows = await connection.ExecuteAsync(command);
            return rows > 0;
        }

        private sealed class ChunkingSettingsRow
        {
            public int ChunkTargetSizeChars { get; set; }

            public int ChunkOverlapChars { get; set; }
        }
    }
}