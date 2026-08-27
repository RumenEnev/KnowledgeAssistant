namespace RagEvaluation.Models;

public sealed class EvalQueryRow
{
    public int Id { get; init; }

    public string QueryText { get; init; } = "";

    public string QueryType { get; init; } = "";

    public int TopicId { get; init; }

    public int? SourceDocumentId { get; init; }

    public string? ExpectedAnswer { get; init; }

    public int[]? ExpectedChunkIds { get; init; }
}