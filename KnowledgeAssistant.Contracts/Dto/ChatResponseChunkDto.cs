namespace KnowledgeAssistant.Contracts.Dto
{
    public class ChatResponseChunkDto
    {
        public string Type { get; set; } = default!; // token | done

        public string? Content { get; set; }

        public Guid? ConversationId { get; set; }

        public Guid? MessageId { get; set; }
    }
}
