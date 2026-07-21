using System.Text.Json.Serialization;

namespace KnowledgeAssistant.Infrastructure.Dto
{
    public class OllamaErrorResponseDto
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
