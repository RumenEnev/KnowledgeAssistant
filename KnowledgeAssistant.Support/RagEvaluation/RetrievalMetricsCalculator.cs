using KnowledgeAssistant.Domain.Documents;
using RagEvaluation.Interfaces;
using RagEvaluation.Models;

namespace KnowledgeAssistant.Eval.Core;

public sealed class RetrievalMetricsCalculator : IRetrievalMetricsCalculator
{
    public RetrievalMetrics Compute(TestQuery query, IReadOnlyList<SelectedChunk> candidates)
    {
        var included = candidates.Where(c => c.IncludedInBudget).OrderBy(c => c.Rank).ToList();
        var expected = new HashSet<int>(query.ExpectedChunkIds);

        if (expected.Count == 0)
        {
            return new RetrievalMetrics
            {
                QueryId = query.Id,
                IncludedCount = included.Count,
                PrecisionAtK = 0,
                RecallAtK = 0,
                ReciprocalRank = 0,
                NdcgAtK = 0
            };
        }

        var hits = included.Count(c => expected.Contains(c.ChunkId));
        var precision = included.Count == 0 ? 0.0 : (double)hits / included.Count;
        var recall = (double)hits / expected.Count;

        var firstHitRank = included.FirstOrDefault(c => expected.Contains(c.ChunkId))?.Rank;
        var reciprocalRank = firstHitRank.HasValue ? 1.0 / firstHitRank.Value : 0.0;

        var ndcg = ComputeNdcg(included, expected);

        return new RetrievalMetrics
        {
            QueryId = query.Id,
            IncludedCount = included.Count,
            PrecisionAtK = precision,
            RecallAtK = recall,
            ReciprocalRank = reciprocalRank,
            NdcgAtK = ndcg
        };
    }

    private static double ComputeNdcg(List<SelectedChunk> included, HashSet<int> expected)
    {
        double dcg = 0;
        for (var i = 0; i < included.Count; i++)
        {
            var rel = expected.Contains(included[i].ChunkId) ? 1.0 : 0.0;
            dcg += rel / Math.Log2(i + 2);
        }

        var idealHits = Math.Min(expected.Count, included.Count);
        double idcg = 0;
        for (var i = 0; i < idealHits; i++)
        {
            idcg += 1.0 / Math.Log2(i + 2);
        }

        return idcg == 0 ? 0 : dcg / idcg;
    }
}