using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents;

public record EmbeddingsLoadedEvent : MessageBase
{
    public EmbeddingsLoadedEvent(string[] models)
    {
        Models = models;
    }

    public string[] Models { get; }
}