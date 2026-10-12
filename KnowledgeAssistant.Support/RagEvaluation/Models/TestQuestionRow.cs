namespace RagEvaluation.Models;

public sealed class TestQuestionRow
{
    public int Id { get; init; }
    public string Question { get; init; } = string.Empty;
    public int? DocumentId { get; init; }
    public string Document { get; init; } = string.Empty;
    public string Topic { get; init; } = string.Empty;
    public string Generator { get; init; } = string.Empty;
    public IReadOnlyList<int> ExpectedChunkIds { get; init; } = Array.Empty<int>();
    public bool IsOrphaned { get; set; }
    public string Status => ExpectedChunkIds.Count == 0 ? "No expected chunk" : IsOrphaned ? "Orphaned" : "OK";
}