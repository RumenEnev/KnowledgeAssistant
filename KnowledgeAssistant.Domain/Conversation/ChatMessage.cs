namespace KnowledgeAssistant.Domain.Conversation
{
    public record ChatMessage
    {
        public Guid Id { get; init; }

        public Guid ConversationId { get; init; }

        public required string Role { get; init; }

        public required string Content { get; init; }

        public DateTime CreatedAt { get; init; }

        public int TokensCount { get; set; }
    }
}