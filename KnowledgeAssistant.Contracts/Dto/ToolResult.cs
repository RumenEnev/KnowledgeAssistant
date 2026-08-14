using System.Text.Json.Serialization;

namespace KnowledgeAssistant.Contracts.Dto
{
    public sealed class ToolResult
    {
        [JsonPropertyName("status")]
        public string Status { get; init; } = "error";

        [JsonPropertyName("outputPath")]
        public string? OutputPath { get; init; }

        [JsonPropertyName("reason")]
        public string? Reason { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }

        public static ToolResult Success(string outputPath, string message) => new()
        {
            Status = "success",
            OutputPath = outputPath,
            Message = message
        };

        public static ToolResult Error(string reason, string message) => new()
        {
            Status = "error",
            Reason = reason,
            Message = message
        };
    }
}