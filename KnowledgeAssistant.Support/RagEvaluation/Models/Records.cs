using KnowledgeAssistant.Contracts.Enums;
using RagEvaluation.Enums;

namespace RagEvaluation.Models;

public sealed record IngestOptions(string EmbeddingModel, int ChunkSize, int ChunkOverlap, DocumentType DocumentType, IReadOnlyList<string> TopicNames, bool AllDocuments);

public sealed record PipelineStatus(int Documents, int Chunks, int TestQuestions, string? LastRunName, double? LastRunRecall, string? LastRunWhen);

public sealed record StatusItem(string Label, string Value, string? ToolTip = null);

public sealed record DocumentChoice(int? Id, string Title);

public sealed record GenerateTestSetOptions(string Provider, string Model, int QuestionsPerChunk, int? DocumentId, bool SkipExisting);

public sealed record EvalRunOutcome(RunSummary Summary, int SkippedQueries, int TotalQueries = 0, int TopicMissing = 0, int NoCandidates = 0, int NothingIncluded = 0, int Failed = 0);

public sealed record TestSetHealth(int Questions, int Orphaned);

public sealed record SummaryMetricRow(string Metric, string Value);

public sealed record PreflightItem(PreflightLevel Level, string Text)
{
    public string Icon => Level switch
    {
        PreflightLevel.Ok => "✓",
        PreflightLevel.Warning => "⚠",
        _ => "✗"
    };
}