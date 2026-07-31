using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.RepositoriesManagement
{
    public record DeleteRepositoryRequest : MessageBase
    {
        public DeleteRepositoryRequest(Guid id)
        {
            Id = id;
        } 

        public Guid Id { get; set; }
    }
}