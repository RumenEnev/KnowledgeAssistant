using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Domain;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace KnowledgeAssistant.Application.Services;

public class ToolExecutionService : IToolExecutionService
{
    public const string GenerateUuidToolName = "generate_uuid";
    public const string SearchWebToolName = "search_web";

    private readonly HttpClient _httpClient;
    private readonly ILogger<ToolExecutionService> _logger;

    public ToolExecutionService(HttpClient httpClient, ILogger<ToolExecutionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<string> ExecuteAsync(ToolDefinitionEntity tool, string argumentsJson, CancellationToken cancellationToken)
    {
        switch (tool.Name)
        {
            case GenerateUuidToolName: return Task.FromResult(ExecuteGenerateUuid());
            case SearchWebToolName: return ExecuteSearchWebAsync(tool, argumentsJson, cancellationToken);
            default:
                throw new NotSupportedException($"Tool scope '{tool.Scope}' is not supported.");
        }
    }

    private static string ExecuteGenerateUuid()
    {
        return JsonSerializer.Serialize(new
        {
            status = "success",
            uuid = Guid.NewGuid().ToString()
        });
    }

    private async Task<string> ExecuteSearchWebAsync(ToolDefinitionEntity tool, string argumentsJson, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tool.Path))
        {
            return SerializeError("missing_configuration", $"Tool '{tool.Name}' has no search engine URI configured (Path is empty).");
        }

        string? query = null;
        try
        {
            using var arguments = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            if (arguments.RootElement.TryGetProperty("query", out var queryElement) && queryElement.ValueKind == JsonValueKind.String)
            {
                query = queryElement.GetString();
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse arguments for tool '{ToolName}'.", tool.Name);
            return SerializeError("invalid_arguments", "The search query arguments could not be parsed.");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return SerializeError("invalid_arguments", "A non-empty 'query' argument is required to search.");
        }

        Uri searchUri;
        try
        {
            var builder = new UriBuilder(tool.Path);
            builder.Query = MergeQuery(builder.Query, query);
            searchUri = builder.Uri;
        }
        catch (UriFormatException ex)
        {
            _logger.LogWarning(ex, "Invalid search engine URI '{Path}' for tool '{ToolName}'.", tool.Path, tool.Name);
            return SerializeError("invalid_configuration", $"Tool '{tool.Name}' has an invalid search engine URI configured.");
        }

        try
        {
            using var response = await _httpClient.GetAsync(searchUri, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Search engine call to {Uri} returned {StatusCode}.", searchUri, response.StatusCode);
                return SerializeError("http_error", $"Search engine responded with status code {(int)response.StatusCode}.");
            }

            return content;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Search engine call to {Uri} failed.", searchUri);
            return SerializeError("request_failed", $"Could not reach the search engine: {ex.Message}");
        }
    }

    private static string MergeQuery(string existingQuery, string query)
    {
        var parameters = new List<KeyValuePair<string, string>>();
        var trimmed = existingQuery.TrimStart('?');
        if (!string.IsNullOrEmpty(trimmed))
        {
            foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                var key = Uri.UnescapeDataString(parts[0]);
                if (string.Equals(key, "q", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "format", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                parameters.Add(new KeyValuePair<string, string>(key, parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty));
            }
        }

        parameters.Add(new KeyValuePair<string, string>("q", query));
        parameters.Add(new KeyValuePair<string, string>("format", "json"));

        return string.Join("&", parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
    }

    private static string SerializeError(string reason, string message) => JsonSerializer.Serialize(new
    {
        status = "error",
        reason,
        message
    });
}