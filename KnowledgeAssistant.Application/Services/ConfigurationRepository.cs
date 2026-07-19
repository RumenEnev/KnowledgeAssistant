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

        private readonly string _connectionString;

        public ConfigurationRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("KnowledgeAssistant")
                ?? throw new InvalidOperationException("Connection string is missing.");
        }

        public async Task UpsertSelectedModelAsync(string selectedModel, CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var updateQuery = "UPDATE ai_interactions.configuration SET selected_model = @SelectedModel WHERE id = @Id";
            var rowsAffected = await connection.ExecuteAsync(updateQuery, new
            {
                Id = GlobalConfigurationId,
                SelectedModel = selectedModel
            });

            if (rowsAffected == 0)
            {
                var insertQuery = "INSERT INTO ai_interactions.configuration (id, selected_model) VALUES (@Id, @SelectedModel)";
                await connection.ExecuteAsync(insertQuery, new
                {
                    Id = GlobalConfigurationId,
                    SelectedModel = selectedModel
                });
            }
        }

        public async Task<string?> GetSelectedModelAsync(CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var query = "SELECT selected_model FROM ai_interactions.configuration WHERE id = @Id";
            return await connection.QuerySingleOrDefaultAsync<string?>(query, new { Id = GlobalConfigurationId });
        }
    }
}
