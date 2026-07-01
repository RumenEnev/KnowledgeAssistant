namespace KnowledgeAssistant.Contracts.Dto
{
    public class MessageDto
    {
        public Guid Id { get; set; }

        public Guid ConversationId { get; set; }

        public string Role { get; set; } = default!;

        public string Content { get; set; } = default!;

        public DateTime CreatedAt { get; set; }
    }
}