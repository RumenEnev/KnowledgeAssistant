using RagEvaluation.Models;
using System.Windows;

namespace RagEvaluation.Desktop.Windows;

public partial class GenerationDetailWindow : Window
{
    public GenerationDetailWindow(QueryGenerationDetail detail)
    {
        InitializeComponent();

        QueryTextBlock.Text = detail.QueryText;

        ScoresTextBlock.Text = detail.FaithfulnessScore is null
            ? "No judge scores recorded for this query."
            : $"Faithfulness: {detail.FaithfulnessScore:F1}/5   " +
              $"Relevance: {detail.RelevanceScore:F1}/5   " +
              $"Completeness: {detail.CompletenessScore:F1}/5   " +
              $"(judged by {detail.JudgeModel})";

        AnswerTextBlock.Text = detail.GeneratedAnswer;
        ChunksItemsControl.ItemsSource = detail.ContextChunks;
        RationaleTextBlock.Text = string.IsNullOrWhiteSpace(detail.JudgeRationale)
            ? "(no rationale recorded)"
            : detail.JudgeRationale;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}