using System.Text.Json.Serialization;

namespace KnowledgeAssistant.Infrastructure.Dto
{
    internal class OllamaMessageDto
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
