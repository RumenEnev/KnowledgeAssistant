namespace RagEvaluation.Models;

public sealed class GenerationMetrics
{
    public required int QueryId { get; init; }

    public required double FaithfulnessScore { get; init; } // 1-5

    public required double RelevanceScore { get; init; }     // 1-5

    public required double CompletenessScore { get; init; }  // 1-5

    public required string JudgeRationale { get; init; }
}