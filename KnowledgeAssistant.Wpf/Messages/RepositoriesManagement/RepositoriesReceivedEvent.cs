using KnowledgeAssistant.Contracts.Repositories;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.RepositoriesManagement
{
    public record RepositoriesReceivedEvent : MessageBase
    {
        public RepositoriesReceivedEvent(IEnumerable<RepositoryDto> repositories)
        {
            Repositories = repositories;
        }

        public IEnumerable<RepositoryDto> Repositories { get; } 
    }
}