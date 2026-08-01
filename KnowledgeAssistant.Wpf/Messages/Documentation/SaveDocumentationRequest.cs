using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documentation
{
    /// <summary>
    /// Requests that previously generated documentation markdown be saved to disk and ingested into the RAG index.
    /// </summary>
    public record SaveDocumentationRequest : MessageBase
    {
        public SaveDocumentationRequest(
            Guid correlationId,
            Guid repositoryId,
            string relativeFilePath,
            string title,
            string markdown)
        {
            CorrelationId = correlationId;
            RepositoryId = repositoryId;
            RelativeFilePath = relativeFilePath;
            Title = title;
            Markdown = markdown;
        }

        public Guid CorrelationId { get; }

        public Guid RepositoryId { get; }

        public string RelativeFilePath { get; }

        public string Title { get; }

        public string Markdown { get; }
    }
}
