namespace RagEvaluation.Models;

public sealed class RunSummary
{
    public required ExperimentRun Run { get; init; }

    public required double MeanPrecisionAtK { get; init; }

    public required double MeanRecallAtK { get; init; }

    public required double MeanReciprocalRank { get; init; }

    public required double MeanNdcgAtK { get; init; }

    public required double MeanFaithfulness { get; init; }

    public required double MeanRelevance { get; init; }

    public required double MeanCompleteness { get; init; }
}