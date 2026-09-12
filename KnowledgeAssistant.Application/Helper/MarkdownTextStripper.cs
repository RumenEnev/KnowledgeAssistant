using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Text;
using System.Text.RegularExpressions;

namespace KnowledgeAssistant.Application.Helper;

public static class MarkdownTextStripper
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
                                                    .UseAdvancedExtensions()
                                                    .Build();

    public static string Strip(string chunkText)
    {
        if (string.IsNullOrWhiteSpace(chunkText))
        {
            return chunkText ?? string.Empty;
        }

        var document = Markdown.Parse(chunkText, Pipeline);
        var sb = new StringBuilder();
        AppendPlainText(document, sb);
        return NormalizeWhitespace(sb.ToString());
    }

    public static List<string> StripAll(IEnumerable<string> chunks) => chunks.Select(Strip).ToList();

    private static void AppendPlainText(MarkdownObject node, StringBuilder sb)
    {
        switch (node)
        {
            case LiteralInline literal:
                sb.Append(literal.Content.ToString());
                break;

            case CodeInline code:
                sb.Append(code.Content);
                break;

            case LineBreakInline:
                sb.Append('\n');
                break;

            case FencedCodeBlock fenced:
                foreach (var line in fenced.Lines.Lines)
                {
                    sb.AppendLine(line.Slice.ToString());
                }
                break;

            case CodeBlock codeBlock when node is not FencedCodeBlock:
                foreach (var line in codeBlock.Lines.Lines)
                {
                    sb.AppendLine(line.Slice.ToString());
                }
                break;

            case ThematicBreakBlock:
                sb.Append('\n');
                break;

            case LinkInline link:
                foreach (var child in link)
                {
                    AppendPlainText(child, sb);
                }
                break;

            case HeadingBlock heading:
                if (heading.Inline != null)
                {
                    foreach (var child in heading.Inline)
                    {
                        AppendPlainText(child, sb);
                    }
                }

                sb.Append('\n');
                break;

            case ParagraphBlock paragraph:
                if (paragraph.Inline != null)
                {
                    foreach (var child in paragraph.Inline)
                    {
                        AppendPlainText(child, sb);
                    }
                }

                sb.Append('\n');
                break;

            case ListItemBlock listItem:
                foreach (var child in listItem)
                {
                    AppendPlainText(child, sb);
                }
                break;

            case QuoteBlock quote:
                foreach (var child in quote)
                {
                    AppendPlainText(child, sb);
                }
                break;

            case ContainerBlock container:
                foreach (var child in container)
                {
                    AppendPlainText(child, sb);
                }
                break;

            case ContainerInline containerInline:
                foreach (var child in containerInline)
                {
                    AppendPlainText(child, sb);
                }
                break;

            default:
                if (node is LeafBlock leaf && leaf.Inline != null)
                {
                    foreach (var child in leaf.Inline)
                    {
                        AppendPlainText(child, sb);
                    }
                }
                break;
        }
    }

    private static string NormalizeWhitespace(string text)
    {
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }
}