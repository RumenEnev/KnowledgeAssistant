using KnowledgeAssistant.Application.Abstraction;
using System.Text;

namespace KnowledgeAssistant.Application.Services
{
    public class DocumentIngestionService
    {
        private readonly IModelGateway _modelGateway;
        private readonly IDocumentRepository _documentRepository;

        private const string EmbeddingModel = "nomic-embed-text";

        // Chunking parameters — tune once tested against real documents.
        private const int TargetChunkSizeChars = 1000;
        private const int OverlapChars = 150;

        public DocumentIngestionService(IModelGateway modelGateway, IDocumentRepository documentRepository)
        {
            _modelGateway = modelGateway;
            _documentRepository = documentRepository;
        }

        public async Task<int> IngestDocumentAsync(
            string title, string originalText, IReadOnlyList<string> topicNames, CancellationToken cancellationToken)
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

            var chunks = ChunkByParagraphWithOverlap(originalText);

            for (int i = 0; i < chunks.Count; i++)
            {
                var embedding = await _modelGateway.GetEmbeddingAsync(EmbeddingModel, chunks[i], cancellationToken);
                await _documentRepository.AddChunkAsync(documentId, i, chunks[i], embedding, cancellationToken);
            }
        }

        private static List<string> ChunkByParagraphWithOverlap(string text)
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
                if (currentChunk.Length > 0 && currentChunk.Length + paragraph.Length > TargetChunkSizeChars)
                {
                    string finishedChunk = currentChunk.ToString();
                    chunks.Add(finishedChunk.Trim());

                    string overlapText = finishedChunk.Length > OverlapChars
                        ? finishedChunk[^OverlapChars..]
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
