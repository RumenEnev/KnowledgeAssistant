namespace RagEvaluation.Models;

public sealed class ExperimentRun
{
    public required int Id { get; init; }

    public required string RunName { get; init; }

    public required string ChatModel { get; init; }

    public required string EmbeddingModel { get; init; }

    public required string JudgeModel { get; init; }

    public string ChunkingConfigNotes { get; init; } = "{}";

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public string? Notes { get; init; }
}