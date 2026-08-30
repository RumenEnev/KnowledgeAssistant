namespace RagEvaluation.Models;

/// <summary>Everything needed to show "what did the model actually see and say" for one query in one run.</summary>
public sealed class QueryGenerationDetail
{
    public required int QueryId { get; init; }
    public required string QueryText { get; init; }
    public required string GeneratedAnswer { get; init; }

    /// <summary>Chunks actually included in the token budget, in the order the model saw them.</summary>
    public required List<ContextChunkDetail> ContextChunks { get; init; }

    public double? FaithfulnessScore { get; init; }
    public double? RelevanceScore { get; init; }
    public double? CompletenessScore { get; init; }
    public string? JudgeRationale { get; init; }
    public string? JudgeModel { get; init; }
}

public sealed class ContextChunkDetail
{
    public required int ChunkId { get; init; }
    public required string ChunkText { get; init; }
}