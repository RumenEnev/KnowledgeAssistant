namespace KnowledgeAssistant.Domain.Documents;

public sealed class SelectedContextChunks
{
    public required List<SelectedChunk> Chunks { get; init; }
}
