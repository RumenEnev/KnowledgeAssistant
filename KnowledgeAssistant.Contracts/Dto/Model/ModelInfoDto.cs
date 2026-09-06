namespace KnowledgeAssistant.Contracts.Dto.Model;

public class ModelInfoDto
{
    public required string Name { get; set; }

    public bool CanCallTools { get; set; }
}