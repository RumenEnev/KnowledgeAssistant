using KnowledgeAssistant.Application.Abstraction;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Text;

namespace KnowledgeAssistant.Application.Services
{
    public class DocumentsHandlingService
    {
        private readonly IModelGateway _modelGateway;
        private readonly IDocumentRepository _documentRepository;
        private readonly IConfigurationRepository _configurationRepository;
        private readonly IModelRepository _modelRepository;
        private static readonly ConcurrentDictionary<string, int> _contextWindowCache = new();
        private readonly int _fallbackContextWindowTokens;

        private const string EmbeddingModel = "nomic-embed-text";
        private const int CandidatePoolSize = 10;
        private const double TargetInjectionFraction = 0.30;
        private const double MaxInjectionFraction = 0.50;
        private const int CharsPerTokenApprox = 4;

        public DocumentsHandlingService(IModelGateway modelGateway, IDocumentRepository documentRepository, IModelRepository modelRepository, IConfigurationRepository configurationRepository)
        {
            _modelGateway = modelGateway;
            _documentRepository = documentRepository;
            _modelRepository = modelRepository;
            _configurationRepository = configurationRepository;
            _fallbackContextWindowTokens = 4096;
        }

        public async Task<int> IngestDocumentAsync(string title, string originalText, IReadOnlyList<string> topicNames, CancellationToken cancellationToken)
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
            await ReplaceChunksAsync(documentId, originalText, cancellationToken);

            return documentId;
        }

        public async Task UpdateDocumentAsync(int documentId, string title, string originalText, IReadOnlyList<string> topicNames, CancellationToken cancellationToken)
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
            await ReplaceChunksAsync(documentId, originalText, cancellationToken);
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

        private async Task ReplaceChunksAsync(int documentId, string originalText, CancellationToken cancellationToken)
        {
            await _documentRepository.DeleteChunksByDocumentAsync(documentId, cancellationToken);

            var (targetChunkSizeChars, overlapChars) = await _configurationRepository.GetChunkingSettingsAsync(cancellationToken);
            var chunks = ChunkByParagraphWithOverlap(originalText, targetChunkSizeChars, overlapChars);
            for (int i = 0; i < chunks.Count; i++)
            {
                var embedding = await _modelGateway.GetEmbeddingAsync(EmbeddingModel, chunks[i], cancellationToken);
                await _documentRepository.AddChunkAsync(documentId, i, chunks[i], embedding, cancellationToken);
            }
        }

        private static List<string> ChunkByParagraphWithOverlap(string text, int targetChunkSizeChars, int overlapChars)
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
            int? reported = await _modelRepository.GetContextWindowTokensAsync(modelId, cancellationToken);
            int resolved = reported ?? _fallbackContextWindowTokens;
            _contextWindowCache[model] = resolved;

            return resolved;
        }
    }
}