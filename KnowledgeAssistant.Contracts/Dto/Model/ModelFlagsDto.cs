namespace KnowledgeAssistant.Contracts.Dto.Model;

public record ModelFlagsDto
{
    public bool InternalUseOnly { get; init; }

    public bool CanCallTools { get; init; }

    public bool IsFavorite { get; init; }
}