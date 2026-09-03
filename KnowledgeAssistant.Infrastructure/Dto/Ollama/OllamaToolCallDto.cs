using System.Text.Json;
using System.Text.Json.Serialization;

namespace KnowledgeAssistant.Infrastructure.Dto.Ollama;

internal class OllamaToolCallDto
{
    [JsonPropertyName("function")]
    public OllamaFunctionCallDto? Function { get; set; }
}