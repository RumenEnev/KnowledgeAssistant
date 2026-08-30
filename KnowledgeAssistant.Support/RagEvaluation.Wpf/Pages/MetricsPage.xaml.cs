using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using RagEvaluation.Desktop.Windows;
using RagEvaluation.Models;
using RagEvaluation.Services;
using System.Globalization;
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

    // Cancels a previous in-flight load if the user switches runs (or clicks Refresh)
    // again before it finishes, so two overlapping loads can't race and leave the
    // grids showing a mix of two different runs' data.
    private CancellationTokenSource? _loadCts;

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
        var ct = BeginLoad();
        SetBusy(true);
        try
        {
            var previouslySelectedId = (RunSelector.SelectedItem as ExperimentRun)?.Id;
            var runs = await _evaluationService.ListRunsAsync(ct);
            ct.ThrowIfCancellationRequested();

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
        catch (OperationCanceledException)
        {
            // A newer load superseded this one - nothing to do.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load evaluation runs for metrics page");
            StatusText.Text = $"Error loading runs: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task LoadMetricsForRunAsync(int runId)
    {
        var ct = BeginLoad();
        SetBusy(true);
        StatusText.Text = "Loading metrics...";
        try
        {
            var summary = await _evaluationService.GetRunSummaryAsync(runId, ct);
            ct.ThrowIfCancellationRequested();
            var queryMetrics = await _evaluationService.GetQueryMetricsAsync(runId, ct);
            ct.ThrowIfCancellationRequested();

            _currentSummary = summary;
            _currentQueryMetrics = queryMetrics;

            RenderSummaryTable(summary);
            QueryMetricsGrid.ItemsSource = queryMetrics;

            StatusText.Text = queryMetrics.Count == 0
                ? "No per-query metrics recorded for this run."
                : $"{queryMetrics.Count} queries scored.";
        }
        catch (OperationCanceledException)
        {
            // A newer load superseded this one - nothing to do.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load metrics for run {RunId}", runId);
            StatusText.Text = $"Error loading metrics: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private CancellationToken BeginLoad()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        return _loadCts.Token;
    }

    private void SetBusy(bool isBusy)
    {
        LoadingIndicator.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        RunSelector.IsEnabled = !isBusy;
        RefreshButton.IsEnabled = !isBusy;
        ExportButton.IsEnabled = !isBusy;
        ExportSummaryButton.IsEnabled = !isBusy;
    }

    private async void QueryMetricsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_currentSummary is null || QueryMetricsGrid.SelectedItem is not QueryMetricsRow row)
        {
            return;
        }

        try
        {
            var detail = await _evaluationService.GetQueryGenerationDetailAsync(_currentSummary.Run.Id, row.QueryId, CancellationToken.None);
            if (detail is null)
            {
                StatusText.Text = "No generation result recorded for this query (it may have been skipped during the run).";
                return;
            }

            var window = new GenerationDetailWindow(detail) { Owner = Window.GetWindow(this) };
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load generation detail for query {QueryId}", row.QueryId);
            StatusText.Text = $"Error loading detail: {ex.Message}";
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

    private void ExportSummaryButton_Click(object sender, RoutedEventArgs e)
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
            FileName = $"eval-summary-{runName}-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var csv = BuildSummaryCsv(_currentSummary);
            File.WriteAllText(dialog.FileName, csv, Encoding.UTF8);
            StatusText.Text = $"Exported summary to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export summary CSV");
            StatusText.Text = $"Export failed: {ex.Message}";
        }
    }

    /// <summary>Just the run info + mean scores, no per-query rows. Same InvariantCulture rule as BuildCsv.</summary>
    private static string BuildSummaryCsv(RunSummary summary)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Run,{CsvEscape(summary.Run.RunName)}");
        sb.AppendLine($"Chat Model,{CsvEscape(summary.Run.ChatModel)}");
        sb.AppendLine($"Judge Model,{CsvEscape(summary.Run.JudgeModel)}");
        sb.AppendLine($"Created,{summary.Run.CreatedAt:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        sb.AppendLine("Metric,Mean Value");
        sb.AppendLine($"Precision,{summary.MeanPrecisionAtK.ToString("F3", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Recall,{summary.MeanRecallAtK.ToString("F3", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"MRR,{summary.MeanReciprocalRank.ToString("F3", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"NDCG,{summary.MeanNdcgAtK.ToString("F3", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Faithfulness (1-5),{summary.MeanFaithfulness.ToString("F2", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Relevance (1-5),{summary.MeanRelevance.ToString("F2", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Completeness (1-5),{summary.MeanCompleteness.ToString("F2", CultureInfo.InvariantCulture)}");

        return sb.ToString();
    }

    /// <summary>
    /// Single CSV with two sections: run-level mean scores, then a blank line, then one
    /// row per query with both retrieval and generation metrics side by side. All numbers
    /// use InvariantCulture (period decimals) since commas are the field separator - a
    /// comma-decimal locale would otherwise corrupt the file structure.
    /// </summary>
    private static string BuildCsv(RunSummary summary, List<QueryMetricsRow> rows)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Run,{CsvEscape(summary.Run.RunName)}");
        sb.AppendLine($"Chat Model,{CsvEscape(summary.Run.ChatModel)}");
        sb.AppendLine($"Judge Model,{CsvEscape(summary.Run.JudgeModel)}");
        sb.AppendLine($"Created,{summary.Run.CreatedAt:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        sb.AppendLine("Metric,Mean Value");
        sb.AppendLine($"Precision,{summary.MeanPrecisionAtK.ToString("F3", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Recall,{summary.MeanRecallAtK.ToString("F3", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"MRR,{summary.MeanReciprocalRank.ToString("F3", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"NDCG,{summary.MeanNdcgAtK.ToString("F3", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Faithfulness (1-5),{summary.MeanFaithfulness.ToString("F2", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Relevance (1-5),{summary.MeanRelevance.ToString("F2", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Completeness (1-5),{summary.MeanCompleteness.ToString("F2", CultureInfo.InvariantCulture)}");
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
        => value.HasValue ? value.Value.ToString(format, CultureInfo.InvariantCulture) : "";

    /// <summary>Quotes a CSV field if it contains a comma, quote, or newline; doubles any embedded quotes.</summary>
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
            new("Precision", summary.MeanPrecisionAtK.ToString("F3", CultureInfo.InvariantCulture)),
            new("Recall", summary.MeanRecallAtK.ToString("F3", CultureInfo.InvariantCulture)),
            new("MRR", summary.MeanReciprocalRank.ToString("F3", CultureInfo.InvariantCulture)),
            new("NDCG", summary.MeanNdcgAtK.ToString("F3", CultureInfo.InvariantCulture)),
            new("Faithfulness (1-5)", summary.MeanFaithfulness.ToString("F2", CultureInfo.InvariantCulture)),
            new("Relevance (1-5)", summary.MeanRelevance.ToString("F2", CultureInfo.InvariantCulture)),
            new("Completeness (1-5)", summary.MeanCompleteness.ToString("F2", CultureInfo.InvariantCulture))
        };
    }

    /// <summary>Simple metric/value pair for the summary DataGrid - not a domain model, just a display row.</summary>
    private sealed record SummaryMetricRow(string Metric, string Value);
}