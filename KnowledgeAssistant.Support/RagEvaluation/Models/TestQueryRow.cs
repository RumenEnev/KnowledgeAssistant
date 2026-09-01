using RagEvaluation.Enums;

namespace RagEvaluation.Models;

public sealed class TestQueryRow
{
    public int Id { get; init; }
    public required string QueryText { get; init; }
    public QueryType Type { get; init; }
    public int TopicId { get; init; }
    public int? SourceDocumentId { get; init; }
    public string? ExpectedAnswer { get; init; }
    public int[] ExpectedChunkIds { get; init; } = [];
}