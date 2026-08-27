namespace KnowledgeAssistant.Domain.Documents;

public sealed class SelectedChunk
{
    public required int ChunkId { get; init; }

    public required int Rank { get; init; }

    public required string ChunkText { get; init; }

    public required int ApproxTokens { get; init; }

    public required bool IncludedInBudget { get; init; }
}