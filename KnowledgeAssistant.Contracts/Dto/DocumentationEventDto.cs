namespace KnowledgeAssistant.Contracts.Dto
{
    /// <summary>
    /// Payload sent over SSE (event: "documentation") when the assistant has generated
    /// markdown documentation for a source file and is waiting for the user to confirm
    /// whether it should be saved to disk and ingested into the RAG index.
    /// </summary>
    public class DocumentationEventDto
    {
        public Guid RepositoryId { get; set; }

        public string RepositoryName { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public string RelativeFilePath { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Markdown { get; set; } = string.Empty;
    }
}
