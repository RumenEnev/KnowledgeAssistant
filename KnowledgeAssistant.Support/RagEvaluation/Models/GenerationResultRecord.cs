namespace KnowledgeAssistant.Eval.Core.Models;

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