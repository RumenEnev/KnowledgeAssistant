using Dapper;
using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Contracts.Enums;
using KnowledgeAssistant.Domain;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace KnowledgeAssistant.Application.Services;

public class ToolRepository : IToolRepository
{
    private readonly string _connectionString;

    public ToolRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("KnowledgeAssistant")
            ?? throw new InvalidOperationException("Connection string is missing.");
    }

    public async Task<Guid> AddToolAsync(string name, string description, string parametersJsonSchema, bool isEnabled, ToolScope scope, string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tool name is required.", nameof(name));
        }

        var existing = await GetToolByNameAsync(name, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"A tool named '{name}' is already registered.");
        }

        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        const string sql = """
                            INSERT INTO ai_interactions.tools (id, name, description, parameters_json_schema, is_enabled, created_at, updated_at, scope, path)
                            VALUES (@Id, @Name, @Description, @ParametersJsonSchema, @IsEnabled, @CreatedAt, @UpdatedAt, @Scope, @Path);
                            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new
        {
            Id = id,
            Name = name,
            Description = description,
            ParametersJsonSchema = parametersJsonSchema,
            IsEnabled = isEnabled,
            CreatedAt = now,
            UpdatedAt = now,
            Scope = scope.ToString(),
            Path = path
        }, cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
        return id;
    }

    public async Task<ToolDefinitionEntity?> GetToolByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
                            SELECT id AS "Id", name AS "Name", description AS "Description",
                                   parameters_json_schema AS "ParametersJsonSchema", is_enabled AS "IsEnabled",
                                   created_at AS "CreatedAt", updated_at AS "UpdatedAt", scope AS "Scope", path AS "Path"
                            FROM ai_interactions.tools WHERE id = @Id
                            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<ToolDefinitionEntity>(command);
    }

    public async Task<ToolDefinitionEntity?> GetToolByNameAsync(string name, CancellationToken cancellationToken)
    {
        const string sql = """
                            SELECT id AS "Id", name AS "Name", description AS "Description",
                                   parameters_json_schema AS "ParametersJsonSchema", is_enabled AS "IsEnabled",
                                   created_at AS "CreatedAt", updated_at AS "UpdatedAt"
                            FROM ai_interactions.tools WHERE name = @Name
                            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Name = name }, cancellationToken: cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<ToolDefinitionEntity>(command);
    }

    public async Task<IReadOnlyList<ToolDefinitionEntity>> GetToolsAsync(MessageSource? source, CancellationToken cancellationToken)
    {
        var sql = """
                  SELECT id AS "Id", name AS "Name", description AS "Description",
                         parameters_json_schema AS "ParametersJsonSchema", is_enabled AS "IsEnabled",
                         created_at AS "CreatedAt", updated_at AS "UpdatedAt", scope AS "Scope", path AS "Path"
                  FROM ai_interactions.tools WHERE 1 = 1
                  """;

        sql = AppendScopeFilter(sql, source);
        sql += " ORDER BY name";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, BuildScopeParameters(source), cancellationToken: cancellationToken);
        var results = await connection.QueryAsync<ToolDefinitionEntity>(command);
        return results.ToList();
    }

    public async Task<IReadOnlyList<ToolDefinitionEntity>> GetEnabledToolsAsync(MessageSource? source, CancellationToken cancellationToken)
    {
        var sql = """
                  SELECT id AS "Id", name AS "Name", description AS "Description",
                         parameters_json_schema AS "ParametersJsonSchema", is_enabled AS "IsEnabled",
                         created_at AS "CreatedAt", updated_at AS "UpdatedAt", scope AS "Scope", path AS "Path"
                  FROM ai_interactions.tools WHERE is_enabled = TRUE
                  """;

        sql = AppendScopeFilter(sql, source);
        sql += " ORDER BY name";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, BuildScopeParameters(source), cancellationToken: cancellationToken);
        var results = await connection.QueryAsync<ToolDefinitionEntity>(command);
        return results.ToList();
    }

    private static string AppendScopeFilter(string sql, MessageSource? source) =>
        source.HasValue ? sql + " AND (scope ILIKE @Scope OR scope ILIKE @AllScope)" : sql;

    private static object BuildScopeParameters(MessageSource? source) => new
    {
        Scope = source.HasValue ? MapToToolScope(source.Value).ToString() : null,
        AllScope = ToolScope.All.ToString()
    };

    private static ToolScope MapToToolScope(MessageSource source) => source switch
    {
        MessageSource.Desktop => ToolScope.Desktop,
        MessageSource.Web => ToolScope.Web,
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unsupported message source.")
    };

    public async Task<IReadOnlyList<ToolDefinition>> GetEnabledToolDefinitionsAsync(MessageSource? source, CancellationToken cancellationToken)
    {
        var enabledTools = await GetEnabledToolsAsync(source, cancellationToken);
        return enabledTools.Select(t => new ToolDefinition
        {
            Name = t.Name,
            Description = t.Description,
            ParametersJsonSchema = t.ParametersJsonSchema
        }).ToList();
    }

    public async Task<bool> UpdateToolAsync(Guid id, string? name, string? description, string? parametersJsonSchema, bool? isEnabled, ToolScope? scope, string? path, CancellationToken cancellationToken)
    {
        var existing = await GetToolByIdAsync(id, cancellationToken);
        if (existing == null) return false;

        const string sql = """
                            UPDATE ai_interactions.tools
                            SET name = @NewName, description = @NewDescription, parameters_json_schema = @NewParametersJsonSchema,
                                is_enabled = @NewIsEnabled, scope = @NewScope, path = @NewPath, updated_at = @UpdatedAt
                            WHERE id = @Id;
                            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new
        {
            NewName = name ?? existing.Name,
            NewDescription = description ?? existing.Description,
            NewParametersJsonSchema = parametersJsonSchema ?? existing.ParametersJsonSchema,
            NewIsEnabled = isEnabled ?? existing.IsEnabled,
            NewScope = (scope ?? existing.Scope).ToString(),
            NewPath = path ?? existing.Path,
            UpdatedAt = DateTime.UtcNow,
            Id = id
        }, cancellationToken: cancellationToken);

        var rows = await connection.ExecuteAsync(command);
        return rows > 0;
    }

    public async Task<bool> DeleteToolAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM ai_interactions.tools WHERE id = @Id";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        var rows = await connection.ExecuteAsync(command);
        return rows > 0;
    }
}