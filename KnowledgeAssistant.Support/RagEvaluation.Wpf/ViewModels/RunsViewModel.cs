using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RagEvaluation.Models;
using RagEvaluation.Services;
using System.Collections.ObjectModel;

namespace RagEvaluation.Desktop.ViewModels;

public sealed partial class RunsViewModel : OperationViewModelBase
{
    private readonly EvaluationService _evaluationService;
    private readonly ShellViewModel _shell;
    private readonly ILogger<RunsViewModel> _logger;

    private int _summaryVersion;

    public RunsViewModel(
        EvaluationService evaluationService,
        ShellViewModel shell,
        ILogger<RunsViewModel> logger)
        : base(logger)
    {
        _evaluationService = evaluationService;
        _shell = shell;
        _logger = logger;
    }

    public ObservableCollection<ExperimentRun> Runs { get; } = new();

    [ObservableProperty]
    private ExperimentRun? _selectedRun;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private string _detailText = "Select a run to view its summary.";

    partial void OnSelectedRunChanged(ExperimentRun? value) => _ = LoadDetailAsync(value);

    protected override async Task OnOperationFinishedAsync()
    {
        await ReloadAsync();
        await _shell.RefreshAsync();
    }

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        await ReloadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        await ReloadAsync();
        await _shell.RefreshAsync();
    }

    public Task CleanDatabaseAsync()
    {
        return RunOperationAsync(
            "Cleaning the database…",
            async ct =>
            {
                await _evaluationService.CleanDatabaseAsync(ct);
                return "Database cleaned.";
            });
    }

    private async Task ReloadAsync()
    {
        try
        {
            var previousId = SelectedRun?.Id;
            var runs = await _evaluationService.ListRunsAsync(CancellationToken.None);

            Runs.Clear();
            foreach (var run in runs)
            {
                Runs.Add(run);
            }

            SelectedRun = Runs.FirstOrDefault(r => r.Id == previousId);
            SummaryText = Runs.Count == 0
                ? "No evaluation runs yet. Start one on the Run page."
                : $"{Runs.Count} run(s).";
            if (Runs.Count == 0)
            {
                DetailText = string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load evaluation runs");
            StatusText = $"Error loading runs: {ex.Message}";
        }
    }

    private async Task LoadDetailAsync(ExperimentRun? run)
    {
        var version = ++_summaryVersion;
        if (run is null)
        {
            DetailText = Runs.Count == 0 ? string.Empty : "Select a run to view its summary.";
            return;
        }

        DetailText = "Loading summary…";
        try
        {
            var summary = await _evaluationService.GetRunSummaryAsync(run.Id, CancellationToken.None);
            if (version == _summaryVersion)
            {
                DetailText = FormatSummary(summary);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load summary for run {RunId}", run.Id);
            if (version == _summaryVersion)
            {
                DetailText = $"Error: {ex.Message}";
            }
        }
    }

    private static string FormatSummary(RunSummary summary)
    {
        return $"=== Run: {summary.Run.RunName} (chat: {summary.Run.ChatProvider}/{summary.Run.ChatModel}, judge: {summary.Run.JudgeProvider}/{summary.Run.JudgeModel}) ===\n" +
            $"Retrieval  - Precision: {summary.MeanPrecisionAtK:F3}  Recall: {summary.MeanRecallAtK:F3}  MRR: {summary.MeanReciprocalRank:F3}  NDCG: {summary.MeanNdcgAtK:F3}\n" +
            $"Generation - Faithfulness: {summary.MeanFaithfulness:F2}/5  Relevance: {summary.MeanRelevance:F2}/5  Completeness: {summary.MeanCompleteness:F2}/5";
    }
}
