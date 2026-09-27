using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Application.Services;
using KnowledgeAssistant.Contracts.Enums;
using Microsoft.Extensions.Logging;
using RagEvaluation.Desktop.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace RagEvaluation.Desktop.Pages;

public partial class ChunkDocumentPage : Page
{
    private static readonly string[] FallbackEmbeddingModels =
    [
        "nomic-embed-text:latest",
        "mxbai-embed-large:latest"
    ];

    private readonly DocumentsHandlingService _documentsHandlingService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IConfigurationRepository _configurationRepository;
    private readonly IModelRepository _modelRepository;
    private readonly ILogger<ChunkDocumentPage> _logger;

    private readonly ObservableCollection<TopicSelectionItem> _topics = new();
    private readonly ObservableCollection<DocumentListItem> _documents = new();

    public ChunkDocumentPage(
        DocumentsHandlingService documentsHandlingService,
        IDocumentRepository documentRepository,
        IConfigurationRepository configurationRepository,
        IModelRepository modelRepository,
        ILogger<ChunkDocumentPage> logger)
    {
        _documentsHandlingService = documentsHandlingService;
        _documentRepository = documentRepository;
        _configurationRepository = configurationRepository;
        _modelRepository = modelRepository;
        _logger = logger;

        InitializeComponent();

        TopicsList.ItemsSource = _topics;
        DocumentsGrid.ItemsSource = _documents;

        DocumentTypeSelector.ItemsSource = Enum.GetValues<DocumentType>();
        DocumentTypeSelector.SelectedItem = DocumentType.PlainText;

        ModelSelector.ItemsSource = FallbackEmbeddingModels;
        ModelSelector.SelectedItem = FallbackEmbeddingModels.FirstOrDefault();

        Loaded += async (_, _) =>
        {
            await LoadTopicsAsync();
            await LoadDocumentsAsync();
            await LoadChunkingSettingsAsync();
        };
    }

    private async Task LoadTopicsAsync()
    {
        try
        {
            var topics = await _documentRepository.GetAllTopicsAsync(CancellationToken.None);
            _topics.Clear();
            foreach (var topic in topics.OrderBy(t => t.Name))
            {
                _topics.Add(new TopicSelectionItem { Id = topic.Id, Name = topic.Name, ParentId = topic.ParentId });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load topics for ChunkDocumentPage");
            ResultText.Text = $"Error loading topics: {ex.Message}";
        }
    }

    private async Task LoadChunkingSettingsAsync()
    {
        try
        {
            var embeddingModels = await _modelRepository.GetEmbeddingModelsAsync(CancellationToken.None);
            var models = embeddingModels.Count > 0 ? embeddingModels.ToArray() : FallbackEmbeddingModels;
            ModelSelector.ItemsSource = models;

            var settings = await _configurationRepository.GetChunkingSettingsAsync(CancellationToken.None);
            ModelSelector.SelectedItem = models.Contains(settings.EmbeddingModelName) ? settings.EmbeddingModelName : models.FirstOrDefault();
            ChunkSizeBox.Value = settings.ChunkTargetSizeChars > 0 ? settings.ChunkTargetSizeChars : 1200;
            ChunkOverlapBox.Value = settings.ChunkOverlapChars >= 0 ? settings.ChunkOverlapChars : 200;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load chunking settings for ChunkDocumentPage");
            ResultText.Text = $"Error loading chunking settings: {ex.Message}";
        }
    }

    private async Task LoadDocumentsAsync()
    {
        try
        {
            var documents = await _documentRepository.GetAllDocumentsAsync(CancellationToken.None);
            _documents.Clear();
            foreach (var document in documents.OrderByDescending(d => d.CreatedAt))
            {
                _documents.Add(new DocumentListItem
                {
                    Id = document.Id,
                    Title = document.Title,
                    TopicsDisplay = string.Join(", ", document.Topics),
                    CreatedAt = document.CreatedAt
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load documents for ChunkDocumentPage");
            ResultText.Text = $"Error loading documents: {ex.Message}";
        }
    }

    private async void RefreshDocumentsButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadDocumentsAsync();
    }

    private async void DocumentsGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DocumentsGrid.SelectedItem is not DocumentListItem)
        {
            ChunkingParamsText.Text = "Select a document to view the chunking settings that apply to it.";
            return;
        }

        try
        {
            var settings = await _configurationRepository.GetChunkingSettingsAsync(CancellationToken.None);
            ChunkingParamsText.Text = $"Chunk size: {settings.ChunkTargetSizeChars} chars | Overlap: {settings.ChunkOverlapChars} chars | " +
                $"Embedding model: {(string.IsNullOrWhiteSpace(settings.EmbeddingModelName) ? "(none)" : settings.EmbeddingModelName)}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load chunking settings for selected document");
            ChunkingParamsText.Text = $"Error loading chunking settings: {ex.Message}";
        }
    }

