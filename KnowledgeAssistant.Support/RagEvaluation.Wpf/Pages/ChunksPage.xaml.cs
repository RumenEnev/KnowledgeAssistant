using KnowledgeAssistant.Domain.Documents;
using Microsoft.Extensions.Logging;
using RagEvaluation.Desktop.Windows;
using RagEvaluation.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RagEvaluation.Desktop.Pages;

public partial class ChunksPage : Page
{
    private const int PageSize = 50;

    private readonly EvaluationService _evaluationService;
    private readonly ILogger<ChunksPage> _logger;

    private int _currentPage = 1;
    private int _totalCount;
    private string? _searchText;

    public ChunksPage(EvaluationService evaluationService, ILogger<ChunksPage> logger)
    {
        _evaluationService = evaluationService;
        _logger = logger;

        InitializeComponent();

        Loaded += async (_, _) => await LoadChunksAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadChunksAsync();
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        _searchText = string.IsNullOrWhiteSpace(SearchBox.Text) ? null : SearchBox.Text.Trim();
        _currentPage = 1;
        await LoadChunksAsync();
    }

    private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _searchText = string.IsNullOrWhiteSpace(SearchBox.Text) ? null : SearchBox.Text.Trim();
            _currentPage = 1;
            await LoadChunksAsync();
        }
    }

    private async void PrevPageButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 1)
        {
            _currentPage--;
            await LoadChunksAsync();
        }
    }

    private async void NextPageButton_Click(object sender, RoutedEventArgs e)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(_totalCount / (double)PageSize));
        if (_currentPage < totalPages)
        {
            _currentPage++;
            await LoadChunksAsync();
        }
    }

    private async void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (ChunksGrid.SelectedItem is not ChunkListItem chunk)
        {
            StatusText.Text = "Select a chunk to edit first.";
            return;
        }

        var editWindow = new EditChunkWindow(chunk.DocumentTitle, chunk.ChunkIndex, chunk.ChunkText)
        {
            Owner = Window.GetWindow(this)
        };

        if (editWindow.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await _evaluationService.UpdateChunkTextAsync(chunk.Id, editWindow.ChunkText, CancellationToken.None);
            StatusText.Text = $"Chunk {chunk.Id} updated.";
            await LoadChunksAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update chunk {ChunkId}", chunk.Id);
            StatusText.Text = $"Error updating chunk: {ex.Message}";
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (ChunksGrid.SelectedItem is not ChunkListItem chunk)
        {
            StatusText.Text = "Select a chunk to delete first.";
            return;
        }

        var result = MessageBox.Show(
            $"Delete chunk #{chunk.ChunkIndex} of document '{chunk.DocumentTitle}' (id {chunk.Id})? This cannot be undone.",
            "Delete Chunk",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _evaluationService.DeleteChunkAsync(chunk.Id, CancellationToken.None);
            StatusText.Text = $"Chunk {chunk.Id} deleted.";
            await LoadChunksAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete chunk {ChunkId}", chunk.Id);
            StatusText.Text = $"Error deleting chunk: {ex.Message}";
        }
    }

    private async Task LoadChunksAsync()
    {
        RefreshButton.IsEnabled = false;
        try
        {
            var (chunks, totalCount) = await _evaluationService.GetAllChunksAsync(_currentPage, PageSize, _searchText, CancellationToken.None);
            _totalCount = totalCount;
            ChunksGrid.ItemsSource = chunks;

            var totalPages = Math.Max(1, (int)Math.Ceiling(_totalCount / (double)PageSize));
            PageText.Text = $"Page {_currentPage} of {totalPages} ({_totalCount} chunks)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load chunks");
            StatusText.Text = $"Error loading chunks: {ex.Message}";
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }
}