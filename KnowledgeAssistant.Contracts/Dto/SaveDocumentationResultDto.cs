namespace KnowledgeAssistant.Contracts.Dto
{
    public class SaveDocumentationResultDto
    {
        public int DocumentId { get; set; }

        public string SavedFilePath { get; set; } = string.Empty;
    }
}
