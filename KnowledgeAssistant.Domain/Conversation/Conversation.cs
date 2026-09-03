namespace KnowledgeAssistant.Domain.Conversation
{
    public class Conversation
    {
        public Guid Id { get; set; }

        public string? Title { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public required string Provider { get; set; }

        public Guid? SelectedModelId { get; set; }

        public int? TopicId { get; set; }

        public string? Topic { get; set; }

        public IEnumerable<ChatMessage>? Messages { get; set; }
    }
}