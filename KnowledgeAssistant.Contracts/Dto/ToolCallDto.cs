using System.Text.Json;

namespace KnowledgeAssistant.Contracts.Dto
{
    public class ToolCallDto
    {
        public Guid ToolCallId { get; set; }

        public required string ToolName { get; set; }

        public JsonElement Arguments { get; set; }
    }
}
