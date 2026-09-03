using System.Text.Json.Serialization;

namespace KnowledgeAssistant.Infrastructure.Dto.AiHub;

internal sealed class AiHubErrorEnvelopeDto
{
    [JsonPropertyName("error")]
    public AiHubErrorDto? Error { get; set; }
}
