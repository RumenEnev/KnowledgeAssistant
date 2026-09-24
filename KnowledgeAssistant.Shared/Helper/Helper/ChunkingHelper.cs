using System.Text;
using System.Text.RegularExpressions;

namespace Helper;

public static class ChunkingHelper
{
    public static List<string> ChunkMarkdownByHeaders(string markdown, int targetChunkSizeChars, int overlapChars)
    {
        var sections = SplitByHeaders(markdown);
        var allChunks = new List<string>();
        foreach (var section in sections)
        {
            var sectionChunks = ChunkSectionByParagraphs(section, targetChunkSizeChars, overlapChars);
            allChunks.AddRange(sectionChunks);
        }

        return MarkdownTextStripper.StripAll(allChunks);
    }

    public static List<string> ChunkByParagraphWithOverlap(string text, int targetChunkSizeChars, int overlapChars)
    {
        var paragraphs = text
            .Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        var chunks = new List<string>();
        var currentChunk = new StringBuilder();
        foreach (var paragraph in paragraphs)
        {
            if (currentChunk.Length > 0 && currentChunk.Length + paragraph.Length > targetChunkSizeChars)
            {
                string finishedChunk = currentChunk.ToString();
                chunks.Add(finishedChunk.Trim());

                string overlapText = finishedChunk.Length > overlapChars
                    ? finishedChunk[^overlapChars..]
                    : finishedChunk;

                currentChunk.Clear();
                currentChunk.Append(overlapText).Append("\n\n");
            }

            currentChunk.Append(paragraph).Append("\n\n");
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        return chunks;
    }

    private static List<string> SplitByHeaders(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var sections = new List<string>();
        var currentContent = new StringBuilder();
        var headerRegex = new Regex(@"^(#{1,6})\s+(.*)$");
        foreach (var line in lines)
        {
            if (headerRegex.IsMatch(line) && (currentContent.Length > 0))
            {
                sections.Add(currentContent.ToString().Trim());
                currentContent.Clear();
            }

            currentContent.AppendLine(line);
        }

        if (currentContent.Length > 0)
        {
            sections.Add(currentContent.ToString().Trim());
            currentContent.Clear();
        }

        return sections;
    }

    private static List<string> ChunkSectionByParagraphs(string text, int targetChunkSizeChars, int overlapChars)
    {
        var paragraphs = SplitIntoParagraphs(text);
        var chunks = new List<string>();
        var current = new List<string>();
        int currentLength = 0;
        foreach (var paragraph in paragraphs)
        {
            if (paragraph.Length > targetChunkSizeChars)
            {
                if (current.Count > 0)
                {
                    chunks.Add(string.Join("\n\n", current));
                    current = CarryOverlap(current, overlapChars);
                    currentLength = current.Sum(p => p.Length);
                }

                foreach (var piece in SplitOversizedParagraph(paragraph, targetChunkSizeChars))
                {
                    chunks.Add(piece);
                }

                continue;
            }

            if (currentLength > 0 && currentLength + paragraph.Length > targetChunkSizeChars)
            {
                chunks.Add(string.Join("\n\n", current));
                current = CarryOverlap(current, overlapChars);
                currentLength = current.Sum(p => p.Length);
            }

            current.Add(paragraph);
            currentLength += paragraph.Length;
        }

        if (current.Count > 0)
        {
            chunks.Add(string.Join("\n\n", current));
        }

        return chunks;
    }

    private static List<string> SplitIntoParagraphs(string text)
    {
        var blocks = new List<string>();
        var lines = text.Split('\n');
        var buffer = new StringBuilder();
        bool inFence = false;
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```"))
            {
                inFence = !inFence;
            }

            if (!inFence && string.IsNullOrWhiteSpace(line))
            {
                if (buffer.Length > 0)
                {
                    blocks.Add(buffer.ToString().Trim());
                    buffer.Clear();
                }
            }
            else
            {
                buffer.AppendLine(line);
            }
        }

        if (buffer.Length > 0)
        {
            blocks.Add(buffer.ToString().Trim());
        }

        return blocks.Where(b => b.Length > 0).ToList();
    }

    private static List<string> CarryOverlap(List<string> paragraphs, int overlapChars)
    {
        var carried = new List<string>();
        int total = 0;

        for (int i = paragraphs.Count - 1; i >= 0; i--)
        {
            if (total >= overlapChars) break;
            carried.Insert(0, paragraphs[i]);
            total += paragraphs[i].Length;
        }

        return carried;
    }

    private static List<string> SplitOversizedParagraph(string paragraph, int targetChunkSizeChars)
    {
        var sentences = Regex.Split(paragraph, @"(?<=[.!?])\s+(?![^$]*\$)");
        var pieces = new List<string>();
        var buffer = new StringBuilder();
        foreach (var sentence in sentences)
        {
            if (buffer.Length > 0 && buffer.Length + sentence.Length > targetChunkSizeChars)
            {
                pieces.Add(buffer.ToString().Trim());
                buffer.Clear();
            }

            buffer.Append(sentence).Append(' ');
        }

        if (buffer.Length > 0)
        {
            pieces.Add(buffer.ToString().Trim());
        }

        return pieces;
    }
}