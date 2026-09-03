using System.Text.Json.Serialization;

namespace KnowledgeAssistant.Infrastructure.Dto.AiHub;

internal sealed class AiHubChatCompletionResponseDto
{
    [JsonPropertyName("choices")]
    public List<AiHubChoiceDto> Choices { get; set; } = [];

    [JsonPropertyName("usage")]
    public AiHubUsageDto? Usage { get; set; }
}