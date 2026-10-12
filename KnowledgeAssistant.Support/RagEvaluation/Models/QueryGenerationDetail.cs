namespace RagEvaluation.Models;

public sealed class QueryGenerationDetail
{
    public required int QueryId { get; init; }
    public required string QueryText { get; init; }
    public required string GeneratedAnswer { get; init; }
    public required List<ContextChunkDetail> ContextChunks { get; init; }
    public double? FaithfulnessScore { get; init; }
    public double? RelevanceScore { get; init; }
    public double? CompletenessScore { get; init; }
    public string? JudgeRationale { get; init; }
    public string? JudgeModel { get; init; }
}