    private async void DeleteDocumentButton_Click(object sender, RoutedEventArgs e)
    {
        if (DocumentsGrid.SelectedItem is not DocumentListItem document)
        {
            ResultText.Text = "Select a document to delete first.";
            return;
        }

        var result = MessageBox.Show(
            $"Delete document '{document.Title}' (id {document.Id})? This also removes its chunks. This cannot be undone.",
            "Delete Document",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _documentRepository.DeleteDocumentAsync(document.Id, CancellationToken.None);
            ResultText.Text = $"Document '{document.Title}' deleted.";
            await LoadDocumentsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document {DocumentId}", document.Id);
            ResultText.Text = $"Error deleting document: {ex.Message}";
        }
    }

    private void LoadFromFileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Text files (*.txt), Markdown files (*.md)|*.txt;*.md|All files (*.*)|*.*",
            Title = "Select a .txt or .md file"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            TextBox.Text = File.ReadAllText(dialog.FileName);
            TitleBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
            DocumentTypeSelector.SelectedItem = Path.GetExtension(dialog.FileName).Equals(".md", StringComparison.OrdinalIgnoreCase)
                ? DocumentType.Markdown
                : DocumentType.PlainText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read file {FileName}", dialog.FileName);
            ResultText.Text = $"Failed to read the file: {ex.Message}";
        }
    }

    private async void ChunkButton_Click(object sender, RoutedEventArgs e)
    {
        var title = TitleBox.Text?.Trim();
        var text = TextBox.Text?.Trim();
        var topicNames = _topics.Where(t => t.IsSelected).Select(t => t.Name).ToList();
        var documentType = DocumentTypeSelector.SelectedItem as DocumentType? ?? DocumentType.PlainText;
        var embeddingModel = ModelSelector.SelectedItem as string;
        var chunkSize = (int)(ChunkSizeBox.Value ?? 0);
        var chunkOverlap = (int)(ChunkOverlapBox.Value ?? 0);

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(text) || topicNames.Count == 0)
        {
            ResultText.Text = "Title, text and at least one topic are required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(embeddingModel))
        {
            ResultText.Text = "Select an embedding model before chunking.";
            return;
        }

        if (chunkSize <= 0 || chunkOverlap < 0 || chunkOverlap >= chunkSize)
        {
            ResultText.Text = "Chunk overlap must be zero or greater, and smaller than chunk size.";
            return;
        }

        ChunkButton.IsEnabled = false;
        ProgressRing.Visibility = Visibility.Visible;
        ResultText.Text = string.Empty;
        try
        {
            var modelId = await _modelRepository.GetOrCreateModelIdAsync(embeddingModel, CancellationToken.None);
            await _configurationRepository.UpsertChunkingSettingsAsync(modelId, chunkSize, chunkOverlap, CancellationToken.None);

            var (documentId, chunksCount) = await _documentsHandlingService.IngestDocumentAsync(
                title, text, embeddingModel, documentType, topicNames, CancellationToken.None);

            ResultText.Text = $"Document '{title}' (id {documentId}) chunked successfully. Chunks count: {chunksCount}.";
            await LoadDocumentsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to chunk document {Title}", title);
            ResultText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            ChunkButton.IsEnabled = true;
            ProgressRing.Visibility = Visibility.Collapsed;
        }
    }
}