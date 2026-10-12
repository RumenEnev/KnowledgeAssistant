using KnowledgeAssistant.Application.Abstraction;
using RagEvaluation.Interfaces;
using RagEvaluation.Models;

namespace RagEvaluation.Desktop.Services;

public sealed class TestSetHealthService
{
    private readonly IExperimentRepository _experimentRepository;
    private readonly IDocumentRepository _documentRepository;

    public TestSetHealthService(IExperimentRepository experimentRepository, IDocumentRepository documentRepository)
    {
        _experimentRepository = experimentRepository;
        _documentRepository = documentRepository;
    }

    public async Task<TestSetHealth> GetAsync(int? documentId, CancellationToken ct)
    {
        var queries = (await _experimentRepository.LoadTestQueriesAsync(documentId, ct)).ToList();
        var expectedByQuery = new List<List<int>>();
        foreach (var query in queries)
        {
            expectedByQuery.Add(query.ExpectedChunkIds is null ? new List<int>() : query.ExpectedChunkIds.ToList());
        }

        var existing = new HashSet<int>();
        foreach (var batch in expectedByQuery.SelectMany(e => e).Distinct().Chunk(500))
        {
            foreach (var chunk in await _documentRepository.GetChunksByIdsAsync(batch.ToList(), ct))
            {
                existing.Add(chunk.Id);
            }
        }

        var orphaned = expectedByQuery.Count(e => e.Count > 0 && e.Any(id => !existing.Contains(id)));
        return new TestSetHealth(queries.Count, orphaned);
    }
}