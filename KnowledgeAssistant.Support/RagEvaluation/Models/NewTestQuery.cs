using RagEvaluation.Enums;

namespace RagEvaluation.Models;

public sealed class NewTestQuery
{
    public required string QueryText { get; init; }

    public required QueryType Type { get; init; }

    public required int TopicId { get; init; }

    public int? SourceDocumentId { get; init; }

    public string? ExpectedAnswer { get; init; }

    public required List<int> ExpectedChunkIds { get; init; }
}