using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Application.Services;
using KnowledgeAssistant.Domain.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RagEvaluation.Services;
using System.Windows;
using System.Windows.Controls;

namespace RagEvaluation.Desktop.Pages;

public partial class GenerateTestSetPage : Page
{
    private readonly TestSetGenerationService _generationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GenerateTestSetPage> _logger;
    private readonly IDocumentRepository _documentRepository;
    private static readonly Document AllDocumentsOption = new() { Id = 0, Title = "All Documents", OriginalText = "", Topics = [] };

    public GenerateTestSetPage(TestSetGenerationService generationService, IDocumentRepository documentRepository, IConfiguration configuration, ILogger<GenerateTestSetPage> logger)
    {
        _generationService = generationService;
        _documentRepository = documentRepository;
        _configuration = configuration;
        _logger = logger;

        InitializeComponent();
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
            _logger.LogError(ex, "Failed to load documents for GenerateTestSetPage");
            ResultText.Text = $"Error loading documents: {ex.Message}";
        }
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        GenerateButton.IsEnabled = false;
        ProgressRing.Visibility = Visibility.Visible;
        ProgressRing.Progress = 0;
        ProgressText.Text = string.Empty;
        ResultText.Text = string.Empty;

        var selectedDocument = DocumentSelector.SelectedItem as Document;
        int? documentId = selectedDocument is null || selectedDocument.Id == 0 ? null : selectedDocument.Id;

        try
        {
            var perChunk = (int)(QuestionsPerChunkBox.Value ?? 1);
            var chatModel = _configuration["Llm:ChatModel"]
                ?? throw new InvalidOperationException("Missing Llm:ChatModel configuration.");

            var progress = new Progress<(int done, int total)>(p =>
            {
                ProgressRing.Progress = p.total > 0 ? (double)p.done / p.total * 100 : 0;
                ProgressText.Text = $"Generating questions: {p.done}/{p.total} chunks processed";
            });

            var count = await _generationService.GenerateAsync(chatModel, perChunk, documentId, progress, CancellationToken.None);
            ResultText.Text = $"Saved {count} synthetic test queries (one row per chunk x topic).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test set generation failed");
            ResultText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            GenerateButton.IsEnabled = true;
            ProgressRing.Visibility = Visibility.Collapsed;
        }
    }
}
