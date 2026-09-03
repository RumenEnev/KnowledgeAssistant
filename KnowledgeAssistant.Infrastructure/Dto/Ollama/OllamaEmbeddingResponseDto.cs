using System.Text.Json.Serialization;

namespace KnowledgeAssistant.Infrastructure.Dto.Ollama;

public class OllamaEmbeddingResponseDto
{
    [JsonPropertyName("embedding")]
    public float[] Embedding { get; set; } = Array.Empty<float>();
}