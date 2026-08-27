namespace RagEvaluation.Models;

public sealed class RunSummaryMetricsRow
{
    public double MeanPrecisionAtK { get; init; }

    public double MeanRecallAtK { get; init; }

    public double MeanReciprocalRank { get; init; }

    public double MeanNdcgAtK { get; init; }

    public double MeanFaithfulness { get; init; }

    public double MeanRelevance { get; init; }

    public double MeanCompleteness { get; init; }
}   