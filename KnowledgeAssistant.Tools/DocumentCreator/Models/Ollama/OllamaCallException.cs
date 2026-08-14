namespace DocumentCreator.Models.Ollama;

public sealed class OllamaCallException : Exception
{
    public OllamaCallException(string message) : base(message) { }
}