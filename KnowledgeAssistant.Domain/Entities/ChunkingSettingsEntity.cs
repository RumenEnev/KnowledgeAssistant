namespace KnowledgeAssistant.Domain.Entities;

public sealed class ChunkingSettingsEntity
{
    public Guid ModelId { get; set; }   

    public int ChunkTargetSizeChars { get; set; }

    public int ChunkOverlapChars { get; set; }
}
