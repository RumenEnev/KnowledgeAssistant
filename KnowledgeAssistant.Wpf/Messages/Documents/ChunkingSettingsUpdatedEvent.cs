using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents
{
    public record ChunkingSettingsUpdatedEvent : MessageBase
    {
        public ChunkingSettingsUpdatedEvent(int chunkTargetSizeChars, int chunkOverlapChars)
        {
            ChunkTargetSizeChars = chunkTargetSizeChars;
            ChunkOverlapChars = chunkOverlapChars;
        }

        public int ChunkTargetSizeChars { get; }

        public int ChunkOverlapChars { get; }
    }
}
