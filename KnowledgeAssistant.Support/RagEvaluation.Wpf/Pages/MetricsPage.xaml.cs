using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using RagEvaluation.Models;
using RagEvaluation.Services;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace RagEvaluation.Desktop.Pages;

public partial class MetricsPage : Page
{
    private readonly EvaluationService _evaluationService;
    private readonly ILogger<MetricsPage> _logger;

    private RunSummary? _currentSummary;
    private List<QueryMetricsRow> _currentQueryMetrics = new();

    public MetricsPage(EvaluationService evaluationService, ILogger<MetricsPage> logger)
    {
        _evaluationService = evaluationService;
        _logger = logger;

        InitializeComponent();

        Loaded += async (_, _) => await LoadRunsAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadRunsAsync();
    }

    private async void RunSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RunSelector.SelectedItem is ExperimentRun run)
        {
            await LoadMetricsForRunAsync(run.Id);
        }
    }

    private async Task LoadRunsAsync()
    {
        RefreshButton.IsEnabled = false;
        try
        {
            var previouslySelectedId = (RunSelector.SelectedItem as ExperimentRun)?.Id;
            var runs = await _evaluationService.ListRunsAsync(CancellationToken.None);
            RunSelector.ItemsSource = runs;

            if (runs.Count == 0)
            {
                StatusText.Text = "No evaluation runs found. Run an evaluation first.";
                return;
            }

            var toSelect = previouslySelectedId is not null
                ? runs.FirstOrDefault(r => r.Id == previouslySelectedId) ?? runs[0]
                : runs[0];

            RunSelector.SelectedItem = toSelect;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load evaluation runs for metrics page");
            StatusText.Text = $"Error loading runs: {ex.Message}";
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    private async Task LoadMetricsForRunAsync(int runId)
    {
        StatusText.Text = "Loading metrics...";
        try
        {
            var summary = await _evaluationService.GetRunSummaryAsync(runId, CancellationToken.None);
            var queryMetrics = await _evaluationService.GetQueryMetricsAsync(runId, CancellationToken.None);

            _currentSummary = summary;
            _currentQueryMetrics = queryMetrics;

            RenderSummaryTable(summary);
            QueryMetricsGrid.ItemsSource = queryMetrics;

            StatusText.Text = queryMetrics.Count == 0
                ? "No per-query metrics recorded for this run."
                : $"{queryMetrics.Count} queries scored.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load metrics for run {RunId}", runId);
            StatusText.Text = $"Error loading metrics: {ex.Message}";
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSummary is null)
        {
            StatusText.Text = "Nothing to export yet - select a run first.";
            return;
        }

        var runName = _currentSummary.Run.RunName;
        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"eval-metrics-{runName}-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var csv = BuildCsv(_currentSummary, _currentQueryMetrics);
            File.WriteAllText(dialog.FileName, csv, Encoding.UTF8);
            StatusText.Text = $"Exported to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export metrics CSV");
            StatusText.Text = $"Export failed: {ex.Message}";
        }
    }

    private static string BuildCsv(RunSummary summary, List<QueryMetricsRow> rows)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Run,{CsvEscape(summary.Run.RunName)}");
        sb.AppendLine($"Chat Model,{CsvEscape(summary.Run.ChatModel)}");
        sb.AppendLine($"Judge Model,{CsvEscape(summary.Run.JudgeModel)}");
        sb.AppendLine($"Created,{summary.Run.CreatedAt:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        sb.AppendLine("Metric,Mean Value");
        sb.AppendLine($"Precision,{summary.MeanPrecisionAtK:F3}");
        sb.AppendLine($"Recall,{summary.MeanRecallAtK:F3}");
        sb.AppendLine($"MRR,{summary.MeanReciprocalRank:F3}");
        sb.AppendLine($"NDCG,{summary.MeanNdcgAtK:F3}");
        sb.AppendLine($"Faithfulness (1-5),{summary.MeanFaithfulness:F2}");
        sb.AppendLine($"Relevance (1-5),{summary.MeanRelevance:F2}");
        sb.AppendLine($"Completeness (1-5),{summary.MeanCompleteness:F2}");
        sb.AppendLine();

        sb.AppendLine("Query,Precision,Recall,MRR,NDCG,Faithfulness,Relevance,Completeness");
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                CsvEscape(row.QueryText),
                FormatNullable(row.PrecisionAtK, "F3"),
                FormatNullable(row.RecallAtK, "F3"),
                FormatNullable(row.ReciprocalRank, "F3"),
                FormatNullable(row.NdcgAtK, "F3"),
                FormatNullable(row.FaithfulnessScore, "F2"),
                FormatNullable(row.RelevanceScore, "F2"),
                FormatNullable(row.CompletenessScore, "F2")
            }));
        }

        return sb.ToString();
    }

    private static string FormatNullable(double? value, string format)
        => value.HasValue ? value.Value.ToString(format) : "";

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private void RenderSummaryTable(RunSummary summary)
    {
        SummaryGrid.ItemsSource = new List<SummaryMetricRow>
        {
            new("Precision", summary.MeanPrecisionAtK.ToString("F3")),
            new("Recall", summary.MeanRecallAtK.ToString("F3")),
            new("MRR", summary.MeanReciprocalRank.ToString("F3")),
            new("NDCG", summary.MeanNdcgAtK.ToString("F3")),
            new("Faithfulness (1-5)", summary.MeanFaithfulness.ToString("F2")),
            new("Relevance (1-5)", summary.MeanRelevance.ToString("F2")),
            new("Completeness (1-5)", summary.MeanCompleteness.ToString("F2"))
        };
    }

    private sealed record SummaryMetricRow(string Metric, string Value);
}