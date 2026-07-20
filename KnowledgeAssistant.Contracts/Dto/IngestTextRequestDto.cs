namespace KnowledgeAssistant.Contracts.Dto
{
    public class IngestTextRequestDto
    {
        public string Title { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public List<string> Topics { get; set; } = new();
    }
}