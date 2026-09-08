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

    /// <summary>Model provider (e.g. "Ollama", "AdessoAiHub") used for chat generation in this run.</summary>
    public string? ChatProvider { get; init; }

    /// <summary>Model provider (e.g. "Ollama", "AdessoAiHub") used for judging in this run.</summary>
    public string? JudgeProvider { get; init; }
}