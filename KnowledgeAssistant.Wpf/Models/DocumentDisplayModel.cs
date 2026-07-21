namespace KnowledgeAssistant.Wpf.Models
{
    public class DocumentDisplayModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public IEnumerable<string> Topics { get; set; } = Array.Empty<string>();

        public string TopicsDisplay => string.Join(", ", Topics);
    }
}
