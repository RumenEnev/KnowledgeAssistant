using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents;

public record EmbeddingsLoadedEvent : MessageBase
{
    public EmbeddingsLoadedEvent(string[] models, int chunkSize, int overlap)
    {
        Models = models;
        ChunkSize = chunkSize;
        Overlap = overlap;
    }

    public string[] Models { get; }

    public int ChunkSize { get; }

    public int Overlap { get; }
}