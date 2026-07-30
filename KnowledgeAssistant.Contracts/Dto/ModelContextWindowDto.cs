namespace KnowledgeAssistant.Contracts.Dto
{
    public class ModelContextWindowDto
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public long Size { get; set; }

        public int? ContextLength { get; set; }

        public string? Family { get; set; }

        public string? QuantizationLevel { get; set; }

        public string? ParameterSize { get; set; }
    }
}
