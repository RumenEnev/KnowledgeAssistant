namespace RagEvaluation.Models;

public sealed class GenerationResultRow
{
    public string GeneratedAnswer { get; init; } = "";
    public string ContextChunkIdsJson { get; init; } = "[]";
    public double? FaithfulnessScore { get; init; }
    public double? RelevanceScore { get; init; }
    public double? CompletenessScore { get; init; }
    public string? JudgeRationale { get; init; }
    public string? JudgeModel { get; init; }
}
