using System.Text.Json.Serialization;

namespace KnowledgeAssistant.Infrastructure.Dto
{
    internal class OllamaChatResponseDto
    {
        [JsonPropertyName("message")]
        public OllamaMessageDto? Message { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }

        [JsonPropertyName("prompt_eval_count")]
        public int PromptEvalCount { get; set; }

        [JsonPropertyName("eval_count")]
        public int EvalCount { get; set; }
    }
}
