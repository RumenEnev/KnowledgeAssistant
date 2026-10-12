using KnowledgeAssistant.Application.Abstraction;
using Microsoft.Extensions.Logging;
using RagEvaluation.Desktop.Models;
using RagEvaluation.Interfaces;
using RagEvaluation.Services;
using System.Globalization;

namespace RagEvaluation.Desktop.Services;

public sealed class PipelineStatusService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IExperimentRepository _experimentRepository;
    private readonly EvaluationService _evaluationService;
    private readonly ILogger<PipelineStatusService> _logger;

    public PipelineStatusService(
        IDocumentRepository documentRepository,
        IExperimentRepository experimentRepository,
        EvaluationService evaluationService,
        ILogger<PipelineStatusService> logger)
    {
        _documentRepository = documentRepository;
        _experimentRepository = experimentRepository;
        _evaluationService = evaluationService;
        _logger = logger;
    }

    public async Task<PipelineStatus> GetAsync(CancellationToken ct)
    {
        var documents = (await _documentRepository.GetAllDocumentsAsync(ct)).Count();
        var (_, chunkTotal) = await _evaluationService.GetAllChunksAsync(1, 1, null, ct);
        var testQuestions = (await _experimentRepository.LoadTestQueriesAsync(ct)).Count();

        string? lastRunName = null;
        double? lastRunRecall = null;
        string? lastRunWhen = null;

        var runs = await _evaluationService.ListRunsAsync(ct);
        var lastRun = runs.OrderByDescending(r => r.CreatedAt).FirstOrDefault();
        if (lastRun is not null)
        {
            lastRunName = lastRun.RunName;
            lastRunWhen = $"{lastRun.CreatedAt:yyyy-MM-dd HH:mm}";

            try
            {
                var summary = await _evaluationService.GetRunSummaryAsync(lastRun.Id, ct);
                lastRunRecall = summary.MeanRecallAtK;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load summary for last run {RunId}", lastRun.Id);
            }
        }

        return new PipelineStatus(documents, chunkTotal, testQuestions, lastRunName, lastRunRecall, lastRunWhen);
    }

    public static string FormatRecall(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
}