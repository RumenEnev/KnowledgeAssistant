namespace KnowledgeAssistant.Contracts.Dto
{
    public class ModelInfoDto
    {
        public required string Name { get; set; }

        public bool CanCallTools { get; set; }
    }
}
