using KnowledgeAssistant.Contracts.Enums;

namespace KnowledgeAssistant.Contracts.Dto
{
    public class IngestTextRequestDto
    {
        public string Title { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public DocumentType DocumentType { get; set; } = DocumentType.PlainText;

        public List<string> Topics { get; set; } = new();
    }
}