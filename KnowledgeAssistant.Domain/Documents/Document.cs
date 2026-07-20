namespace KnowledgeAssistant.Domain.Documents
{
    public class Document
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string OriginalText { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public IEnumerable<string> Topics { get; set; } = Array.Empty<string>();
    }
}