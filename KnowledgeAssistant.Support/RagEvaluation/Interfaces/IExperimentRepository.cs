using KnowledgeAssistant.Domain.Documents;
using KnowledgeAssistant.Eval.Core.Models;
using RagEvaluation.Models;

namespace RagEvaluation.Interfaces;

public interface IExperimentRepository
{
    Task<List<TestQuery>> SaveTestQueriesAsync(IEnumerable<NewTestQuery> queries, CancellationToken ct = default);

    Task<List<TestQuery>> LoadTestQueriesAsync(CancellationToken ct = default);

    Task<ExperimentRun> SaveRunAsync(ExperimentRun run, CancellationToken ct = default);

    Task SaveRetrievalResultAsync(int runId, int queryId, IReadOnlyList<SelectedChunk> candidates, RetrievalMetrics metrics, CancellationToken ct = default);

    Task SaveGenerationResultAsync(GenerationResult result, GenerationMetrics metrics, string judgePromptVersion, string judgeModel, CancellationToken ct = default);

    Task<RunSummary> GetRunSummaryAsync(int runId, CancellationToken ct = default);

    Task<List<ExperimentRun>> ListRunsAsync(CancellationToken ct = default);

    Task CleanDatabaseAsync(CancellationToken ct = default);

    Task<List<QueryMetricsRow>> GetQueryMetricsAsync(int runId, CancellationToken ct = default);

    Task<GenerationResultRecord?> GetGenerationResultAsync(int runId, int queryId, CancellationToken ct = default);
}