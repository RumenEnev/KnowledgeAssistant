using RagEvaluation.Desktop.Services;
using RagEvaluation.Desktop.ViewModels;
using RagEvaluation.Desktop.Windows;
using RagEvaluation.Models;
using System.Windows;
using System.Windows.Controls;

namespace RagEvaluation.Desktop.Pages;

public partial class TestSetPage : Page
{
    private readonly TestSetViewModel _viewModel;
    private readonly LlmCatalogService _llm;

    public TestSetPage(TestSetViewModel viewModel, LlmCatalogService llm)
    {
        _viewModel = viewModel;
        _llm = llm;
        DataContext = viewModel;

        InitializeComponent();

        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private void QuestionsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.UpdateSelection(QuestionsGrid.SelectedItems.Cast<TestQuestionRow>().ToList());
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        var documents = await _viewModel.LoadDocumentChoicesAsync();
        var dialog = new GenerateTestSetOptionsWindow(_llm, documents, _viewModel.SelectedFilter?.Id)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true || dialog.Options is null)
        {
            return;
        }

        await _viewModel.GenerateAsync(dialog.Options);
    }

    private async void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        var count = _viewModel.SelectedCount;
        if (count == 0)
        {
            return;
        }

        var answer = MessageBox.Show(
            $"Delete {count} selected question(s)? Results of earlier runs for them are removed as well. This cannot be undone.",
            "Delete questions",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer == MessageBoxResult.Yes)
        {
            await _viewModel.DeleteSelectedAsync();
        }
    }

    private async void DeleteOrphansButton_Click(object sender, RoutedEventArgs e)
    {
        var count = _viewModel.OrphanCount;
        if (count == 0)
        {
            return;
        }

        var answer = MessageBox.Show(
            $"Delete {count} orphaned question(s)? Their expected chunk no longer exists, so they cannot be scored. Results of earlier runs for them are removed as well.",
            "Delete orphaned questions",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer == MessageBoxResult.Yes)
        {
            await _viewModel.DeleteOrphansAsync();
        }
    }
}