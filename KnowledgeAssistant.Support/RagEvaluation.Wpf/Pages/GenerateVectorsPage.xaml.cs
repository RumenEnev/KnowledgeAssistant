using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Application.Services;
using KnowledgeAssistant.Domain.Documents;
using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Controls;

namespace RagEvaluation.Desktop.Pages;

public partial class GenerateVectorsPage : Page
{
    private static readonly string[] EmbeddingModels =
    [
        "nomic-embed-text:latest",
        "mxbai-embed-large:latest"
    ];

    private readonly DocumentsHandlingService _documentsHandlingService;
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<GenerateVectorsPage> _logger;

    public GenerateVectorsPage(
        DocumentsHandlingService documentsHandlingService,
        IDocumentRepository documentRepository,
        ILogger<GenerateVectorsPage> logger)
    {
        _documentsHandlingService = documentsHandlingService;
        _documentRepository = documentRepository;
        _logger = logger;

        InitializeComponent();
        ModelSelector.ItemsSource = EmbeddingModels;
        ModelSelector.SelectedItem = EmbeddingModels.FirstOrDefault();

        Loaded += async (_, _) => await LoadDocumentsAsync();
    }

    private async Task LoadDocumentsAsync()
    {
        try
        {
            var documents = await _documentRepository.GetAllDocumentsAsync(CancellationToken.None);
            var items = documents.OrderBy(d => d.Title).ToList();
            DocumentSelector.ItemsSource = items;
            DocumentSelector.SelectedItem = items.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load documents for GenerateVectorsPage");
            ResultText.Text = $"Error loading documents: {ex.Message}";
        }
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        var document = DocumentSelector.SelectedItem as Document;
        var model = ModelSelector.SelectedItem as string;
        if (document is null)
        {
            ResultText.Text = "Select a document before generating.";
            return;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            ResultText.Text = "Select an embedding model before generating.";
            return;
        }

        GenerateButton.IsEnabled = false;
        ProgressRing.Visibility = Visibility.Visible;
        ProgressRing.Progress = 0;
        ProgressText.Text = string.Empty;
        ResultText.Text = string.Empty;
        try
        {
            var progress = new Progress<(int Done, int Total)>(p =>
            {
                ProgressRing.Progress = p.Total > 0 ? (double)p.Done / p.Total * 100 : 0;
                ProgressText.Text = $"Generating vectors: {p.Done}/{p.Total} chunks processed";
            });

            var count = await _documentsHandlingService.GenerateChunkVectorsAsync(document.Id, model, progress, CancellationToken.None);
            ResultText.Text = count == 0
                ? $"Document '{document.Title}' has no chunks yet - nothing to embed."
                : $"Generated/updated vectors for {count} chunk(s) of '{document.Title}' using {model}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vector generation failed for document {DocumentId}", document.Id);
            ResultText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            GenerateButton.IsEnabled = true;
            ProgressRing.Visibility = Visibility.Collapsed;
        }
    }
}