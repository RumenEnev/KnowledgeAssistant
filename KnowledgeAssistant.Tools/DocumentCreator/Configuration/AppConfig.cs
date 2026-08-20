using OllamaClients.Configuration;

namespace DocumentCreator.Configuration;

public sealed class AppConfig
{
    public DatabaseConfig Database { get; init; } = new();

    public OllamaConfig Ollama { get; init; } = new();

    public OutputConfig Output { get; init; } = new();

    public string PromptKey { get; init; } = "document_generation";

    public int MaxFileContentCharacters { get; init; } = 60000;
}
