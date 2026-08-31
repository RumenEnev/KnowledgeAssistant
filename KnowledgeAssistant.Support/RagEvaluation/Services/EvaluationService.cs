using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Application.Services;
using KnowledgeAssistant.Domain.Conversation;
using KnowledgeAssistant.Eval.Core;
using Microsoft.Extensions.Logging;
using RagEvaluation.Interfaces;
using RagEvaluation.Models;

namespace RagEvaluation.Services;

public sealed record EvalRunOutcome(RunSummary Summary, int SkippedQueries);

public enum EvalPhase
{
    Retrieval,
    Generation,
    Judging,
    Skipped,
    Failed
}

public sealed record EvalProgress(int Done, int Total, EvalPhase Phase, string QueryText);

public sealed class EvaluationService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly DocumentsHandlingService _documentsHandlingService;
    private readonly IModelGateway _modelGateway;
    private readonly IExperimentRepository _experimentRepository;
    private readonly IRetrievalMetricsCalculator _metricsCalculator;
    private readonly ILlmJudge _judge;
    private readonly ILogger<EvaluationService>? _logger;

    public EvaluationService(
        IDocumentRepository documentRepository,
        DocumentsHandlingService documentsHandlingService,
        IModelGateway modelGateway,
        IExperimentRepository experimentRepository,
        IRetrievalMetricsCalculator? metricsCalculator = null,
        ILlmJudge? judge = null,
        ILogger<EvaluationService>? logger = null)
    {
        _documentRepository = documentRepository;
        _documentsHandlingService = documentsHandlingService;
        _modelGateway = modelGateway;
        _experimentRepository = experimentRepository;
        _metricsCalculator = metricsCalculator ?? new RetrievalMetricsCalculator();
        _judge = judge ?? throw new ArgumentNullException(nameof(judge),
            "ILlmJudge needs an IModelGateway to construct - pass Judging.LlmJudge built with the same gateway.");
        _logger = logger;
    }

    public async Task<EvalRunOutcome> RunEvalAsync(
        string runName,
        string chatModel,
        string embeddingModel,
        string judgeModel,
        IProgress<EvalProgress>? progress = null,
        CancellationToken ct = default)
    {
        var testQueries = await _experimentRepository.LoadTestQueriesAsync(ct);
        if (testQueries.Count == 0)
        {
            throw new InvalidOperationException("No test queries found - run test-set generation first.");
        }

        var allTopics = await _documentRepository.GetAllTopicsAsync(ct);
        var topicNameById = allTopics.ToDictionary(t => t.Id, t => t.Name);
        var run = await _experimentRepository.SaveRunAsync(new ExperimentRun
        {
            Id = 0, // ignored on insert, SaveRunAsync returns the DB-assigned id
            RunName = runName,
            ChatModel = chatModel,
            EmbeddingModel = embeddingModel,
            JudgeModel = judgeModel
        }, ct);

        var skipped = 0;
        var i = 0;
        foreach (var query in testQueries)
        {
            ct.ThrowIfCancellationRequested();
            i++;
            if (!topicNameById.TryGetValue(query.TopicId, out var topicName))
            {
                skipped++; // topic was deleted since test-set generation - can't evaluate this query
                progress?.Report(new EvalProgress(i, testQueries.Count, EvalPhase.Skipped, query.QueryText));
                continue;
            }

            try
            {
                progress?.Report(new EvalProgress(i, testQueries.Count, EvalPhase.Retrieval, query.QueryText));
                var selection = await _documentsHandlingService.SelectRelevantChunksAsync(
                    chatModel, topicName, query.QueryText, ct);

                if (selection is null || selection.Chunks.Count == 0)
                {
                    skipped++; // no candidates at all under this topic - nothing to score
                    progress?.Report(new EvalProgress(i, testQueries.Count, EvalPhase.Skipped, query.QueryText));
                    continue;
                }

                var retrievalMetrics = _metricsCalculator.Compute(query, selection.Chunks);
                await _experimentRepository.SaveRetrievalResultAsync(run.Id, query.Id, selection.Chunks, retrievalMetrics, ct);
                var includedIds = selection.Chunks
                    .Where(c => c.IncludedInBudget)
                    .OrderBy(c => c.Rank)
                    .Select(c => c.ChunkId)
                    .ToList();

                if (includedIds.Count == 0)
                {
                    skipped++;
                    progress?.Report(new EvalProgress(i, testQueries.Count, EvalPhase.Skipped, query.QueryText));
                    continue;
                }

                var contextChunks = (await _documentRepository.GetChunksByIdsAsync(includedIds, ct)).ToList();
                contextChunks = includedIds
                    .Select(id => contextChunks.First(c => c.Id == id))
                    .ToList();

                var contextText = string.Join("\n\n---\n\n", contextChunks.Select(c => c.ChunkText));
                var systemMessage = new ChatMessage
                {
                    Role = "system",
                    Content = "Content = Answer the user's question using ONLY the provided context. Be thorough: " +
                    "include all relevant facts, factors, or perspectives mentioned in the context that address the question, " +
                    "not just the first one you find. If the context presents multiple sides, causes, or examples relevant to the question, " +
                    "mention all of them. If the context doesn't contain the answer, say so explicitly."
                };
                var userMessage = new ChatMessage
                {
                    Role = "user",
                    Content = $"Context:\n{contextText}\n\nQuestion: {query.QueryText}"
                };

                progress?.Report(new EvalProgress(i, testQueries.Count, EvalPhase.Generation, query.QueryText));
                var answer = await _modelGateway.GenerateAsync(chatModel, userMessage, systemMessage, ct);
                var generationResult = new GenerationResult
                {
                    QueryId = query.Id,
                    RunId = run.Id,
                    GeneratedAnswer = answer,
                    ContextChunkIds = includedIds
                };

                progress?.Report(new EvalProgress(i, testQueries.Count, EvalPhase.Judging, query.QueryText));
                var generationMetrics = await _judge.ScoreAsync(judgeModel, query, generationResult, contextChunks, ct);
                await _experimentRepository.SaveGenerationResultAsync(
                    generationResult, generationMetrics, KnowledgeAssistant.Eval.Core.Judging.LlmJudge.JudgePromptVersion, judgeModel, ct);

                progress?.Report(new EvalProgress(i, testQueries.Count, EvalPhase.Judging, query.QueryText));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Query {QueryId} failed - skipping. Query text: {QueryText}", query.Id, query.QueryText);
                skipped++;
                progress?.Report(new EvalProgress(i, testQueries.Count, EvalPhase.Failed, query.QueryText));
            }
        }

        var summary = await _experimentRepository.GetRunSummaryAsync(run.Id, ct);
        return new EvalRunOutcome(summary, skipped);
    }

    public async Task<QueryGenerationDetail?> GetQueryGenerationDetailAsync(int runId, int queryId, CancellationToken ct = default)
    {
        var record = await _experimentRepository.GetGenerationResultAsync(runId, queryId, ct);
        if (record is null)
        {
            return null; // query was skipped during this run - nothing to show
        }

        var chunks = (await _documentRepository.GetChunksByIdsAsync(record.ContextChunkIds, ct)).ToList();
        var orderedChunks = record.ContextChunkIds
            .Select(id => chunks.FirstOrDefault(c => c.Id == id))
            .Where(c => c is not null)
            .Select(c => new ContextChunkDetail { ChunkId = c!.Id, ChunkText = c.ChunkText })
            .ToList();

        var queryText = (await _experimentRepository.LoadTestQueriesAsync(ct))
            .FirstOrDefault(q => q.Id == queryId)?.QueryText ?? "(query text unavailable)";

        return new QueryGenerationDetail
        {
            QueryId = queryId,
            QueryText = queryText,
            GeneratedAnswer = record.GeneratedAnswer,
            ContextChunks = orderedChunks,
            FaithfulnessScore = record.FaithfulnessScore,
            RelevanceScore = record.RelevanceScore,
            CompletenessScore = record.CompletenessScore,
            JudgeRationale = record.JudgeRationale,
            JudgeModel = record.JudgeModel
        };
    }

    public Task<RunSummary> GetRunSummaryAsync(int runId, CancellationToken ct = default)
        => _experimentRepository.GetRunSummaryAsync(runId, ct);

    public Task<List<ExperimentRun>> ListRunsAsync(CancellationToken ct = default)
        => _experimentRepository.ListRunsAsync(ct);

    public Task CleanDatabaseAsync(CancellationToken ct = default)
        => _experimentRepository.CleanDatabaseAsync(ct);

    public Task<List<QueryMetricsRow>> GetQueryMetricsAsync(int runId, CancellationToken ct = default)
        => _experimentRepository.GetQueryMetricsAsync(runId, ct);

    public Task<(IEnumerable<KnowledgeAssistant.Domain.Documents.ChunkListItem> Chunks, int TotalCount)> GetAllChunksAsync(
        int page, int pageSize, string? searchText, CancellationToken ct = default)
        => _documentRepository.GetAllChunksAsync(page, pageSize, searchText, ct);

    public Task UpdateChunkTextAsync(int chunkId, string chunkText, CancellationToken ct = default)
        => _documentRepository.UpdateChunkTextAsync(chunkId, chunkText, ct);

    public Task DeleteChunkAsync(int chunkId, CancellationToken ct = default)
        => _documentRepository.DeleteChunkAsync(chunkId, ct);
}