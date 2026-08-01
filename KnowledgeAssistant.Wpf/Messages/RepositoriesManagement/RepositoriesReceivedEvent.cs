using KnowledgeAssistant.Contracts.Repositories;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.RepositoriesManagement
{
    public record RepositoriesReceivedEvent : MessageBase
    {
        public RepositoriesReceivedEvent(IEnumerable<RepositoryDto> repositories, string? Error = null)
        {
            Repositories = repositories;
            ErrorMessage = Error;
        }

        public IEnumerable<RepositoryDto> Repositories { get; } 

        public string? ErrorMessage { get; }
    }
}