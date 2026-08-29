using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RagEvaluation.Models;
using RagEvaluation.Services;
using System.Windows;
using System.Windows.Controls;

namespace RagEvaluation.Desktop.Pages;

public partial class RunEvalPage : Page
{
    private readonly EvaluationService _evaluationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RunEvalPage> _logger;

    public RunEvalPage(EvaluationService evaluationService, IConfiguration configuration, ILogger<RunEvalPage> logger)
    {
        _evaluationService = evaluationService;
        _configuration = configuration;
        _logger = logger;

        InitializeComponent();

        ChatModelBox.Text = _configuration["Llm:ChatModel"] ?? string.Empty;
        EmbeddingModelBox.Text = _configuration["Llm:EmbeddingModel"] ?? string.Empty;
        JudgeModelBox.Text = _configuration["Llm:JudgeModel"] ?? string.Empty;
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        RunButton.IsEnabled = false;
        ProgressBarControl.Visibility = Visibility.Visible;
        ProgressBarControl.Value = 0;
        ProgressText.Text = string.Empty;
        ResultText.Text = string.Empty;

        var runName = string.IsNullOrWhiteSpace(RunNameBox.Text)
            ? $"run-{DateTime.UtcNow:yyyyMMdd-HHmmss}"
            : RunNameBox.Text.Trim();
        var chatModel = ChatModelBox.Text.Trim();
        var embeddingModel = EmbeddingModelBox.Text.Trim();
        var judgeModel = JudgeModelBox.Text.Trim();

        try
        {
            var progress = new Progress<EvalProgress>(p =>
            {
                ProgressBarControl.Maximum = Math.Max(p.Total, 1);
                ProgressBarControl.Value = p.Done;
                var phaseLabel = p.Phase switch
                {
                    EvalPhase.Retrieval => "Retrieving chunks",
                    EvalPhase.Generation => "Generating answer",
                    EvalPhase.Judging => "Judging answer",
                    EvalPhase.Skipped => "Skipped",
                    EvalPhase.Failed => "Failed - skipping",
                    _ => p.Phase.ToString()
                };

                ProgressText.Text = $"{p.Done}/{p.Total} queries - {phaseLabel}: {p.QueryText}";
            });

            var outcome = await _evaluationService.RunEvalAsync(runName, chatModel, embeddingModel, judgeModel, progress, CancellationToken.None);

            ResultText.Text = FormatSummary(outcome);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Evaluation run '{RunName}' failed", runName);
            ResultText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            RunButton.IsEnabled = true;
            ProgressBarControl.Visibility = Visibility.Collapsed;
        }
    }

    private static string FormatSummary(EvalRunOutcome outcome)
    {
        var summary = outcome.Summary;
        var text = $"=== Run: {summary.Run.RunName} (chat: {summary.Run.ChatModel}, judge: {summary.Run.JudgeModel}) ===\n" +
            $"Retrieval  - Precision: {summary.MeanPrecisionAtK:F3}  Recall: {summary.MeanRecallAtK:F3}  MRR: {summary.MeanReciprocalRank:F3}  NDCG: {summary.MeanNdcgAtK:F3}\n" +
            $"Generation - Faithfulness: {summary.MeanFaithfulness:F2}/5  Relevance: {summary.MeanRelevance:F2}/5  Completeness: {summary.MeanCompleteness:F2}/5";

        if (outcome.SkippedQueries > 0)
        {
            text += $"\n\n({outcome.SkippedQueries} queries skipped - no candidates or empty budget selection)";
        }

        return text;
    }
}
