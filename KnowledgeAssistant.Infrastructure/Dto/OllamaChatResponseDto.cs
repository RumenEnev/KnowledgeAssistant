using System.Text.Json.Serialization;

namespace KnowledgeAssistant.Infrastructure.Dto
{
    internal class OllamaChatResponseDto
    {
        [JsonPropertyName("message")]
        public OllamaMessageDto? Message { get; set; }

        public bool Done { get; set; }
    }
}
