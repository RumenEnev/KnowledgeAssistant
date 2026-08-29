using Wpf.Ui.Controls;

namespace RagEvaluation.Desktop.Windows;

public partial class MainWindow : FluentWindow
{
    public MainWindow(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        RootNavigation.SetServiceProvider(serviceProvider);
        Loaded += (_, _) => RootNavigation.Navigate(typeof(Pages.GenerateTestSetPage));
    }
}
