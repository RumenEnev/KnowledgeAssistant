using System.Text.Json.Serialization;

namespace KnowledgeAssistant.Infrastructure.Dto.AiHub;

internal sealed class AiHubEmbeddingResponseDto
{
    [JsonPropertyName("data")]
    public List<AiHubEmbeddingDataDto> Data { get; set; } = [];

    [JsonPropertyName("usage")]
    public AiHubUsageDto? Usage { get; set; }
}