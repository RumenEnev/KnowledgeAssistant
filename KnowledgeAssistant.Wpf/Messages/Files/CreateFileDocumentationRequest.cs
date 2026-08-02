using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Files
{
    public record CreateFileDocumentationRequest : MessageBase
    {
        public CreateFileDocumentationRequest(string fileName, List<string> repositories)
        {
            FileName = fileName;
            Repositories = repositories;
        }

        public string FileName { get; }

        public List<string> Repositories { get; }
    }
}