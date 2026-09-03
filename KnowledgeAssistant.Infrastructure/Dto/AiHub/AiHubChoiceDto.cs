using System.Text.Json.Serialization;

namespace KnowledgeAssistant.Infrastructure.Dto.AiHub;

internal sealed class AiHubChoiceDto
{
    [JsonPropertyName("message")]
    public AiHubResponseMessageDto? Message { get; set; }

    [JsonPropertyName("delta")]
    public AiHubResponseMessageDto? Delta { get; set; }
}