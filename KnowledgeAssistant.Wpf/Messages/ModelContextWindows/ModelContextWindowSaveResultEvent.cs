using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.ModelContextWindows
{
    public record ModelContextWindowSaveResultEvent : MessageBase
    {
        public ModelContextWindowSaveResultEvent(Guid id, bool success, string? errorMessage)
        {
            Id = id;
            Success = success;
            ErrorMessage = errorMessage;
        }

        public Guid Id { get; }

        public bool Success { get; }

        public string? ErrorMessage { get; }
    }
}
