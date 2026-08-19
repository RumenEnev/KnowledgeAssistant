using KnowledgeAssistant.Domain.Documents;

namespace KnowledgeAssistant.Application.Abstraction;

public interface IDocumentRepository
{
    Task<IEnumerable<Topic>> GetAllTopicsAsync(CancellationToken cancellationToken);

    Task<int?> GetTopicIdByNameAsync(string topicName, CancellationToken cancellationToken);

    Task<Topic> CreateTopicAsync(string name, int? parentId, CancellationToken cancellationToken);

    Task<bool> UpdateTopicAsync(int topicId, string name, int? parentId, CancellationToken cancellationToken);

    Task DeleteTopicAsync(int topicId, CancellationToken cancellationToken);

    Task<int> CreateDocumentAsync(string title, string originalText, CancellationToken cancellationToken);

    Task LinkDocumentTopicsAsync(int documentId, IEnumerable<int> topicIds, CancellationToken cancellationToken);

    Task AddChunkAsync(int documentId, int chunkIndex, string chunkText, float[] embedding, CancellationToken cancellationToken);

    Task<IEnumerable<DocumentChunk>> SearchChunksByTopicAsync(int topicId, float[] queryEmbedding, int maxResults, CancellationToken cancellationToken);

    Task<IEnumerable<Document>> GetAllDocumentsAsync(CancellationToken cancellationToken);

    Task DeleteDocumentAsync(int documentId, CancellationToken cancellationToken);

    Task UpdateDocumentAsync(int documentId, string title, string originalText, CancellationToken cancellationToken);

    Task ReplaceDocumentTopicsAsync(int documentId, IEnumerable<int> topicIds, CancellationToken cancellationToken);

    Task DeleteChunksByDocumentAsync(int documentId, CancellationToken cancellationToken);
}