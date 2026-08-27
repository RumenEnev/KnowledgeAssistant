namespace RagEvaluation.Models;

public sealed class RetrievalMetrics
{
    public required int QueryId { get; init; }

    public required int IncludedCount { get; init; }

    public required double PrecisionAtK { get; init; }

    public required double RecallAtK { get; init; }

    public required double ReciprocalRank { get; init; }

    public required double NdcgAtK { get; init; }
}