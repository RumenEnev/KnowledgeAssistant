namespace KnowledgeAssistant.Domain.Entities;

public sealed class ChunkingSettingsEntity
{
    public string ModelName { get; set; } = string.Empty;

    public int ChunkTargetSizeChars { get; set; }

    public int ChunkOverlapChars { get; set; }
}
