namespace KnowledgeAssistant.Contracts.Dto
{
    public record MessageDoneDto
    {
        public int PromptTokens { get; init; }

        public int ResponseTokens { get; init; }
    }
}