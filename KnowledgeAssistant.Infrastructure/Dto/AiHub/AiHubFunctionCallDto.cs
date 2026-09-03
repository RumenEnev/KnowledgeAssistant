using System.Text.Json.Serialization;

namespace KnowledgeAssistant.Infrastructure.Dto.AiHub;

internal sealed class AiHubFunctionCallDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    // OpenAI-compatible APIs return function arguments as a JSON-encoded string.
    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
}