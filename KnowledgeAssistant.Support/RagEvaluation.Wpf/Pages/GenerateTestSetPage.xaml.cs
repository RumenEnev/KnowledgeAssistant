using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RagEvaluation.Services;
using System.Windows;
using System.Windows.Controls;

namespace RagEvaluation.Desktop.Pages;

public partial class GenerateTestSetPage : Page
{
    private readonly TestSetGenerationService _generationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GenerateTestSetPage> _logger;

    public GenerateTestSetPage(TestSetGenerationService generationService, IConfiguration configuration, ILogger<GenerateTestSetPage> logger)
    {
        _generationService = generationService;
        _configuration = configuration;
        _logger = logger;

        InitializeComponent();
    }

    private async void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        GenerateButton.IsEnabled = false;
        ProgressRing.Visibility = Visibility.Visible;
        ProgressRing.Progress = 0;
        ProgressText.Text = string.Empty;
        ResultText.Text = string.Empty;

        try
        {
            var perChunk = (int)(QuestionsPerChunkBox.Value ?? 1);
            var chatModel = _configuration["Llm:ChatModel"]
                ?? throw new InvalidOperationException("Missing Llm:ChatModel configuration.");

            var progress = new Progress<(int done, int total)>(p =>
            {
                ProgressRing.Progress = p.total > 0 ? (double)p.done / p.total * 100 : 0;
                ProgressText.Text = $"Generating questions: {p.done}/{p.total} chunks processed";
            });

            var count = await _generationService.GenerateAsync(chatModel, perChunk, progress, CancellationToken.None);

            ResultText.Text = $"Saved {count} synthetic test queries (one row per chunk x topic).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test set generation failed");
            ResultText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            GenerateButton.IsEnabled = true;
            ProgressRing.Visibility = Visibility.Collapsed;
        }
    }
}
