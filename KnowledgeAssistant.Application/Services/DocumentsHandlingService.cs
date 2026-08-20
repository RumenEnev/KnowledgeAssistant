using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Contracts.Enums;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace KnowledgeAssistant.Application.Services;

public class DocumentsHandlingService
{
    private readonly IModelGateway _modelGateway;
    private readonly IDocumentRepository _documentRepository;
    private readonly IConfigurationRepository _configurationRepository;
    private readonly IModelRepository _modelRepository;
    private static readonly ConcurrentDictionary<string, int> _contextWindowCache = new();
    private readonly int _fallbackContextWindowTokens;
    private readonly ModelCatalogService _modelCatalogService;

    private const string EmbeddingModel = "nomic-embed-text";
    private const int CandidatePoolSize = 10;
    private const double TargetInjectionFraction = 0.30;
    private const double MaxInjectionFraction = 0.50;
    private const int CharsPerTokenApprox = 4;

    public DocumentsHandlingService(IModelGateway modelGateway, 
        IDocumentRepository documentRepository,
        IModelRepository modelRepository, 
        IConfigurationRepository configurationRepository,
        ModelCatalogService modelCatalogService)
    {
        _modelGateway = modelGateway;
        _documentRepository = documentRepository;
        _modelRepository = modelRepository;
        _configurationRepository = configurationRepository;
        _modelCatalogService = modelCatalogService;
        _fallbackContextWindowTokens = 4096;
    }

    public async Task<(int, int)> IngestDocumentAsync(string title, string originalText, DocumentType documentType, IReadOnlyList<string> topicNames, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(originalText))
        {
            throw new ArgumentException("Document text cannot be empty.", nameof(originalText));
        }

        if (topicNames.Count == 0)
        {
            throw new ArgumentException("At least one topic label is required.", nameof(topicNames));
        }

        var topicIds = await ResolveTopicIdsAsync(topicNames, cancellationToken);
        var documentId = await _documentRepository.CreateDocumentAsync(title, originalText, cancellationToken);
        await _documentRepository.LinkDocumentTopicsAsync(documentId, topicIds, cancellationToken);
        var chunksCount = await ReplaceChunksAsync(documentId, originalText, documentType, cancellationToken);

