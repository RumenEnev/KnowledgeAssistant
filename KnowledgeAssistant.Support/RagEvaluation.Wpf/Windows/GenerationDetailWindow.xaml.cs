using RagEvaluation.Models;
using System;
using System.Text.Json;
using System.Windows;

namespace RagEvaluation.Desktop.Windows;

public partial class GenerationDetailWindow : Window
{
    private readonly QueryGenerationDetail _detail;

    public GenerationDetailWindow(QueryGenerationDetail detail)
    {
        InitializeComponent();

        _detail = detail;

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

    private void CopyToClipboardButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var payload = new
            {
                Query = _detail.QueryText,
                Scores = _detail.FaithfulnessScore is null
                    ? null
                    : new
                    {
                        _detail.FaithfulnessScore,
                        _detail.RelevanceScore,
                        _detail.CompletenessScore,
                        JudgeModel = _detail.JudgeModel
                    },
                GeneratedAnswer = _detail.GeneratedAnswer,
                ContextChunks = _detail.ContextChunks,
                JudgeRationale = string.IsNullOrWhiteSpace(_detail.JudgeRationale)
                    ? null
                    : _detail.JudgeRationale
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            Clipboard.SetText(json);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to copy to clipboard: {ex.Message}",
                "Copy to Clipboard",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}