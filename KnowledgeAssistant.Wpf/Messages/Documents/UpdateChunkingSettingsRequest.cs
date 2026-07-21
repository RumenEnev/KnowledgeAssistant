using MessageServices;

namespace KnowledgeAssistant.Wpf.Messages.Documents
{
    public record UpdateChunkingSettingsRequest : MessageBase
    {
        public UpdateChunkingSettingsRequest(int chunkTargetSizeChars, int chunkOverlapChars)
        {
            ChunkTargetSizeChars = chunkTargetSizeChars;
            ChunkOverlapChars = chunkOverlapChars;
        }

        public int ChunkTargetSizeChars { get; }

        public int ChunkOverlapChars { get; }
    }
}
