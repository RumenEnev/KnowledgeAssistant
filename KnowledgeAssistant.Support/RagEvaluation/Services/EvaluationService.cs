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
                    Content = "Answer the user's question using ONLY the provided context. If the context doesn't contain the answer, say so explicitly."
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
                // any per-query failure (retrieval, generation, or judging - e.g. malformed judge JSON, model
                // timeout) shouldn't abort the whole run; skip this query and keep going so partial results are saved.
                _logger?.LogError(ex, "Query {QueryId} failed - skipping. Query text: {QueryText}", query.Id, query.QueryText);
                skipped++;
                progress?.Report(new EvalProgress(i, testQueries.Count, EvalPhase.Failed, query.QueryText));
            }
        }

        var summary = await _experimentRepository.GetRunSummaryAsync(run.Id, ct);
        return new EvalRunOutcome(summary, skipped);
    }

    public Task<RunSummary> GetRunSummaryAsync(int runId, CancellationToken ct = default)
        => _experimentRepository.GetRunSummaryAsync(runId, ct);

    public Task<List<ExperimentRun>> ListRunsAsync(CancellationToken ct = default)
        => _experimentRepository.ListRunsAsync(ct);

    public Task CleanDatabaseAsync(CancellationToken ct = default)
        => _experimentRepository.CleanDatabaseAsync(ct);
}