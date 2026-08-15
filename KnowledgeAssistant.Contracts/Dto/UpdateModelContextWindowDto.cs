namespace KnowledgeAssistant.Contracts.Dto
{
    public class UpdateModelContextWindowDto
    {
        public bool InternalUseOnly { get; set; }

        public bool CanCallTools { get; set; }
    }
}
