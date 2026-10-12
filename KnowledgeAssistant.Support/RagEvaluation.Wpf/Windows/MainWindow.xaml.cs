using RagEvaluation.Desktop.ViewModels;
using System.Runtime.Versioning;
using System.Windows;
using Wpf.Ui.Controls;

namespace RagEvaluation.Desktop.Windows;

[SupportedOSPlatform("windows7.0")]
public partial class MainWindow : FluentWindow
{
    private readonly ShellViewModel _shell;

    public MainWindow(IServiceProvider serviceProvider, ShellViewModel shell)
    {
        _shell = shell;

        InitializeComponent();
        StatusStrip.DataContext = shell;
        RootNavigation.SetServiceProvider(serviceProvider);
        RootNavigation.Navigated += async (_, _) => await _shell.RefreshAsync();
        Loaded += (_, _) => RootNavigation.Navigate(typeof(Pages.ChunkDocumentPage));
    }

    private async void RefreshStatusButton_Click(object sender, RoutedEventArgs e)
    {
        await _shell.RefreshAsync();
    }
}