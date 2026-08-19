using Dapper;
using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Domain.Documents;
using Npgsql;

namespace KnowledgeAssistant.Application.Services;

public class DocumentRepository : IDocumentRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public DocumentRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IEnumerable<Topic>> GetAllTopicsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var query = "SELECT id, name, parent_id AS ParentId FROM rag.topics ORDER BY name";
        return await connection.QueryAsync<Topic>(query);
    }

    public async Task<int?> GetTopicIdByNameAsync(string topicName, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var query = "SELECT id FROM rag.topics WHERE name = @TopicName";
        return await connection.QuerySingleOrDefaultAsync<int?>(query, new { TopicName = topicName });
    }

    public async Task<Topic> CreateTopicAsync(string name, int? parentId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            var query = """
                INSERT INTO rag.topics (name, parent_id)
                VALUES (@Name, @ParentId)
                RETURNING id, name, parent_id AS ParentId;
                """;
            return await connection.QuerySingleAsync<Topic>(query, new { Name = name, ParentId = parentId });
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException($"A topic named '{name}' already exists.");
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new InvalidOperationException("The selected parent topic no longer exists.");
        }
    }

    public async Task<bool> UpdateTopicAsync(int topicId, string name, int? parentId, CancellationToken cancellationToken)
    {
        if (parentId == topicId)
        {
            throw new InvalidOperationException("A topic cannot be its own parent.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            var query = "UPDATE rag.topics SET name = @Name, parent_id = @ParentId WHERE id = @TopicId";
            var rows = await connection.ExecuteAsync(query, new { TopicId = topicId, Name = name, ParentId = parentId });
            return rows > 0;
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException($"A topic named '{name}' already exists.");
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new InvalidOperationException("The selected parent topic no longer exists.");
        }
    }

    public async Task DeleteTopicAsync(int topicId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync("DELETE FROM rag.topics WHERE id = @TopicId", new { TopicId = topicId });
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new InvalidOperationException("This topic is still in use by one or more documents or conversations and cannot be deleted.");
        }
    }

    public async Task<int> CreateDocumentAsync(string title, string originalText, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var query = """
            INSERT INTO rag.documents (title, original_text, created_at)
            VALUES (@Title, @OriginalText, @CreatedAt)
            RETURNING id;
            """;

        return await connection.QuerySingleAsync<int>(query, new
        {
            Title = title,
            OriginalText = originalText,
            CreatedAt = DateTime.UtcNow
        });
    }

    public async Task LinkDocumentTopicsAsync(int documentId, IEnumerable<int> topicIds, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var query = """
            INSERT INTO rag.document_topics (document_id, topic_id)
            VALUES (@DocumentId, @TopicId)
            ON CONFLICT DO NOTHING;
            """;

        foreach (var topicId in topicIds)
        {
            await connection.ExecuteAsync(query, new { DocumentId = documentId, TopicId = topicId });
        }
    }

    public async Task AddChunkAsync(int documentId, int chunkIndex, string chunkText, float[] embedding, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var query = """
            INSERT INTO rag.chunks (document_id, chunk_index, chunk_text, embedding)
            VALUES (@DocumentId, @ChunkIndex, @ChunkText, @Embedding);
            """;

        // Requires dataSourceBuilder.UseVector() to have been called when the
        // NpgsqlDataSource singleton was built in Program.cs, so Npgsql recognizes Pgvector.Vector.
        await connection.ExecuteAsync(query, new
        {
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            ChunkText = chunkText,
            Embedding = new Pgvector.Vector(embedding)
        });
    }

    public async Task<IEnumerable<DocumentChunk>> SearchChunksByTopicAsync(
        int topicId, float[] queryEmbedding, int maxResults, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        // Cosine distance operator <=> is provided by pgvector; lower = more similar.
        var query = """
            SELECT c.id, c.document_id AS DocumentId, c.chunk_index AS ChunkIndex, c.chunk_text AS ChunkText
            FROM rag.chunks c
            INNER JOIN rag.document_topics dt ON dt.document_id = c.document_id
            WHERE dt.topic_id = @TopicId
            ORDER BY c.embedding <=> @QueryEmbedding
            LIMIT @MaxResults;
            """;

        return await connection.QueryAsync<DocumentChunk>(query, new
        {
            TopicId = topicId,
            QueryEmbedding = new Pgvector.Vector(queryEmbedding),
            MaxResults = maxResults
        });
    }

    public async Task<IEnumerable<Document>> GetAllDocumentsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var query = """
            SELECT d.id, d.title, d.original_text AS OriginalText, d.created_at AS CreatedAt,
                   COALESCE(array_agg(t.name) FILTER (WHERE t.name IS NOT NULL), ARRAY[]::text[]) AS Topics
            FROM rag.documents d
            LEFT JOIN rag.document_topics dt ON dt.document_id = d.id
            LEFT JOIN rag.topics t ON t.id = dt.topic_id
            GROUP BY d.id
            ORDER BY d.created_at DESC;
            """;

        return await connection.QueryAsync<Document>(query);
    }

    public async Task DeleteDocumentAsync(int documentId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        // rag.chunks and rag.document_topics both cascade via FK ON DELETE CASCADE
        var query = "DELETE FROM rag.documents WHERE id = @DocumentId";
        await connection.ExecuteAsync(query, new { DocumentId = documentId });
    }

    public async Task UpdateDocumentAsync(int documentId, string title, string originalText, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var query = "UPDATE rag.documents SET title = @Title, original_text = @OriginalText WHERE id = @DocumentId";
        await connection.ExecuteAsync(query, new { DocumentId = documentId, Title = title, OriginalText = originalText });
    }

    public async Task ReplaceDocumentTopicsAsync(int documentId, IEnumerable<int> topicIds, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync("DELETE FROM rag.document_topics WHERE document_id = @DocumentId", new { DocumentId = documentId });

        var query = """
            INSERT INTO rag.document_topics (document_id, topic_id)
            VALUES (@DocumentId, @TopicId)
            ON CONFLICT DO NOTHING;
            """;

        foreach (var topicId in topicIds)
        {
            await connection.ExecuteAsync(query, new { DocumentId = documentId, TopicId = topicId });
        }
    }

    public async Task DeleteChunksByDocumentAsync(int documentId, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync("DELETE FROM rag.chunks WHERE document_id = @DocumentId", new { DocumentId = documentId });
    }
}