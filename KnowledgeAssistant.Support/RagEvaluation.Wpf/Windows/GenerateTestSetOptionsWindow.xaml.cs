using RagEvaluation.Desktop.Services;
using RagEvaluation.Models;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace RagEvaluation.Desktop.Windows;

public partial class GenerateTestSetOptionsWindow : FluentWindow
{
    private readonly LlmCatalogService _llm;

    public GenerateTestSetOptionsWindow(
        LlmCatalogService llm,
        IReadOnlyList<DocumentChoice> documents,
        int? preselectDocumentId)
    {
        _llm = llm;
        InitializeComponent();

        DialogTitleBar.Title = Title;
        DocumentSelector.ItemsSource = documents;
        DocumentSelector.SelectedItem = documents.FirstOrDefault(d => d.Id == preselectDocumentId)
                                        ?? documents.FirstOrDefault();

        QuestionsPerChunkBox.Value = 2;
        ProviderSelector.ItemsSource = llm.Providers;
        ProviderSelector.SelectedItem = llm.Providers.FirstOrDefault();
    }

    public GenerateTestSetOptions? Options { get; private set; }

    private async void ProviderSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var provider = ProviderSelector.SelectedItem as string;
        ModelSelector.ItemsSource = null;
        if (string.IsNullOrWhiteSpace(provider))
        {
            return;
        }

        try
        {
            ErrorText.Text = string.Empty;
            var models = await _llm.GetModelsAsync(provider, CancellationToken.None);
            ModelSelector.ItemsSource = models;

            var configured = _llm.DefaultChatModel;
            ModelSelector.SelectedItem = configured is not null && models.Contains(configured)
                ? configured
                : models.FirstOrDefault();
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Could not load models for '{provider}': {ex.Message}";
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var provider = ProviderSelector.SelectedItem as string;
        var model = ModelSelector.SelectedItem as string;
        var document = DocumentSelector.SelectedItem as DocumentChoice;
        var perChunk = (int)(QuestionsPerChunkBox.Value ?? 0);

        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(model))
        {
            ErrorText.Text = "Select a provider and a model.";
            return;
        }

        if (document is null)
        {
            ErrorText.Text = "Select which documents to generate for.";
            return;
        }

        if (perChunk < 1)
        {
            ErrorText.Text = "Questions per chunk must be at least 1.";
            return;
        }

        Options = new GenerateTestSetOptions(provider, model, perChunk, document.Id, SkipExistingCheck.IsChecked == true);
        DialogResult = true;
    }
}