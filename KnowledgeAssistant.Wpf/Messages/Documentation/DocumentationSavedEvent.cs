using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documentation
{
    public record DocumentationSavedEvent : MessageBase
    {
        public DocumentationSavedEvent(Guid correlationId, int documentId, string savedFilePath)
        {
            CorrelationId = correlationId;
            DocumentId = documentId;
            SavedFilePath = savedFilePath;
        }

        public Guid CorrelationId { get; }

        public int DocumentId { get; }

        public string SavedFilePath { get; }
    }
}
