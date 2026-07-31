using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.RepositoriesManagement
{
    public record RepositoryDeletedEvent : MessageBase
    {
        public RepositoryDeletedEvent(Guid id)
        {
            Id = id;
        }

        public Guid Id { get; }
    }
}