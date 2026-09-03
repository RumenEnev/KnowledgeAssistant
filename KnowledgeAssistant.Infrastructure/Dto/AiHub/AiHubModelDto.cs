using System.Text.Json.Serialization;

namespace KnowledgeAssistant.Infrastructure.Dto.AiHub;

internal sealed class AiHubModelDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("owned_by")]
    public string? OwnedBy { get; set; }
}