using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Domain.Documents;
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
    private readonly IDocumentRepository _documentRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RunEvalPage> _logger;

    private static readonly Document AllDocumentsOption = new() { Id = 0, Title = "All Documents", OriginalText = "", Topics = [] };

    public RunEvalPage(EvaluationService evaluationService, IDocumentRepository documentRepository, IConfiguration configuration, ILogger<RunEvalPage> logger)
    {
        _evaluationService = evaluationService;
        _documentRepository = documentRepository;
        _configuration = configuration;
        _logger = logger;

        InitializeComponent();

        ChatModelBox.Text = _configuration["Llm:ChatModel"] ?? string.Empty;
        EmbeddingModelBox.Text = _configuration["Llm:EmbeddingModel"] ?? string.Empty;
        JudgeModelBox.Text = _configuration["Llm:JudgeModel"] ?? string.Empty;

        Loaded += async (_, _) => await LoadDocumentsAsync();
    }

    private async Task LoadDocumentsAsync()
    {
        try
        {
            var documents = await _documentRepository.GetAllDocumentsAsync(CancellationToken.None);
            var items = new List<Document> { AllDocumentsOption };
            items.AddRange(documents);
            DocumentSelector.ItemsSource = items;
            DocumentSelector.SelectedItem = AllDocumentsOption;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load documents for RunEvalPage");
            ResultText.Text = $"Error loading documents: {ex.Message}";
        }
    }

    private async void DocumentSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = DocumentSelector.SelectedItem as Document;

        if (selected is null || selected.Id == 0)
        {
            RetrievalSettingsCard.Visibility = Visibility.Collapsed;
            return;
        }

        RetrievalSettingsCard.Visibility = Visibility.Visible;
        await LoadRetrievalConfigAsync(selected.Id);
    }

    private async Task LoadRetrievalConfigAsync(int documentId)
    {
        RetrievalConfigStatusText.Text = "Loading...";
        try
        {
            var config = await _documentRepository.GetRetrievalConfigAsync(documentId, CancellationToken.None)
                ?? DocumentRetrievalConfig.Default(documentId);

            PoolSizeBox.Value = config.CandidatePoolSize;
            FanoutBox.Value = config.CandidateFanout;
            DistanceThresholdBox.Value = config.MaxDistanceThreshold;
            RrfKBox.Value = config.RrfK;
            TargetFractionBox.Value = config.TargetInjectionFraction;
            MaxFractionBox.Value = config.MaxInjectionFraction;

            RetrievalConfigStatusText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load retrieval config for document {DocumentId}", documentId);
            RetrievalConfigStatusText.Text = $"Error loading settings: {ex.Message}";
        }
    }

    private async void SaveRetrievalConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = DocumentSelector.SelectedItem as Document;
        if (selected is null || selected.Id == 0)
        {
            return;
        }

        var config = new DocumentRetrievalConfig
        {
            DocumentId = selected.Id,
            ChunkSize = (await _documentRepository.GetRetrievalConfigAsync(selected.Id, CancellationToken.None) ?? DocumentRetrievalConfig.Default(selected.Id)).ChunkSize,
            ChunkOverlap = (await _documentRepository.GetRetrievalConfigAsync(selected.Id, CancellationToken.None) ?? DocumentRetrievalConfig.Default(selected.Id)).ChunkOverlap,
            CandidatePoolSize = (int)(PoolSizeBox.Value ?? 5),
            CandidateFanout = (int)(FanoutBox.Value ?? 20),
            MaxDistanceThreshold = DistanceThresholdBox.Value ?? 0.5,
            RrfK = (int)(RrfKBox.Value ?? 60),
            TargetInjectionFraction = TargetFractionBox.Value ?? 0.30,
            MaxInjectionFraction = MaxFractionBox.Value ?? 0.50
        };

        SaveRetrievalConfigButton.IsEnabled = false;
        RetrievalConfigStatusText.Text = "Saving...";
        try
        {
            await _documentRepository.SaveRetrievalConfigAsync(config, CancellationToken.None);
            RetrievalConfigStatusText.Text = "Saved.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save retrieval config for document {DocumentId}", selected.Id);
            RetrievalConfigStatusText.Text = $"Error saving settings: {ex.Message}";
        }
        finally
        {
            SaveRetrievalConfigButton.IsEnabled = true;
        }
    }

    private async void ResetRetrievalConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = DocumentSelector.SelectedItem as Document;
        if (selected is null || selected.Id == 0)
        {
            return;
        }

        try
        {
            await _documentRepository.DeleteRetrievalConfigAsync(selected.Id, CancellationToken.None);
            await LoadRetrievalConfigAsync(selected.Id);
            RetrievalConfigStatusText.Text = "Reset to default.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset retrieval config for document {DocumentId}", selected.Id);
            RetrievalConfigStatusText.Text = $"Error resetting settings: {ex.Message}";
        }
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

        var selectedDocument = DocumentSelector.SelectedItem as Document;
        int? documentId = selectedDocument is null || selectedDocument.Id == 0 ? null : selectedDocument.Id;

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

            var outcome = await _evaluationService.RunEvalAsync(runName, chatModel, embeddingModel, judgeModel, documentId, progress, CancellationToken.None);

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