namespace KnowledgeAssistant.Infrastructure.Dto.Ollama;

public class OllamaModelDto
{
    public required string Name { get; set; }

    public long Size { get; set; }
}