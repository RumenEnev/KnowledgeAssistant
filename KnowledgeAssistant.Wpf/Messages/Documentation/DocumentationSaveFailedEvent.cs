using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documentation
{
    public record DocumentationSaveFailedEvent : MessageBase
    {
        public DocumentationSaveFailedEvent(Guid correlationId, string errorMessage)
        {
            CorrelationId = correlationId;
            ErrorMessage = errorMessage;
        }

        public Guid CorrelationId { get; }

        public string ErrorMessage { get; }
    }
}
