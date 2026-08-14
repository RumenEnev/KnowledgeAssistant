namespace DocumentCreator.Configuration;

public sealed class OllamaConfig
{
    public string BaseUrl { get; init; } = "http://localhost:11434";

    public string GenerationModel { get; init; } = "codestral";

    public int RequestTimeoutSeconds { get; init; } = 120;
}