using RagEvaluation.Desktop.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace RagEvaluation.Desktop.Pages;

public partial class RunsPage : Page
{
    private readonly RunsViewModel _viewModel;

    public RunsPage(RunsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        InitializeComponent();

        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private async void CleanDatabaseButton_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            "This will permanently delete all evaluation runs, test queries, and results from the database. Continue?",
            "Clean database",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer == MessageBoxResult.Yes)
        {
            await _viewModel.CleanDatabaseAsync();
        }
    }
}
