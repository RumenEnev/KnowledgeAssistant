namespace KnowledgeAssistant.Domain.Conversation
{
    public class Conversation
    {
        public Guid Id { get; set; }

        public string? Title { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public IEnumerable<ChatMessage>? Messages { get; set; }
    }
}