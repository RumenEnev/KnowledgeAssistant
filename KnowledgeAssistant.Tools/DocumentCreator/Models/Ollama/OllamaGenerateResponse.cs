using System.Text.Json.Serialization;

namespace DocumentCreator.Models.Ollama;

public sealed class OllamaGenerateResponse
{
    [JsonPropertyName("response")]
    public string? Response { get; init; }

    [JsonPropertyName("done")]
    public bool Done { get; init; }
}