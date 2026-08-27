namespace RagEvaluation.Models;

public sealed class GenerationResult
{
    public required int QueryId { get; init; }

    public required int RunId { get; init; }

    public required string GeneratedAnswer { get; init; }

    public required List<int> ContextChunkIds { get; init; }
}