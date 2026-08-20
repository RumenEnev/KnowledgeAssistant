namespace OllamaClients.Configuration;

public sealed class OllamaCallException : Exception
{
    public OllamaCallException(string message) : base(message) { }
}