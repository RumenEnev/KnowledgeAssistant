namespace KnowledgeAssistant.Contracts.Dto
{
    public class ChatRequestDto
    {
        public Guid? ConversationId { get; set; }

        public string Message { get; set; } = default!;

        public string? Model { get; set; }

        public double? Temperature { get; set; }
    }
}
