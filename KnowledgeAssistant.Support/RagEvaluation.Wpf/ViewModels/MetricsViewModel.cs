using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RagEvaluation.Desktop.Services;
using RagEvaluation.Models;
using RagEvaluation.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace RagEvaluation.Desktop.ViewModels;

public sealed partial class MetricsViewModel : OperationViewModelBase
{
    private readonly EvaluationService _evaluationService;
    private readonly ILogger<MetricsViewModel> _logger;

    private RunSummary? _currentSummary;
    private bool _suppressSelection;
    private CancellationTokenSource? _loadCts;

    public MetricsViewModel(EvaluationService evaluationService, ILogger<MetricsViewModel> logger) : base(logger)
    {
        _evaluationService = evaluationService;
        _logger = logger;
    }

    public ObservableCollection<ExperimentRun> Runs { get; } = new();
    public ObservableCollection<SummaryMetricRow> Summary { get; } = new();
    public ObservableCollection<QueryMetricsRow> QueryMetrics { get; } = new();

    [ObservableProperty]
    private ExperimentRun? _selectedRun;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExport))]
    private bool _hasRun;

    public bool CanExport => IsIdle && HasRun;

    protected override void OnBusyChanged() => OnPropertyChanged(nameof(CanExport));

    partial void OnSelectedRunChanged(ExperimentRun? value)
    {
        if (!_suppressSelection && value is not null)
        {
            _ = LoadMetricsAsync(value.Id);
        }
    }

    public Task LoadAsync() => ReloadRunsAsync();

    [RelayCommand]
    private Task RefreshAsync() => ReloadRunsAsync();

    private async Task ReloadRunsAsync()
    {
        var ct = BeginLoad();
        IsBusy = true;
        try
        {
            var previousId = SelectedRun?.Id;
            var runs = await _evaluationService.ListRunsAsync(ct);
            ct.ThrowIfCancellationRequested();

            _suppressSelection = true;
            try
            {
                Runs.Clear();
                foreach (var run in runs)
                {
                    Runs.Add(run);
                }

                SelectedRun = Runs.FirstOrDefault(r => r.Id == previousId) ?? Runs.FirstOrDefault();
            }
            finally
            {
                _suppressSelection = false;
            }

            if (SelectedRun is null)
            {
                Clear();
                StatusText = "No evaluation runs found. Run an evaluation first.";
                return;
            }

            await LoadMetricsCoreAsync(SelectedRun.Id, ct);
        }
        catch (OperationCanceledException)
        {
            // A newer load superseded this one.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load evaluation runs for the metrics page");
            StatusText = $"Error loading runs: {ex.Message}";
        }
        finally
        {
            EndLoad(ct);
        }
    }

    private async Task LoadMetricsAsync(int runId)
    {
        var ct = BeginLoad();
        IsBusy = true;
        try
        {
            await LoadMetricsCoreAsync(runId, ct);
        }
        catch (OperationCanceledException)
        {
            // A newer load superseded this one.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load metrics for run {RunId}", runId);
            StatusText = $"Error loading metrics: {ex.Message}";
        }
        finally
        {
            EndLoad(ct);
        }
    }

    private async Task LoadMetricsCoreAsync(int runId, CancellationToken ct)
    {
        StatusText = "Loading metrics…";
        var summary = await _evaluationService.GetRunSummaryAsync(runId, ct);
        ct.ThrowIfCancellationRequested();
        var queryMetrics = await _evaluationService.GetQueryMetricsAsync(runId, ct);
        ct.ThrowIfCancellationRequested();

        _currentSummary = summary;
        HasRun = true;

        Summary.Clear();
        Summary.Add(new SummaryMetricRow("Precision", MetricsCsvBuilder.Format(summary.MeanPrecisionAtK, "F3")));
        Summary.Add(new SummaryMetricRow("Recall", MetricsCsvBuilder.Format(summary.MeanRecallAtK, "F3")));
        Summary.Add(new SummaryMetricRow("MRR", MetricsCsvBuilder.Format(summary.MeanReciprocalRank, "F3")));
        Summary.Add(new SummaryMetricRow("NDCG", MetricsCsvBuilder.Format(summary.MeanNdcgAtK, "F3")));
        Summary.Add(new SummaryMetricRow("Faithfulness (1-5)", MetricsCsvBuilder.Format(summary.MeanFaithfulness, "F2")));
        Summary.Add(new SummaryMetricRow("Relevance (1-5)", MetricsCsvBuilder.Format(summary.MeanRelevance, "F2")));
        Summary.Add(new SummaryMetricRow("Completeness (1-5)", MetricsCsvBuilder.Format(summary.MeanCompleteness, "F2")));

        QueryMetrics.Clear();
        foreach (var row in queryMetrics)
        {
            QueryMetrics.Add(row);
        }

        StatusText = queryMetrics.Count == 0
            ? "No per-query metrics recorded for this run."
            : $"{queryMetrics.Count} queries scored. Double-click a row for the full answer and judge detail.";
    }

    private void Clear()
    {
        _currentSummary = null;
        HasRun = false;
        Summary.Clear();
        QueryMetrics.Clear();
    }

    private CancellationToken BeginLoad()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        return _loadCts.Token;
    }

    private void EndLoad(CancellationToken ct)
    {
        if (_loadCts is not null && _loadCts.Token == ct)
        {
            IsBusy = false;
        }
    }

    public string SuggestedFileName(bool summaryOnly)
    {
        var prefix = summaryOnly ? "eval-summary" : "eval-metrics";
        return $"{prefix}-{_currentSummary?.Run.RunName}-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
    }

    public void Export(string path, bool summaryOnly)
    {
        if (_currentSummary is null)
        {
            StatusText = "Nothing to export yet - select a run first.";
            return;
        }

        try
        {
            var csv = summaryOnly
                ? MetricsCsvBuilder.BuildSummary(_currentSummary)
                : MetricsCsvBuilder.Build(_currentSummary, QueryMetrics);
            File.WriteAllText(path, csv, Encoding.UTF8);
            StatusText = $"Exported to {path}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export metrics CSV");
            StatusText = $"Export failed: {ex.Message}";
        }
    }

    public async Task<QueryGenerationDetail?> LoadDetailAsync(QueryMetricsRow row)
    {
        if (_currentSummary is null)
        {
            return null;
        }

        try
        {
            var detail = await _evaluationService.GetQueryGenerationDetailAsync(_currentSummary.Run.Id, row.QueryId, CancellationToken.None);
            if (detail is null)
            {
                StatusText = "No generation result recorded for this query (it may have been skipped during the run).";
            }

            return detail;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load generation detail for query {QueryId}", row.QueryId);
            StatusText = $"Error loading detail: {ex.Message}";
            return null;
        }
    }
}