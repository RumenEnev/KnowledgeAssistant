using System.Windows;
using Wpf.Ui.Controls;

namespace RagEvaluation.Desktop.Windows;

public partial class EditChunkWindow : FluentWindow
{
    public string ChunkText { get; private set; } = string.Empty;

    public EditChunkWindow(string documentTitle, int chunkIndex, string chunkText)
    {
        InitializeComponent();

        HeaderText.Text = $"{documentTitle} - chunk #{chunkIndex}";
        ChunkTextBox.Text = chunkText;
        ChunkText = chunkText;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ChunkText = ChunkTextBox.Text;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
