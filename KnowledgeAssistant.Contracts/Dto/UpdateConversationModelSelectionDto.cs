namespace KnowledgeAssistant.Contracts.Dto;

public sealed class UpdateConversationModelSelectionDto
{
    public required string SelectedProvider { get; init; }

    public required string SelectedModel { get; init; }
}