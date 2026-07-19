namespace KnowledgeAssistant.Contracts.Dto
{
    public class ConversationDto
    {
        public Guid Id { get; set; }

        public string? Title { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? SelectedModel { get; set; }

        public IEnumerable<MessageDto>? Messages { get; set; }
    }
}
