using System.Text.Json.Serialization;

namespace KnowledgeAssistant.Infrastructure.Dto.AiHub;

internal sealed class AiHubModelsResponseDto
{
    [JsonPropertyName("data")]
    public List<AiHubModelDto> Data { get; set; } = [];
}
