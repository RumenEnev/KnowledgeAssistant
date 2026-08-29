namespace KnowledgeAssistant.Domain.Documents
{
    public class ChunkListItem
    {
        public int Id { get; set; }

        public int DocumentId { get; set; }

        public string DocumentTitle { get; set; } = string.Empty;

        public int ChunkIndex { get; set; }

        public string ChunkText { get; set; } = string.Empty;
    }
}
