using RagEvaluation.Desktop.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace RagEvaluation.Desktop.Pages;

public partial class RunEvalPage : Page
{
    private readonly RunViewModel _viewModel;

    public RunEvalPage(RunViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        InitializeComponent();

        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.StartAsync(message =>
            MessageBox.Show(message, "Start run", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes);
    }
}
