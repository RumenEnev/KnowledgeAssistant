namespace RagEvaluation.Desktop.Models;

public sealed record PipelineStatus(int Documents, int Chunks, int TestQuestions, string? LastRunName, double? LastRunRecall, string? LastRunWhen);