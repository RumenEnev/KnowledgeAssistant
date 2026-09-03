namespace KnowledgeAssistant.Contracts.Dto;

public sealed class UpdateConversationModelSelectionDto
{
    public string SelectedProvider { get; init; } = string.Empty;

    public string SelectedModel { get; init; } = string.Empty;
}
