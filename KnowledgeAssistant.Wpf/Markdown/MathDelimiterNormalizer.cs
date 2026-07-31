using System.Text;
using System.Text.RegularExpressions;

namespace KnowledgeAssistant.Wpf.Markdown
{
    /// <summary>
    /// Many LLMs (including Ollama models) emit LaTeX math using the
    /// "\( ... \)" (inline) and "\[ ... \]" (block) delimiter conventions
    /// instead of the "$...$" / "$$...$$" delimiters that Markdig's
    /// Mathematics extension understands. This normalizer rewrites the
    /// former into the latter so the existing Markdig math pipeline can
    /// recognize and render the formulas.
    /// </summary>
    public static class MathDelimiterNormalizer
    {
        private static readonly Regex BlockDelimiterRegex = new(
            @"\\\[(.+?)\\\]",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex InlineDelimiterRegex = new(
            @"\\\((.+?)\\\)",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex CodeFenceSplitRegex = new(
            @"(```.*?```|`[^`\n]*`)",
            RegexOptions.Singleline | RegexOptions.Compiled);

        public static string Normalize(string? markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return markdown ?? string.Empty;
            }

            // Avoid touching content inside fenced/inline code blocks, where
            // backslash sequences should be left as literal text.
            var segments = CodeFenceSplitRegex.Split(markdown);
            var result = new StringBuilder(markdown.Length);

            foreach (var segment in segments)
            {
                if (segment.StartsWith("```", StringComparison.Ordinal) ||
                    (segment.StartsWith('`') && segment.EndsWith('`') && segment.Length > 1))
                {
                    result.Append(segment);
                    continue;
                }

                var converted = BlockDelimiterRegex.Replace(segment, match =>
                    "\n$$\n" + match.Groups[1].Value.Trim() + "\n$$\n");

                converted = InlineDelimiterRegex.Replace(converted, match =>
                    "$" + match.Groups[1].Value.Trim() + "$");

                result.Append(converted);
            }

            return result.ToString();
        }
    }
}
