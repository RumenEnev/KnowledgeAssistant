namespace KnowledgeAssistant.Contracts.Dto
{
    public class SaveDocumentationRequestDto
    {
        public Guid RepositoryId { get; set; }

        public string RelativeFilePath { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Markdown { get; set; } = string.Empty;
    }
}
