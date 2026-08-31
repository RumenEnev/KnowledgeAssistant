namespace KnowledgeAssistant.Domain.Documents
{
    public class DocumentChunk
    {
        public int Id { get; set; }

        public int DocumentId { get; set; }

        public int ChunkIndex { get; set; }

        public string ChunkText { get; set; } = string.Empty;

        public double Distance { get; set; }

        public double? FusedScore { get; set; }
    }
}