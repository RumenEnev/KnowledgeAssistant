using Microsoft.Win32;
using RagEvaluation.Desktop.ViewModels;
using RagEvaluation.Desktop.Windows;
using RagEvaluation.Models;
using System.Windows;
using System.Windows.Controls;

namespace RagEvaluation.Desktop.Pages;

public partial class MetricsPage : Page
{
    private readonly MetricsViewModel _viewModel;

    public MetricsPage(MetricsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private async void QueryMetricsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (QueryMetricsGrid.SelectedItem is not QueryMetricsRow row)
        {
            return;
        }

        var detail = await _viewModel.LoadDetailAsync(row);
        if (detail is null)
        {
            return;
        }

        new GenerationDetailWindow(detail) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedRun is not { } run)
        {
            return;
        }

        var answer = MessageBox.Show(
            $"Delete run '{run.RunName}' and all of its retrieval and answer metrics? This cannot be undone.",
            "Delete run",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer == MessageBoxResult.Yes)
        {
            await _viewModel.DeleteSelectedRunAsync();
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e) => Export(summaryOnly: false);

    private void ExportSummaryButton_Click(object sender, RoutedEventArgs e) => Export(summaryOnly: true);

    private void Export(bool summaryOnly)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = _viewModel.SuggestedFileName(summaryOnly)
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.Export(dialog.FileName, summaryOnly);
        }
    }
}
