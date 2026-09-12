using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents;

public record EmbeddingsLoadedEvent : MessageBase
{
    public EmbeddingsLoadedEvent(string[] models, string selectedModel, int chunkSize, int overlap)
    {
        Models = models;
        SelectedModel = selectedModel;
        ChunkSize = chunkSize;
        Overlap = overlap;
    }

    public string[] Models { get; }

    public string SelectedModel { get; }

    public int ChunkSize { get; }

    public int Overlap { get; }
} 