namespace KnowledgeAssistant.Contracts.Dto
{
    public record ErrorEventDto
    {
        public string Message { get; set; } = default!;
    }
}
