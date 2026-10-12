using KnowledgeAssistant.Contracts.Enums;
using Microsoft.Win32;
using RagEvaluation.Desktop.Enums;
using RagEvaluation.Desktop.Models;
using RagEvaluation.Models;
using System.IO;
using System.Windows;
using Wpf.Ui.Controls;

namespace RagEvaluation.Desktop.Windows;

public partial class IngestOptionsWindow : FluentWindow
{
    private readonly IngestMode _mode;
    private readonly IReadOnlyList<TopicSelectionItem> _topics;
    private readonly int _affectedSelected;
    private readonly int _affectedAll;
    private List<string> _files = new();

    public IngestOptionsWindow(
        IngestMode mode,
        IReadOnlyList<TopicSelectionItem> topics,
        IReadOnlyList<string> models,
        string? selectedModel,
        int chunkSize,
        int chunkOverlap,
        bool hasSelection,
        int affectedSelected,
        int affectedAll)
    {
        _mode = mode;
        _topics = topics;
        _affectedSelected = affectedSelected;
        _affectedAll = affectedAll;
        InitializeComponent();

        var isImport = mode == IngestMode.Import;
        Title = isImport ? "Import documents" : "Re-chunk documents";
        DialogTitleBar.Title = Title;
        HeadingText.Text = Title;
        ImportSection.Visibility = isImport ? Visibility.Visible : Visibility.Collapsed;
        RechunkSection.Visibility = isImport ? Visibility.Collapsed : Visibility.Visible;
        WarningCard.Visibility = isImport ? Visibility.Collapsed : Visibility.Visible;

        TopicsList.ItemsSource = topics;

        ModelSelector.ItemsSource = models;
        ModelSelector.SelectedItem = selectedModel is not null && models.Contains(selectedModel)
            ? selectedModel
            : models.FirstOrDefault();

        TypeSelector.ItemsSource = Enum.GetValues<DocumentType>();
        TypeSelector.SelectedItem = DocumentType.Markdown;

        ChunkSizeBox.Value = chunkSize;
        ChunkOverlapBox.Value = chunkOverlap;

        ScopeSelectedRadio.IsEnabled = hasSelection;
        if (hasSelection)
        {
            ScopeSelectedRadio.IsChecked = true;
        }
        else
        {
            ScopeAllRadio.IsChecked = true;
        }

        UpdateWarning();
    }

    public IReadOnlyList<string> Files => _files;

    public IngestOptions? Options { get; private set; }

    private void ChooseFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Select markdown or text files",
            Filter = "Markdown and text files|*.md;*.txt|All files|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _files = dialog.FileNames.ToList();
        FilesText.Text = _files.Count == 1
            ? Path.GetFileName(_files[0])
            : $"{_files.Count} files selected";
    }

    private void Scope_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
        {
            UpdateWarning();
        }
    }

    private void UpdateWarning()
    {
        if (_mode != IngestMode.Rechunk)
        {
            return;
        }

        var all = ScopeAllRadio.IsChecked == true;
        var count = all ? _affectedAll : _affectedSelected;
        var questions = count < 0 ? "any existing" : count.ToString();
        WarningText.Text =
            "Re-chunking replaces the chunks of the chosen documents.\n" +
            "• Vectors made with other embedding models are removed - regenerate them afterwards.\n" +
            $"• Chunk IDs change, so {questions} test question(s) for these documents will no longer match - regenerate them on the Test set page.";
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var model = ModelSelector.SelectedItem as string;
        var size = (int)(ChunkSizeBox.Value ?? 0);
        var overlap = (int)(ChunkOverlapBox.Value ?? 0);
        var topicNames = _topics.Where(t => t.IsSelected).Select(t => t.Name).ToList();

        if (_mode == IngestMode.Import && _files.Count == 0)
        {
            ShowError("Choose at least one file.");
            return;
        }

        if (_mode == IngestMode.Import && topicNames.Count == 0)
        {
            ShowError("Select at least one topic.");
            return;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            ShowError("Select an embedding model.");
            return;
        }

        if (size <= 0 || overlap < 0 || overlap >= size)
        {
            ShowError("Chunk overlap must be zero or greater, and smaller than the chunk size.");
            return;
        }

        var type = TypeSelector.SelectedItem as DocumentType? ?? DocumentType.Markdown;
        Options = new IngestOptions(
            model,
            size,
            overlap,
            type,
            topicNames,
            AllDocuments: _mode == IngestMode.Rechunk && ScopeAllRadio.IsChecked == true);

        DialogResult = true;
    }

    private void ShowError(string message) => ErrorText.Text = message;
}