using KnowledgeAssistant.Domain.Documents;

namespace RagEvaluation.Desktop.Models;

public sealed class DocumentRow
{
    public required Document Source { get; init; }
    public int Id => Source.Id;
    public string Title => Source.Title;
    public string Topics { get; init; } = string.Empty;
    public int ChunkCount { get; init; }
    public string Created { get; init; } = string.Empty;
}