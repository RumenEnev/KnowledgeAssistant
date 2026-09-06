namespace KnowledgeAssistant.Contracts.Dto.Model;

public record UpdateSelectedModelDto
{
    public required string SelectedModel { get; set; }
}