namespace KnowledgeAssistant.Infrastructure.Dto;

public record AvailableModelInfo
{
    public required string Name { get; init; }

    public bool CanCallTools { get; init; }
}