using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents;

public record UpdateChunkingSettingsRequest : MessageBase
{
    public UpdateChunkingSettingsRequest(string embeddingModelName, int chunkTargetSizeChars, int chunkOverlapChars)
    {
        EmbeddingModelName = embeddingModelName;
        ChunkTargetSizeChars = chunkTargetSizeChars;
        ChunkOverlapChars = chunkOverlapChars;
    }

    public string EmbeddingModelName { get; }

    public int ChunkTargetSizeChars { get; }

    public int ChunkOverlapChars { get; }
}