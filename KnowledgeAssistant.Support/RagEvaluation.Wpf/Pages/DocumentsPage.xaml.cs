using RagEvaluation.Desktop.Enums;
using RagEvaluation.Desktop.ViewModels;
using RagEvaluation.Desktop.Windows;
using System.Windows;
using System.Windows.Controls;

namespace RagEvaluation.Desktop.Pages;

public partial class DocumentsPage : Page
{
    private readonly DocumentsViewModel _viewModel;

    public DocumentsPage(DocumentsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var topics = await _viewModel.LoadTopicsAsync();
        var dialog = new IngestOptionsWindow(
            IngestMode.Import,
            topics,
            _viewModel.EmbeddingModels.ToList(),
            _viewModel.SelectedEmbeddingModel,
            _viewModel.DefaultChunkSize,
            _viewModel.DefaultChunkOverlap,
            hasSelection: false,
            affectedSelected: 0,
            affectedAll: 0)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true || dialog.Options is null)
        {
            return;
        }

        await _viewModel.ImportAsync(dialog.Files, dialog.Options);
    }

    private async void RechunkButton_Click(object sender, RoutedEventArgs e)
    {
        var (affectedSelected, affectedAll) = await _viewModel.CountTestQuestionsAsync();
        var dialog = new IngestOptionsWindow(
            IngestMode.Rechunk,
            new List<Models.TopicSelectionItem>(),
            _viewModel.EmbeddingModels.ToList(),
            _viewModel.SelectedEmbeddingModel,
            _viewModel.DefaultChunkSize,
            _viewModel.DefaultChunkOverlap,
            hasSelection: _viewModel.SelectedDocument is not null,
            affectedSelected: affectedSelected,
            affectedAll: affectedAll)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true || dialog.Options is null)
        {
            return;
        }

        await _viewModel.RechunkAsync(dialog.Options);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedDocument is not { } document)
        {
            return;
        }

        var answer = MessageBox.Show(
            $"Delete '{document.Title}'? This removes its {document.ChunkCount} chunk(s), their vectors and the test questions generated from it. This cannot be undone.",
            "Delete document",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer == MessageBoxResult.Yes)
        {
            await _viewModel.DeleteSelectedDocumentAsync();
        }
    }

    private async void EditChunkButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedDocument is not { } document || _viewModel.SelectedChunk is not { } chunk)
        {
            return;
        }

        var window = new EditChunkWindow(document.Title, chunk.Index, chunk.Text)
        {
            Owner = Window.GetWindow(this)
        };

        if (window.ShowDialog() == true)
        {
            await _viewModel.UpdateChunkTextAsync(chunk, window.ChunkText);
        }
    }

    private async void DeleteChunkButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedDocument is not { } document || _viewModel.SelectedChunk is not { } chunk)
        {
            return;
        }

        var answer = MessageBox.Show(
            $"Delete chunk #{chunk.Index} of '{document.Title}'? This cannot be undone.",
            "Delete chunk",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer == MessageBoxResult.Yes)
        {
            await _viewModel.DeleteChunkAsync(chunk);
        }
    }
}