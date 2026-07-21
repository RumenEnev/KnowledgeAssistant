using KnowledgeAssistant.Application.Abstraction;
using System.Text;

namespace KnowledgeAssistant.Application.Services
{
    public class DocumentIngestionService
    {
        private readonly IModelGateway _modelGateway;
        private readonly IDocumentRepository _documentRepository;
        private readonly IConfigurationRepository _configurationRepository;

        private const string EmbeddingModel = "nomic-embed-text";

        public DocumentIngestionService(
            IModelGateway modelGateway, IDocumentRepository documentRepository, IConfigurationRepository configurationRepository)
        {
            _modelGateway = modelGateway;
            _documentRepository = documentRepository;
            _configurationRepository = configurationRepository;
        }

        public async Task<int> IngestDocumentAsync(string title, string originalText, IReadOnlyList<string> topicNames, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(originalText))
                throw new ArgumentException("Document text cannot be empty.", nameof(originalText));

            if (topicNames.Count == 0)
                throw new ArgumentException("At least one topic label is required.", nameof(topicNames));

            var topicIds = await ResolveTopicIdsAsync(topicNames, cancellationToken);

            var documentId = await _documentRepository.CreateDocumentAsync(title, originalText, cancellationToken);
            await _documentRepository.LinkDocumentTopicsAsync(documentId, topicIds, cancellationToken);

            await ReplaceChunksAsync(documentId, originalText, cancellationToken);

            return documentId;
        }

        public async Task UpdateDocumentAsync(
            int documentId, string title, string originalText, IReadOnlyList<string> topicNames, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(originalText))
                throw new ArgumentException("Document text cannot be empty.", nameof(originalText));

            if (topicNames.Count == 0)
                throw new ArgumentException("At least one topic label is required.", nameof(topicNames));

            var topicIds = await ResolveTopicIdsAsync(topicNames, cancellationToken);

            await _documentRepository.UpdateDocumentAsync(documentId, title, originalText, cancellationToken);
            await _documentRepository.ReplaceDocumentTopicsAsync(documentId, topicIds, cancellationToken);

            await ReplaceChunksAsync(documentId, originalText, cancellationToken);
        }

        private async Task<List<int>> ResolveTopicIdsAsync(IReadOnlyList<string> topicNames, CancellationToken cancellationToken)
        {
            // Resolve topic names to IDs up front so we fail fast on a typo
            // before doing any embedding work.
            var topicIds = new List<int>();
            foreach (var topicName in topicNames)
            {
                var topicId = await _documentRepository.GetTopicIdByNameAsync(topicName, cancellationToken);
                if (topicId is null)
                    throw new InvalidOperationException(
                        $"Topic '{topicName}' does not exist in rag.topics. Create it via the admin panel first.");

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
                chunks.Add(currentChunk.ToString().Trim());

            return chunks;
        }
    }
}
