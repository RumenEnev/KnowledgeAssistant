namespace KnowledgeAssistant.Domain
{
    public class SourceRepository
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string RootPath { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}