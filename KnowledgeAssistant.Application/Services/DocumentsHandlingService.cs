using Helper;
using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Contracts.Enums;
using KnowledgeAssistant.Domain.Documents;
using System.Collections.Concurrent;

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

    public async Task<(int, int)> IngestDocumentAsync(string title, string originalText, string embeddingModel, DocumentType documentType, IReadOnlyList<string> topicNames, CancellationToken cancellationToken)
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
        var chunksCount = await ReplaceChunksAsync(documentId, embeddingModel, originalText, documentType, cancellationToken);

        return (documentId, chunksCount);
    }

    public async Task UpdateDocumentAsync(int documentId, string title, string embeddingModel, string originalText, DocumentType documentType, IReadOnlyList<string> topicNames, CancellationToken cancellationToken)
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
        await ReplaceChunksAsync(documentId, embeddingModel, originalText, documentType, cancellationToken);
    }

    public async Task<int> GenerateChunkVectorsAsync(int documentId, string embeddingModel, IProgress<(int Done, int Total)>? progress, CancellationToken cancellationToken)
    {
        var chunks = (await _documentRepository.GetChunksByDocumentIdAsync(documentId, cancellationToken))
            .OrderBy(c => c.ChunkIndex)
            .ToList();

        for (int i = 0; i < chunks.Count; i++)
        {
            var embedding = await _modelGateway.GetEmbeddingAsync(embeddingModel, chunks[i].ChunkText, cancellationToken);
            await _documentRepository.UpsertChunkEmbeddingAsync(chunks[i].Id, embedding, embeddingModel, cancellationToken);
            progress?.Report((i + 1, chunks.Count));
        }

        return chunks.Count;
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

    private async Task<int> ReplaceChunksAsync(int documentId, string embeddingModel, string originalText, DocumentType documentType, CancellationToken cancellationToken)
    {
        await _documentRepository.DeleteChunksByDocumentAsync(documentId, cancellationToken);
        var chunkingSettings = await _configurationRepository.GetChunkingSettingsAsync(cancellationToken);
        var chunks = documentType == DocumentType.Markdown
            ? ChunkingHelper.ChunkMarkdownByHeaders(originalText, chunkingSettings.ChunkTargetSizeChars, chunkingSettings.ChunkOverlapChars)
            : ChunkingHelper.ChunkByParagraphWithOverlap(originalText, chunkingSettings.ChunkTargetSizeChars, chunkingSettings.ChunkOverlapChars);

        for (int i = 0; i < chunks.Count; i++)
        {
            var embedding = await _modelGateway.GetEmbeddingAsync(embeddingModel, chunks[i], cancellationToken);
            await _documentRepository.AddChunkAsync(documentId, i, chunks[i], embedding, embeddingModel, cancellationToken);
        }

        return chunks.Count;
    }

    public async Task<string?> GetRelevantContextAsync(string model, string? classifiedTopic, string userMessage, string embeddingModel, CancellationToken cancellationToken)
    {
        var selection = await SelectRelevantChunksAsync(model, classifiedTopic, userMessage, embeddingModel, cancellationToken);
        if (selection is null)
        {
            return null;
        }

        var includedTexts = selection.Chunks
            .Where(c => c.IncludedInBudget)
            .Select(c => c.ChunkText)
            .ToList();

        return includedTexts.Count > 0
            ? string.Join("\n\n---\n\n", includedTexts)
            : null;
    }

    public async Task<SelectedContextChunks?> SelectRelevantChunksAsync(string model, string? classifiedTopic, string userMessage, string embeddingModel, CancellationToken cancellationToken)
    {
        var context = await ResolveTopicDocumentsAsync(classifiedTopic, cancellationToken);
        if (context is null)
        {
            return null;
        }

        var queryEmbedding = await _modelGateway.GetEmbeddingAsync(embeddingModel, userMessage, cancellationToken);
        var allCandidates = new List<DocumentChunk>();

        foreach (var docId in context.DocumentIds)
        {
            var config = context.ConfigByDocument[docId];
            var docCandidates = await _documentRepository.SearchChunksByDocumentAsync(
                docId, queryEmbedding, userMessage,
                config.CandidatePoolSize, config.CandidateFanout, config.RrfK, cancellationToken);
            allCandidates.AddRange(docCandidates);
        }

        return await BuildSelectionAsync(model, allCandidates, context.ConfigByDocument, cancellationToken);
    }

    public async Task<SelectedContextChunks?> SelectRelevantChunksForModelAsync(string model, string embeddingModel, string? classifiedTopic, string userMessage, CancellationToken cancellationToken)
    {
        var context = await ResolveTopicDocumentsAsync(classifiedTopic, cancellationToken);
        if (context is null)
        {
            return null;
        }

        var queryEmbedding = await _modelGateway.GetEmbeddingAsync(embeddingModel, userMessage, cancellationToken);
        var allCandidates = new List<DocumentChunk>();

        foreach (var docId in context.DocumentIds)
        {
            var config = context.ConfigByDocument[docId];
            var docCandidates = await _documentRepository.SearchChunksByDocumentAndModelAsync(
                docId, embeddingModel, queryEmbedding, userMessage,
                config.CandidatePoolSize, config.CandidateFanout, config.RrfK, cancellationToken);
            allCandidates.AddRange(docCandidates);
        }

        return await BuildSelectionAsync(model, allCandidates, context.ConfigByDocument, cancellationToken);
    }

    private sealed record TopicDocumentContext(List<int> DocumentIds, Dictionary<int, DocumentRetrievalConfig> ConfigByDocument);

    private async Task<TopicDocumentContext?> ResolveTopicDocumentsAsync(string? classifiedTopic, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(classifiedTopic))
        {
            return null;
        }

        var allTopics = await _documentRepository.GetAllTopicsAsync(cancellationToken);
        var topicId = allTopics.FirstOrDefault(t => t.Name == classifiedTopic)?.Id;
        if (topicId is null)
        {
            return null;
        }

        var documentIds = await _documentRepository.GetDocumentIdsByTopicAsync(topicId.Value, cancellationToken);
        if (documentIds.Count == 0)
        {
            return null;
        }

        var savedConfigs = await _documentRepository.GetRetrievalConfigsAsync(documentIds, cancellationToken);
        var configByDocument = documentIds.ToDictionary(
            id => id,
            id => savedConfigs.TryGetValue(id, out var saved) ? saved : DocumentRetrievalConfig.Default(id));

        return new TopicDocumentContext(documentIds, configByDocument);
    }

    private async Task<SelectedContextChunks?> BuildSelectionAsync(string model, List<DocumentChunk> allCandidates, Dictionary<int, DocumentRetrievalConfig> configByDocument, CancellationToken cancellationToken)
    {
        var candidateList = allCandidates.OrderBy(c => c.Distance).ToList();
        if (candidateList.Count == 0)
        {
            return null;
        }

        int contextWindowTokens = await GetContextWindowTokensAsync(model, cancellationToken);
        int targetTokenBudget = (int)(contextWindowTokens * TargetInjectionFraction);
        int maxTokenBudget = (int)(contextWindowTokens * MaxInjectionFraction);

        var selectedChunks = new List<SelectedChunk>();
        int runningTokens = 0;
        bool budgetExhausted = false;
        int rank = 1;
        foreach (var chunk in candidateList)
        {
            int chunkTokens = Math.Max(1, chunk.ChunkText.Length / CharsPerTokenApprox);
            bool included = false;
            var chunkConfig = configByDocument[chunk.DocumentId];

            if (!budgetExhausted && chunk.Distance <= chunkConfig.MaxDistanceThreshold)
            {
                if (runningTokens + chunkTokens > maxTokenBudget)
                {
                    budgetExhausted = true;
                }
                else
                {
                    included = true;
                    runningTokens += chunkTokens;
                    if (runningTokens >= targetTokenBudget)
                    {
                        budgetExhausted = true;
                    }
                }
            }

            selectedChunks.Add(new SelectedChunk
            {
                ChunkId = chunk.Id,
                Rank = rank++,
                ChunkText = chunk.ChunkText,
                ApproxTokens = chunkTokens,
                IncludedInBudget = included
            });
        }

        return new SelectedContextChunks { Chunks = selectedChunks };
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

    public async Task<DocumentRetrievalConfig> GetDocumentRetrievalConfigAsync(int documentId, CancellationToken cancellationToken)
    {
        var saved = await _documentRepository.GetRetrievalConfigAsync(documentId, cancellationToken);
        return saved ?? DocumentRetrievalConfig.Default(documentId);
    }

    public Task SaveDocumentRetrievalConfigAsync(DocumentRetrievalConfig config, CancellationToken cancellationToken)
        => _documentRepository.SaveRetrievalConfigAsync(config, cancellationToken);

    public Task ResetDocumentRetrievalConfigAsync(int documentId, CancellationToken cancellationToken)
        => _documentRepository.DeleteRetrievalConfigAsync(documentId, cancellationToken);
}