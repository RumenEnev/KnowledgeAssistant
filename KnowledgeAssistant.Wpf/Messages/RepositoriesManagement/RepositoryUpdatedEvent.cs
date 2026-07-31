using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.RepositoriesManagement
{
    public record RepositoryUpdatedEvent : MessageBase
    {
        public RepositoryUpdatedEvent(Guid id)
        {
            Id = id;
        }

        public Guid Id { get; }
    }
}