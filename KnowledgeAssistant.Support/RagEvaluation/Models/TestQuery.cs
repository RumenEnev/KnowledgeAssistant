using RagEvaluation.Enums;

namespace RagEvaluation.Models;

public sealed class TestQuery
{
    public required int Id { get; init; }

    public required string QueryText { get; init; }

    public required QueryType Type { get; init; }

    public required int TopicId { get; init; }

    public int? SourceDocumentId { get; init; }

    public string? ExpectedAnswer { get; init; }

    public required List<int> ExpectedChunkIds { get; init; }
}