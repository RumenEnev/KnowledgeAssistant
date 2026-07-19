using Dapper;
using KnowledgeAssistant.Application.Abstraction;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace KnowledgeAssistant.Application.Services
{
    public class ModelRepository : IModelRepository
    {
        private readonly string _connectionString;

        public ModelRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("KnowledgeAssistant")
                ?? throw new InvalidOperationException("Connection string is missing.");
        }

        public async Task<Guid> GetOrCreateModelIdAsync(string modelName, CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var selectQuery = "SELECT \"Id\" FROM ai_interactions.models WHERE name = @Name LIMIT 1";
            var existingId = await connection.QuerySingleOrDefaultAsync<Guid?>(selectQuery, new { Name = modelName });
            if (existingId.HasValue)
            {
                return existingId.Value;
            }

            var newId = Guid.NewGuid();
            var insertQuery = "INSERT INTO ai_interactions.models (\"Id\", name, provider, is_installed, last_seen) " +
                               "VALUES (@Id, @Name, @Provider, @IsInstalled, @LastSeen)";

            await connection.ExecuteAsync(insertQuery, new
            {
                Id = newId,
                Name = modelName,
                Provider = "ollama",
                IsInstalled = true,
                LastSeen = DateTimeOffset.UtcNow
            });

            return newId;
        }

        public async Task<string?> GetModelNameAsync(Guid modelId, CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var query = "SELECT name FROM ai_interactions.models WHERE \"Id\" = @Id";
            return await connection.QuerySingleOrDefaultAsync<string?>(query, new { Id = modelId });
        }
    }
}
