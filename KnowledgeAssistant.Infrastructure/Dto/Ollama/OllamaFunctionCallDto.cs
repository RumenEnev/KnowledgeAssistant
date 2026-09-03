using System.Text.Json;
using System.Text.Json.Serialization;

namespace KnowledgeAssistant.Infrastructure.Dto.Ollama;

internal class OllamaFunctionCallDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arguments")]
    public JsonElement Arguments { get; set; }
}