using KnowledgeAssistant.Domain.Documents;
using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents;

public record SaveRetrievalConfigRequest : MessageBase
{
    public SaveRetrievalConfigRequest(string modelName, int chunkSize, int chunkOverlap)
    {
        ModelName = modelName;
        ChunkSize = chunkSize;
        ChunkOverlap = chunkOverlap;
    }

    public string ModelName { get; }

    public int ChunkSize { get; }

    public int ChunkOverlap { get; }
}