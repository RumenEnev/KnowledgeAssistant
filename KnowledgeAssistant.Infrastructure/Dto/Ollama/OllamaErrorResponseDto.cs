using System.Text.Json.Serialization;

namespace KnowledgeAssistant.Infrastructure.Dto.Ollama;

public class OllamaErrorResponseDto
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
