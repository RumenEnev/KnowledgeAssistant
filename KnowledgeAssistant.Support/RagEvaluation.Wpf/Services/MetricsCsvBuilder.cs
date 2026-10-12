using RagEvaluation.Models;
using System.Globalization;
using System.Text;

namespace RagEvaluation.Desktop.Services;

public static class MetricsCsvBuilder
{
    public static string BuildSummary(RunSummary summary)
    {
        var sb = new StringBuilder();
        AppendSummary(sb, summary);
        return sb.ToString();
    }

    public static string Build(RunSummary summary, IEnumerable<QueryMetricsRow> rows)
    {
        var sb = new StringBuilder();
        AppendSummary(sb, summary);
        sb.AppendLine();

        sb.AppendLine("Query,Precision,Recall,MRR,NDCG,Faithfulness,Relevance,Completeness");
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                Escape(row.QueryText),
                FormatNullable(row.PrecisionAtK, "F3"),
                FormatNullable(row.RecallAtK, "F3"),
                FormatNullable(row.ReciprocalRank, "F3"),
                FormatNullable(row.NdcgAtK, "F3"),
                FormatNullable(row.FaithfulnessScore, "F2"),
                FormatNullable(row.RelevanceScore, "F2"),
                FormatNullable(row.CompletenessScore, "F2")
            }));
        }

        return sb.ToString();
    }

    public static string Format(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static void AppendSummary(StringBuilder sb, RunSummary summary)
    {
        sb.AppendLine($"Run,{Escape(summary.Run.RunName)}");
        sb.AppendLine($"Chat Model,{Escape(summary.Run.ChatModel)}");
        sb.AppendLine($"Judge Model,{Escape(summary.Run.JudgeModel)}");
        sb.AppendLine($"Created,{summary.Run.CreatedAt:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        sb.AppendLine("Metric,Mean Value");
        sb.AppendLine($"Precision,{Format(summary.MeanPrecisionAtK, "F3")}");
        sb.AppendLine($"Recall,{Format(summary.MeanRecallAtK, "F3")}");
        sb.AppendLine($"MRR,{Format(summary.MeanReciprocalRank, "F3")}");
        sb.AppendLine($"NDCG,{Format(summary.MeanNdcgAtK, "F3")}");
        sb.AppendLine($"Faithfulness (1-5),{Format(summary.MeanFaithfulness, "F2")}");
        sb.AppendLine($"Relevance (1-5),{Format(summary.MeanRelevance, "F2")}");
        sb.AppendLine($"Completeness (1-5),{Format(summary.MeanCompleteness, "F2")}");
    }

    private static string FormatNullable(double? value, string format) => value.HasValue ? Format(value.Value, format) : "";

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}