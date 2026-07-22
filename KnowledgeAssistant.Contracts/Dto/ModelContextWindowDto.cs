namespace KnowledgeAssistant.Contracts.Dto
{
    public class ModelContextWindowDto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public int? ContextWindowTokens { get; set; }
    }
}
