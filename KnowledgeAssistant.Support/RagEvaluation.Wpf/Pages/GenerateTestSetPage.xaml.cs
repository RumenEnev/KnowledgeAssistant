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
    private readonly IModelProviderRegistry _providerRegistry;
    private readonly IModelGatewayResolver _gatewayResolver;
    private static readonly Document AllDocumentsOption = new() { Id = 0, Title = "All Documents", OriginalText = "", Topics = [] };

    public GenerateTestSetPage(
        TestSetGenerationService generationService,
        IDocumentRepository documentRepository,
        IModelProviderRegistry providerRegistry,
        IModelGatewayResolver gatewayResolver,
        IConfiguration configuration,
        ILogger<GenerateTestSetPage> logger)
    {
        _generationService = generationService;
        _documentRepository = documentRepository;
        _providerRegistry = providerRegistry;
        _gatewayResolver = gatewayResolver;
        _configuration = configuration;
        _logger = logger;

        InitializeComponent();
        ProviderSelector.ItemsSource = _providerRegistry.Providers;
        ProviderSelector.SelectedItem = _providerRegistry.Providers.FirstOrDefault();

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

    private async void ProviderSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var provider = ProviderSelector.SelectedItem as string;
        ModelSelector.ItemsSource = null;
        if (string.IsNullOrWhiteSpace(provider) || !_providerRegistry.TryGetCatalogGateway(provider, out var catalogGateway))
        {
            return;
        }

        try
        {
            var models = await catalogGateway.GetModelsAsync(CancellationToken.None);
            var modelNames = models.Select(m => m.Name).ToList();
            ModelSelector.ItemsSource = modelNames;

            var configuredModel = _configuration["Llm:ChatModel"];
            ModelSelector.SelectedItem = configuredModel is not null && modelNames.Contains(configuredModel)
                ? configuredModel
                : modelNames.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load models for provider {Provider}", provider);
            ResultText.Text = $"Error loading models for '{provider}': {ex.Message}";
        }
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        var provider = ProviderSelector.SelectedItem as string;
        var model = ModelSelector.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(model))
        {
            ResultText.Text = "Select a provider and a model before generating.";
            return;
        }

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
            var gateway = _gatewayResolver.GetRequiredGateway(provider);

            var progress = new Progress<(int done, int total)>(p =>
            {
                ProgressRing.Progress = p.total > 0 ? (double)p.done / p.total * 100 : 0;
                ProgressText.Text = $"Generating questions: {p.done}/{p.total} chunks processed";
            });

            var count = await _generationService.GenerateAsync(gateway, provider, model, perChunk, documentId, progress, CancellationToken.None);
            ResultText.Text = $"Saved {count} synthetic test queries (one row per chunk x topic) using {provider} / {model}.";
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
