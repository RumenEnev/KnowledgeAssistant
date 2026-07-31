using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.RepositoriesManagement
{
    public record UpdateRepositoryRequest : MessageBase
    {
        public UpdateRepositoryRequest(Guid id, string name, string rootPath, string? description)
        {
            Id = id;
            Name = name;
            RootPath = rootPath;
            Description = description;
        }

        public Guid Id { get; }

        public string Name { get; }

        public string RootPath { get; }

        public string? Description { get; }
    }
}