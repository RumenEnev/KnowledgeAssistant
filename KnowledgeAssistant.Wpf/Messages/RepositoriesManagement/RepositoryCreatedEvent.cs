using KnowledgeAssistant.Contracts.Repositories;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.RepositoriesManagement
{
    public record RepositoryCreatedEvent : MessageBase
    {
        public RepositoryCreatedEvent(RepositoryDto repository)
        {
            Repository = repository;
        }

        public RepositoryDto Repository { get; }
    }
}