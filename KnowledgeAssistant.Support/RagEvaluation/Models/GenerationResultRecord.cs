namespace KnowledgeAssistant.Eval.Core.Models;

/// <summary>Raw row data from eval_generation_results + eval_generation_metrics, before chunk text is resolved.</summary>
public sealed class GenerationResultRecord
{
    public required string GeneratedAnswer { get; init; }
    public required List<int> ContextChunkIds { get; init; }
    public double? FaithfulnessScore { get; init; }
    public double? RelevanceScore { get; init; }
    public double? CompletenessScore { get; init; }
    public string? JudgeRationale { get; init; }
    public string? JudgeModel { get; init; }
}