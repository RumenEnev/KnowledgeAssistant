using System.Text.Json;

namespace KnowledgeAssistant.Contracts.Dto
{
    public class ToolCallDto
    {
        public Guid ToolCallId { get; set; }

        public required Guid ToolId { get; set; }

        public required string ToolName { get; set; }

        public string? ToolPath { get; set; }

        public JsonElement Arguments { get; set; }
    }
}