        return (documentId, chunksCount);
    }

    public async Task UpdateDocumentAsync(int documentId, string title, string originalText, DocumentType documentType, IReadOnlyList<string> topicNames, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(originalText))
        {
            throw new ArgumentException("Document text cannot be empty.", nameof(originalText));
        }

        if (topicNames.Count == 0)
        {
            throw new ArgumentException("At least one topic label is required.", nameof(topicNames));
        }

        var topicIds = await ResolveTopicIdsAsync(topicNames, cancellationToken);
        await _documentRepository.UpdateDocumentAsync(documentId, title, originalText, cancellationToken);
        await _documentRepository.ReplaceDocumentTopicsAsync(documentId, topicIds, cancellationToken);
        await ReplaceChunksAsync(documentId, originalText, documentType, cancellationToken);
    }

    private async Task<List<int>> ResolveTopicIdsAsync(IReadOnlyList<string> topicNames, CancellationToken cancellationToken)
    {
        var topicIds = new List<int>();
        foreach (var topicName in topicNames)
        {
            var topicId = await _documentRepository.GetTopicIdByNameAsync(topicName, cancellationToken);
            if (topicId is null)
            {
                throw new InvalidOperationException($"Topic '{topicName}' does not exist in rag.topics. Create it via the admin panel first.");
            }

            topicIds.Add(topicId.Value);
        }

        return topicIds;
    }

    private async Task<int> ReplaceChunksAsync(int documentId, string originalText, DocumentType documentType, CancellationToken cancellationToken)
    {
        await _documentRepository.DeleteChunksByDocumentAsync(documentId, cancellationToken);

        var (targetChunkSizeChars, overlapChars) = await _configurationRepository.GetChunkingSettingsAsync(cancellationToken);
        var chunks = documentType == DocumentType.Markdown
            ? ChunkMarkdownByHeaders(originalText, targetChunkSizeChars, overlapChars)
            : ChunkByParagraphWithOverlap(originalText, targetChunkSizeChars, overlapChars);

        for (int i = 0; i < chunks.Count; i++)
        {
            var embedding = await _modelGateway.GetEmbeddingAsync(EmbeddingModel, chunks[i], cancellationToken);
            await _documentRepository.AddChunkAsync(documentId, i, chunks[i], embedding, cancellationToken);
        }

        return chunks.Count;
    }

    private List<string> ChunkMarkdownByHeaders(string markdown, int targetChunkSizeChars, int overlapChars)
    {
        var sections = SplitByHeaders(markdown);
        var allChunks = new List<string>();
        foreach (var section in sections)
        {
            var sectionChunks = ChunkSectionByParagraphs(section, targetChunkSizeChars, overlapChars);
            allChunks.AddRange(sectionChunks);
        }

        return allChunks;
    }

    private List<string> ChunkSectionByParagraphs(string text, int targetChunkSizeChars, int overlapChars)
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

    private List<string> SplitOversizedParagraph(string paragraph, int targetChunkSizeChars)
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

    private List<string> CarryOverlap(List<string> paragraphs, int overlapChars)
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

    private List<string> SplitIntoParagraphs(string text)
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

    private List<string> SplitByHeaders(string markdown)
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

    private List<string> ChunkByParagraphWithOverlap(string text, int targetChunkSizeChars, int overlapChars)
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

    public async Task<string?> GetRelevantContextAsync(string model, string? classifiedTopic, string userMessage, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(classifiedTopic))
        {
            return null;
        }

        var allTopics = await _documentRepository.GetAllTopicsAsync(cancellationToken);
        if (!allTopics.Select(topic => topic.Name).Contains(classifiedTopic))
        {
            return null;
        }

        var queryEmbedding = await _modelGateway.GetEmbeddingAsync(EmbeddingModel, userMessage, cancellationToken);
        var topicId = allTopics.FirstOrDefault(t => t.Name == classifiedTopic)?.Id;
        if (topicId is null)
        {
            return null;
        }

        var candidates = await _documentRepository.SearchChunksByTopicAsync(topicId.Value, queryEmbedding, CandidatePoolSize, cancellationToken);
        var candidateList = candidates.ToList();
        if (candidateList.Count == 0)
        {
            return null;
        }

        int contextWindowTokens = await GetContextWindowTokensAsync(model, cancellationToken);
        int targetTokenBudget = (int)(contextWindowTokens * TargetInjectionFraction);
        int maxTokenBudget = (int)(contextWindowTokens * MaxInjectionFraction);
        var selectedChunks = new List<string>();
        int runningTokens = 0;
        foreach (var chunk in candidateList)
        {
            int chunkTokens = Math.Max(1, chunk.ChunkText.Length / CharsPerTokenApprox);
            if (runningTokens + chunkTokens > maxTokenBudget)
            {
                break;
            }

            selectedChunks.Add(chunk.ChunkText);
            runningTokens += chunkTokens;
            if (runningTokens >= targetTokenBudget)
            {
                break;
            }
        }

        return selectedChunks.Count > 0
            ? string.Join("\n\n---\n\n", selectedChunks)
            : null;
    }

    private async Task<int> GetContextWindowTokensAsync(string model, CancellationToken cancellationToken)
    {
        if (_contextWindowCache.TryGetValue(model, out int cached))
        {
            return cached;
        }

        var modelId = await _modelRepository.GetOrCreateModelIdAsync(model, cancellationToken);
        int reported = await _modelCatalogService.GetModelContextWindowAsync(model, cancellationToken);
        int resolved = reported > 0 ? reported : _fallbackContextWindowTokens;
        _contextWindowCache[model] = resolved;

        return resolved;
    }
}