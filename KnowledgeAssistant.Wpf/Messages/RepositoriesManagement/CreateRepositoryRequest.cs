using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.RepositoriesManagement
{
    public record CreateRepositoryRequest : MessageBase
    {
        public CreateRepositoryRequest(string name, string rootPath, string? description)
        {
            Name = name;
            RootPath = rootPath;
            Description = description;
        }

        public string Name { get; }

        public string RootPath { get; }

        public string? Description { get; }
    }
}