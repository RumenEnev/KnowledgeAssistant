using RagEvaluation.Models;

namespace RagEvaluation.Interfaces;

public interface IRetrievalMetricsCalculator
{
    RetrievalMetrics Compute(TestQuery query, IReadOnlyList<KnowledgeAssistant.Domain.Documents.SelectedChunk> candidates);
}