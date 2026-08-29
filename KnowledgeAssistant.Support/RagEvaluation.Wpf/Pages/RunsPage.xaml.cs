using Microsoft.Extensions.Logging;
using RagEvaluation.Models;
using RagEvaluation.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RagEvaluation.Desktop.Pages;

public partial class RunsPage : Page
{
    private readonly EvaluationService _evaluationService;
    private readonly ILogger<RunsPage> _logger;

    public RunsPage(EvaluationService evaluationService, ILogger<RunsPage> logger)
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

    private async void CleanDatabaseButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "This will permanently delete all evaluation runs, test queries, and results from the database. Continue?",
            "Clean Database",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        CleanDatabaseButton.IsEnabled = false;
        RefreshButton.IsEnabled = false;
        try
        {
            await _evaluationService.CleanDatabaseAsync(CancellationToken.None);
            RunsGrid.ItemsSource = null;
            SummaryText.Text = "Database cleaned.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean evaluation database");
            SummaryText.Text = $"Error cleaning database: {ex.Message}";
        }
        finally
        {
            CleanDatabaseButton.IsEnabled = true;
            RefreshButton.IsEnabled = true;
        }
    }

    private async Task LoadRunsAsync()
    {
        RefreshButton.IsEnabled = false;
        try
        {
            var runs = await _evaluationService.ListRunsAsync(CancellationToken.None);
            RunsGrid.ItemsSource = runs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load evaluation runs");
            SummaryText.Text = $"Error loading runs: {ex.Message}";
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    private async void RunsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RunsGrid.SelectedItem is not ExperimentRun run)
        {
            return;
        }

        SummaryText.Text = "Loading summary...";
        try
        {
            var summary = await _evaluationService.GetRunSummaryAsync(run.Id, CancellationToken.None);
            SummaryText.Text = FormatSummary(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load summary for run {RunId}", run.Id);
            SummaryText.Text = $"Error: {ex.Message}";
        }
    }

    private static string FormatSummary(RunSummary summary)
    {
        return $"=== Run: {summary.Run.RunName} (chat: {summary.Run.ChatModel}, judge: {summary.Run.JudgeModel}) ===\n" +
            $"Retrieval  - Precision: {summary.MeanPrecisionAtK:F3}  Recall: {summary.MeanRecallAtK:F3}  MRR: {summary.MeanReciprocalRank:F3}  NDCG: {summary.MeanNdcgAtK:F3}\n" +
            $"Generation - Faithfulness: {summary.MeanFaithfulness:F2}/5  Relevance: {summary.MeanRelevance:F2}/5  Completeness: {summary.MeanCompleteness:F2}/5";
    }
}
