using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.RepositoriesManagement
{
    public record RepositoryOperationFailedEvent : MessageBase
    {
        public RepositoryOperationFailedEvent(string operation, string errorMessage)
        {
            Operation = operation;
            ErrorMessage = errorMessage;
        }
        public string Operation { get; }

        public string ErrorMessage { get; }
    }
}