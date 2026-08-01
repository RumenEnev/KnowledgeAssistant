using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documentation
{
    /// <summary>
    /// Broadcast when the assistant has generated markdown documentation for a source file and
    /// is waiting for the user to confirm whether it should be saved to disk and ingested into the RAG index.
    /// </summary>
    public record DocumentationReadyEvent : MessageBase
    {
        public DocumentationReadyEvent(
            Guid correlationId,
            Guid repositoryId,
            string repositoryName,
            string fileName,
            string relativeFilePath,
            string title,
            string markdown)
        {
            CorrelationId = correlationId;
            RepositoryId = repositoryId;
            RepositoryName = repositoryName;
            FileName = fileName;
            RelativeFilePath = relativeFilePath;
            Title = title;
            Markdown = markdown;
        }

        public Guid CorrelationId { get; }

        public Guid RepositoryId { get; }

        public string RepositoryName { get; }

        public string FileName { get; }

        public string RelativeFilePath { get; }

        public string Title { get; }

        public string Markdown { get; }
    }
}
