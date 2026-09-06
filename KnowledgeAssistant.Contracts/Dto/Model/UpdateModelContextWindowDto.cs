namespace KnowledgeAssistant.Contracts.Dto.Model;

public class UpdateModelContextWindowDto
{
    public bool InternalUseOnly { get; set; }

    public bool CanCallTools { get; set; }

    public bool IsFavorite { get; set; }
}