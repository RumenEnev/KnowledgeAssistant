namespace RagEvaluation.Models;

public sealed class QueryMetricsRow
{
    public required int QueryId { get; init; }

    public required string QueryText { get; init; }

    public double? PrecisionAtK { get; init; }

    public double? RecallAtK { get; init; }

    public double? ReciprocalRank { get; init; }

    public double? NdcgAtK { get; init; }

    public double? FaithfulnessScore { get; init; }

    public double? RelevanceScore { get; init; }

    public double? CompletenessScore { get; init; }
}
