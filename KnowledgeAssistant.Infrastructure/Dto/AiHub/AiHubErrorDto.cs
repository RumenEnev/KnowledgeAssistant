using System.Text.Json;
using System.Text.Json.Serialization;

namespace KnowledgeAssistant.Infrastructure.Dto.AiHub;

internal sealed class AiHubErrorDto
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("code")]
    public JsonElement Code { get; set; }
}